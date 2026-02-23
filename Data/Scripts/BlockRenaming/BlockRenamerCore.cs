// Data/Scripts/BlockRenaming/BlockRenamerCore.cs
//
// CHANGELOG v1.5.12:
// - [NEW] ApplyAction() method extracted from OnMessageReceived to avoid code duplication.
// - [NEW] SendNetworkRequest() now checks MyAPIGateway.Session.IsServer before sending
//         a network message. If true (singleplayer, host, or Pulsar), the action is applied
//         directly via ApplyAction() without going through the network layer.
//         This fixes the mod not working in Pulsar and other single-instance environments.
// - [CHANGED] OnMessageReceived() now delegates to ApplyAction() instead of
//             containing its own switch block.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace BlockRenaming
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class BlockRenamerCore : MySessionComponentBase
    {
        private const string MOD_VERSION = "1.5.12";
        public const ushort NETWORK_ID = 58432;

        private bool _isInitialized = false;

        // Temporary storage for UI inputs (client-side only)
        private readonly Dictionary<IMyTerminalBlock, string> TempStringFinds =
            new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempStringRenames =
            new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempRegexReplace =
            new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempCounterFormat =
            new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempNumberSeparator =
            new Dictionary<IMyTerminalBlock, string>();

        // Per-grid numbering counters
        private static readonly Dictionary<long, int> GridNumberCounters =
            new Dictionary<long, int>();

        // Per-block-type counters for grouped numbering
        private static readonly Dictionary<string, int> BlockTypeCounters =
            new Dictionary<string, int>();

        // Group by block type toggle
        private static bool GroupByBlockType = false;

        private static string GlobalThrusterTemplate = "Thruster {0}";

        // Auto-continue numbering toggle (default ON)
        private static bool AutoContinueNumbering = true;

        // Control lists
        private List<IMyTerminalControl> ControlsListMain = null;
        private List<IMyTerminalControl> ControlsListThrusters = null;

        // ─────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────

        public override void LoadData()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NETWORK_ID, OnMessageReceived);
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(NETWORK_ID, OnMessageReceived);
            MyAPIGateway.TerminalControls.CustomControlGetter -= AddControlsToBlocks;

            GridNumberCounters.Clear();
            BlockTypeCounters.Clear();
            TempStringFinds.Clear();
            TempStringRenames.Clear();
            TempRegexReplace.Clear();
            TempCounterFormat.Clear();
            TempNumberSeparator.Clear();

            if (ControlsListMain != null)
                ControlsListMain.Clear();
            if (ControlsListThrusters != null)
                ControlsListThrusters.Clear();
        }

        public override void UpdateBeforeSimulation()
        {
            if (_isInitialized || MyAPIGateway.Session == null)
                return;

            // Skip UI setup on dedicated servers — they only handle network messages
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                _isInitialized = true;
                return;
            }

            _isInitialized = true;

            ControlsListMain = CreateControlList();
            ControlsListThrusters = CreateThrusterControlList();

            MyAPIGateway.TerminalControls.CustomControlGetter += AddControlsToBlocks;

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                this.SetUpdateOrder(MyUpdateOrder.NoUpdate);
            });
        }

        // ─────────────────────────────────────────────────────────────
        //  Terminal control injection
        // ─────────────────────────────────────────────────────────────

        public void AddControlsToBlocks(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block == null || ControlsListMain == null)
                return;

            foreach (var control in ControlsListMain)
                controls.Add(control);

            if (block is IMyThrust && ControlsListThrusters != null)
            {
                foreach (var control in ControlsListThrusters)
                    controls.Add(control);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  UI — Main control list
        // ─────────────────────────────────────────────────────────────

        private List<IMyTerminalControl> CreateControlList()
        {
            var list = new List<IMyTerminalControl>();

            // Main separator
            var separator0 = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlSeparator,
                IMyTerminalBlock
            >("Renamer_MainSeparator");
            separator0.Enabled = (b) => true;
            separator0.SupportsMultipleBlocks = true;
            list.Add(separator0);

            // Version label
            var label0 = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlLabel,
                IMyTerminalBlock
            >("Renamer_Label");
            label0.Enabled = (b) => true;
            label0.SupportsMultipleBlocks = true;
            label0.Label = MyStringId.GetOrCompute(
                string.Format("Block Renaming Controls v{0}", MOD_VERSION)
            );
            list.Add(label0);

            // New name textbox
            var textbox0 = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlTextbox,
                IMyTerminalBlock
            >("Renamer_Textbox");
            textbox0.Enabled = (b) => true;
            textbox0.SupportsMultipleBlocks = true;
            textbox0.Title = MyStringId.GetOrCompute("New Naming");
            textbox0.Getter = (b) =>
            {
                string value;
                if (!TempStringRenames.TryGetValue(b, out value))
                    value = "";
                return new StringBuilder(value);
            };
            textbox0.Setter = (b, Builder) =>
            {
                TempStringRenames[b] = Builder.ToString();
            };
            list.Add(textbox0);

            // Replace button
            var replaceBtn = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlButton,
                IMyTerminalBlock
            >("Renamer_RenameButton");
            replaceBtn.Enabled = (b) => true;
            replaceBtn.SupportsMultipleBlocks = true;
            replaceBtn.Title = MyStringId.GetOrCompute("Replace");
            replaceBtn.Action = (b) =>
            {
                string value;
                if (!TempStringRenames.TryGetValue(b, out value))
                    value = "";
                SendNetworkRequest(b, "REPLACE", value);
            };
            list.Add(replaceBtn);

            // Prefix button
            var prefixBtn = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlButton,
                IMyTerminalBlock
            >("Renamer_PrefixButton");
            prefixBtn.Enabled = (b) => true;
            prefixBtn.SupportsMultipleBlocks = true;
            prefixBtn.Title = MyStringId.GetOrCompute("Prefix");
            prefixBtn.Action = (b) =>
            {
                string value;
                if (!TempStringRenames.TryGetValue(b, out value))
                    value = "";
                SendNetworkRequest(b, "PREFIX", value);
            };
            list.Add(prefixBtn);

            // Suffix button
            var suffixBtn = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlButton,
                IMyTerminalBlock
            >("Renamer_SuffixButton");
            suffixBtn.Enabled = (b) => true;
            suffixBtn.SupportsMultipleBlocks = true;
            suffixBtn.Title = MyStringId.GetOrCompute("Suffix");
            suffixBtn.Action = (b) =>
            {
                string value;
                if (!TempStringRenames.TryGetValue(b, out value))
                    value = "";
                SendNetworkRequest(b, "SUFFIX", value);
            };
            list.Add(suffixBtn);

            // Counter format textbox
            var counterTxt = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlTextbox,
                IMyTerminalBlock
            >("Renamer_CounterFormatTextbox");
            counterTxt.Enabled = (b) => true;
            counterTxt.SupportsMultipleBlocks = true;
            counterTxt.Title = MyStringId.GetOrCompute("Counter Format (e.g. 01)");
            counterTxt.Getter = (b) =>
            {
                string value;
                if (!TempCounterFormat.TryGetValue(b, out value))
                    value = "01";
                return new StringBuilder(value);
            };
            counterTxt.Setter = (b, Builder) =>
            {
                TempCounterFormat[b] = Builder.ToString();
            };
            list.Add(counterTxt);

            // Current counter status label
            var counterStatusLabel = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlLabel,
                IMyTerminalBlock
            >("Renamer_CounterStatusLabel");
            counterStatusLabel.Enabled = (b) =>
            {
                counterStatusLabel.Label = MyStringId.GetOrCompute(GetCurrentCounterStatus(b));
                return true;
            };
            counterStatusLabel.SupportsMultipleBlocks = false;
            list.Add(counterStatusLabel);

            // Auto-continue numbering checkbox
            var autoContinueCheckbox = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlCheckbox,
                IMyTerminalBlock
            >("Renamer_AutoContinueCheckbox");
            autoContinueCheckbox.Enabled = (b) => true;
            autoContinueCheckbox.SupportsMultipleBlocks = true;
            autoContinueCheckbox.Title = MyStringId.GetOrCompute("Auto Continue Numbering");
            autoContinueCheckbox.Tooltip = MyStringId.GetOrCompute(
                "Continue from last number instead of resetting to format"
            );
            autoContinueCheckbox.Getter = (b) => AutoContinueNumbering;
            autoContinueCheckbox.Setter = (b, value) => AutoContinueNumbering = value;
            list.Add(autoContinueCheckbox);

            // Number separator textbox
            var separatorTxt = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlTextbox,
                IMyTerminalBlock
            >("Renamer_NumberSeparatorTextbox");
            separatorTxt.Enabled = (b) => true;
            separatorTxt.SupportsMultipleBlocks = true;
            separatorTxt.Title = MyStringId.GetOrCompute("Number Separator (default: space)");
            separatorTxt.Getter = (b) =>
            {
                string value;
                if (!TempNumberSeparator.TryGetValue(b, out value))
                    value = " ";
                return new StringBuilder(value);
            };
            separatorTxt.Setter = (b, Builder) =>
            {
                TempNumberSeparator[b] = Builder.ToString();
            };
            list.Add(separatorTxt);

            // Group by Block Type checkbox
            var groupCheckbox = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlCheckbox,
                IMyTerminalBlock
            >("Renamer_GroupByTypeCheckbox");
            groupCheckbox.Enabled = (b) => true;
            groupCheckbox.SupportsMultipleBlocks = true;
            groupCheckbox.Title = MyStringId.GetOrCompute("Group by Block Type");
            groupCheckbox.Tooltip = MyStringId.GetOrCompute(
                "Auto-reset counter for each block type"
            );
            groupCheckbox.Getter = (b) => GroupByBlockType;
            groupCheckbox.Setter = (b, value) =>
            {
                GroupByBlockType = value;
                if (value)
                    BlockTypeCounters.Clear();
            };
            list.Add(groupCheckbox);

            // Reset Counter button
            var resetCounterBtn = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlButton,
                IMyTerminalBlock
            >("Renamer_ResetCounterButton");
            resetCounterBtn.Enabled = (b) => true;
            resetCounterBtn.SupportsMultipleBlocks = true;
            resetCounterBtn.Title = MyStringId.GetOrCompute("Reset Counter");
            resetCounterBtn.Action = (b) =>
            {
                long gridId = b.CubeGrid.EntityId;
                string format;
                if (!TempCounterFormat.TryGetValue(b, out format))
                    format = "001";

                string digitsOnly = new string(format.Where(char.IsDigit).ToArray());
                int startNumber = 0;
                if (!string.IsNullOrEmpty(digitsOnly))
                    int.TryParse(digitsOnly, out startNumber);

                GridNumberCounters[gridId] = startNumber;
                if (GroupByBlockType)
                    BlockTypeCounters.Clear();
            };
            list.Add(resetCounterBtn);

            // Number Prefix button
            var numPrefixBtn = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlButton,
                IMyTerminalBlock
            >("Renamer_NumberPrefixButton");
            numPrefixBtn.Enabled = (b) => true;
            numPrefixBtn.SupportsMultipleBlocks = true;
            numPrefixBtn.Title = MyStringId.GetOrCompute("Number Prefix");
            numPrefixBtn.Action = (b) => ProcessNumbering(b, true);
            list.Add(numPrefixBtn);

            // Number Suffix button
            var numBtn = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlButton,
                IMyTerminalBlock
            >("Renamer_NumberSuffixButton");
            numBtn.Enabled = (b) => true;
            numBtn.SupportsMultipleBlocks = true;
            numBtn.Title = MyStringId.GetOrCompute("Number Suffix");
            numBtn.Action = (b) => ProcessNumbering(b, false);
            list.Add(numBtn);

            // Reset to default button
            var resetBtn = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlButton,
                IMyTerminalBlock
            >("Renamer_ResetButton");
            resetBtn.Enabled = (b) => true;
            resetBtn.SupportsMultipleBlocks = true;
            resetBtn.Title = MyStringId.GetOrCompute("Reset to default");
            resetBtn.Action = (b) => SendNetworkRequest(b, "RESET", "");
            list.Add(resetBtn);

            // Regex section separator
            var regexSep = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlSeparator,
                IMyTerminalBlock
            >("RegRen_Separator");
            regexSep.Enabled = (b) => true;
            regexSep.SupportsMultipleBlocks = true;
            list.Add(regexSep);

            // Regex find textbox
            var findTxt = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlTextbox,
                IMyTerminalBlock
            >("RegRen_FindTextbox");
            findTxt.Enabled = (b) => true;
            findTxt.SupportsMultipleBlocks = true;
            findTxt.Title = MyStringId.GetOrCompute("Find (Pattern)");
            findTxt.Getter = (b) =>
            {
                string value;
                if (!TempStringFinds.TryGetValue(b, out value))
                    value = "";
                return new StringBuilder(value);
            };
            findTxt.Setter = (b, Builder) =>
            {
                TempStringFinds[b] = Builder.ToString();
            };
            list.Add(findTxt);

            // Regex replace-with textbox
            var regexReplaceTxt = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlTextbox,
                IMyTerminalBlock
            >("RegRen_ReplaceWithTextbox");
            regexReplaceTxt.Enabled = (b) => true;
            regexReplaceTxt.SupportsMultipleBlocks = true;
            regexReplaceTxt.Title = MyStringId.GetOrCompute("Replace With");
            regexReplaceTxt.Getter = (b) =>
            {
                string value;
                if (!TempRegexReplace.TryGetValue(b, out value))
                    value = "";
                return new StringBuilder(value);
            };
            regexReplaceTxt.Setter = (b, Builder) =>
            {
                TempRegexReplace[b] = Builder.ToString();
            };
            list.Add(regexReplaceTxt);

            // Regex replace button
            var regexBtn = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlButton,
                IMyTerminalBlock
            >("RegRen_ReplaceButton_v2");
            regexBtn.Enabled = (b) => true;
            regexBtn.SupportsMultipleBlocks = true;
            regexBtn.Title = MyStringId.GetOrCompute("Replace");
            regexBtn.Action = (b) =>
            {
                string findValue,
                    replaceValue;
                if (!TempStringFinds.TryGetValue(b, out findValue))
                    findValue = "";
                if (!TempRegexReplace.TryGetValue(b, out replaceValue))
                    replaceValue = "";
                SendNetworkRequest(b, "REGEX", string.Format("{0}#{1}", findValue, replaceValue));
            };
            list.Add(regexBtn);

            return list;
        }

        // ─────────────────────────────────────────────────────────────
        //  UI — Thruster control list
        // ─────────────────────────────────────────────────────────────

        private List<IMyTerminalControl> CreateThrusterControlList()
        {
            var list = new List<IMyTerminalControl>();

            var thrusterSep = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlSeparator,
                IMyThrust
            >("TR_Separator");
            thrusterSep.Enabled = (b) => true;
            thrusterSep.SupportsMultipleBlocks = true;
            list.Add(thrusterSep);

            var thrusterTxt = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlTextbox,
                IMyThrust
            >("TR_TemplateTextbox");
            thrusterTxt.Enabled = (b) => true;
            thrusterTxt.SupportsMultipleBlocks = true;
            thrusterTxt.Title = MyStringId.GetOrCompute("Template ({0} = direction)");
            thrusterTxt.Getter = (b) => new StringBuilder(GlobalThrusterTemplate);
            thrusterTxt.Setter = (b, Builder) => GlobalThrusterTemplate = Builder.ToString();
            list.Add(thrusterTxt);

            var btn = MyAPIGateway.TerminalControls.CreateControl<
                IMyTerminalControlButton,
                IMyThrust
            >("TR_RenameButton");
            btn.Enabled = (b) => true;
            btn.SupportsMultipleBlocks = true;
            btn.Title = MyStringId.GetOrCompute("Rename by Direction");
            btn.Action = (b) => SendNetworkRequest(b, "THRUST", GlobalThrusterTemplate);
            list.Add(btn);

            return list;
        }

        // ─────────────────────────────────────────────────────────────
        //  Numbering logic
        // ─────────────────────────────────────────────────────────────

        private void ProcessNumbering(IMyTerminalBlock block, bool isPrefix)
        {
            long gridId = block.CubeGrid.EntityId;

            string format;
            if (!TempCounterFormat.TryGetValue(block, out format))
                format = "01";

            string separator;
            if (!TempNumberSeparator.TryGetValue(block, out separator))
                separator = " ";

            if (!AutoContinueNumbering)
            {
                string digitsOnly = new string(format.Where(char.IsDigit).ToArray());
                int startNumber = 0;
                if (!string.IsNullOrEmpty(digitsOnly))
                    int.TryParse(digitsOnly, out startNumber);

                if (GroupByBlockType)
                    BlockTypeCounters[block.DefinitionDisplayNameText] = startNumber;
                else
                    GridNumberCounters[gridId] = startNumber;
            }

            int currentNumber;
            if (GroupByBlockType)
            {
                string blockType = block.DefinitionDisplayNameText;
                if (!BlockTypeCounters.ContainsKey(blockType))
                {
                    string digitsOnly = new string(format.Where(char.IsDigit).ToArray());
                    int startNumber = 0;
                    if (!string.IsNullOrEmpty(digitsOnly))
                        int.TryParse(digitsOnly, out startNumber);
                    BlockTypeCounters[blockType] = startNumber;
                }
                currentNumber = BlockTypeCounters[blockType];
                BlockTypeCounters[blockType] = currentNumber + 1;
            }
            else
            {
                if (!GridNumberCounters.ContainsKey(gridId))
                {
                    string digitsOnly = new string(format.Where(char.IsDigit).ToArray());
                    int startNumber = 0;
                    if (!string.IsNullOrEmpty(digitsOnly))
                        int.TryParse(digitsOnly, out startNumber);
                    GridNumberCounters[gridId] = startNumber;
                }
                currentNumber = GridNumberCounters[gridId];
                GridNumberCounters[gridId] = currentNumber + 1;
            }

            string digitsFromFormat = new string(format.Where(char.IsDigit).ToArray());
            int digitCount = digitsFromFormat.Length > 0 ? digitsFromFormat.Length : 1;
            string formatted = currentNumber.ToString("D" + digitCount);

            if (isPrefix)
                SendNetworkRequest(block, "NUMPREFIX", formatted + separator);
            else
                SendNetworkRequest(block, "NUM", separator + formatted);
        }

        // ─────────────────────────────────────────────────────────────
        //  Networking
        //
        //  [CHANGED] SendNetworkRequest now detects whether the current
        //  session is also the server (singleplayer, host, Pulsar).
        //  If so, it calls ApplyAction() directly, skipping the network
        //  layer entirely. This fixes the mod in Pulsar and singleplayer.
        //  In a real dedicated-server multiplayer session the message is
        //  still sent over the network as before.
        // ─────────────────────────────────────────────────────────────

        private void SendNetworkRequest(IMyTerminalBlock block, string action, string value)
        {
            // If this client IS the server, apply the action directly — no network needed.
            // This covers: singleplayer, host in listen-server, and environments like Pulsar
            // where there is no separate dedicated server process.
            if (MyAPIGateway.Session.IsServer)
            {
                ApplyAction(block, action, value);
                return;
            }

            // Dedicated multiplayer: send the request to the server.
            var data = string.Format("{0}|{1}|{2}", block.EntityId, action, value);
            MyAPIGateway.Multiplayer.SendMessageToServer(NETWORK_ID, Encoding.UTF8.GetBytes(data));
        }

        // [CHANGED] OnMessageReceived now delegates to ApplyAction() instead of
        // containing its own switch block. Logic is identical; duplication removed.
        private void OnMessageReceived(ushort id, byte[] data, ulong sender, bool fromServer)
        {
            if (!MyAPIGateway.Session.IsServer)
                return;
            try
            {
                var message = Encoding.UTF8.GetString(data);
                var parts = message.Split('|');
                if (parts.Length < 3)
                    return;

                long entityId;
                if (!long.TryParse(parts[0], out entityId))
                    return;

                var block = MyAPIGateway.Entities.GetEntityById(entityId) as IMyTerminalBlock;
                if (block == null)
                    return;

                ApplyAction(block, parts[1], parts[2]);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("Renamer Error: " + ex.Message);
            }
        }

        // [NEW] Central action dispatcher used by both SendNetworkRequest (direct path)
        // and OnMessageReceived (network path). Single source of truth for all rename logic.
        private void ApplyAction(IMyTerminalBlock block, string action, string value)
        {
            switch (action)
            {
                case "REPLACE":
                    block.CustomName = value;
                    break;
                case "PREFIX":
                    block.CustomName = value + block.CustomName;
                    break;
                case "SUFFIX":
                    block.CustomName = block.CustomName + value;
                    break;
                case "RESET":
                    block.CustomName = block.DefinitionDisplayNameText;
                    break;
                case "NUMPREFIX":
                    block.CustomName = value + block.CustomName;
                    break;
                case "NUM":
                    block.CustomName += value;
                    break;
                case "THRUST":
                    ApplyThrusterRename(block as IMyThrust, value);
                    break;
                case "REGEX":
                    var regexParts = value.Split('#');
                    if (regexParts.Length == 2)
                    {
                        try
                        {
                            block.CustomName = Regex.Replace(
                                block.CustomName,
                                regexParts[0],
                                regexParts[1]
                            );
                        }
                        catch
                        { /* invalid regex — silently ignore */
                        }
                    }
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Thruster direction rename
        // ─────────────────────────────────────────────────────────────

        private void ApplyThrusterRename(IMyThrust thruster, string template)
        {
            if (thruster == null)
                return;

            var direction = thruster.GridThrustDirection;
            string letter = "X";

            if (direction == Vector3I.Forward)
                letter = "B";
            else if (direction == Vector3I.Backward)
                letter = "F";
            else if (direction == Vector3I.Up)
                letter = "D";
            else if (direction == Vector3I.Down)
                letter = "U";
            else if (direction == Vector3I.Left)
                letter = "R";
            else if (direction == Vector3I.Right)
                letter = "L";

            thruster.CustomName = template.Contains("{0}")
                ? template.Replace("{0}", letter)
                : template + " " + letter;
        }

        // ─────────────────────────────────────────────────────────────
        //  Counter status helper
        // ─────────────────────────────────────────────────────────────

        private string GetCurrentCounterStatus(IMyTerminalBlock block)
        {
            if (block == null || block.CubeGrid == null)
                return "Counter: N/A";

            int current = 0;
            if (GroupByBlockType)
            {
                if (BlockTypeCounters.TryGetValue(block.DefinitionDisplayNameText, out current))
                    return string.Format("Next {0}: {1}", block.DefinitionDisplayNameText, current);
                return "Next: Ready";
            }
            if (GridNumberCounters.TryGetValue(block.CubeGrid.EntityId, out current))
                return string.Format("Next Number: {0}", current);
            return "Next: Ready";
        }
    }
}
