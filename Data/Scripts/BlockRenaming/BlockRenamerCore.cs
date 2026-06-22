// /Data/Scripts/BlockRenaming/BlockRenamerCore.cs
// MAMBA BlockRenamerCore/MODULE_TYPE

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
        public const string MOD_VERSION = "1.5.30";
        public const ushort NETWORK_ID = 58432;

        private bool _isInitialized = false;

        // Temporary storage for UI inputs (client-side only)
        private readonly Dictionary<IMyTerminalBlock, string> TempStringFinds = new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempStringRenames = new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempRegexReplace = new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempCounterFormat = new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempNumberSeparator = new Dictionary<IMyTerminalBlock, string>();

        // Global panel visibility state across all terminal blocks
        private static bool GlobalShowRenamePanel = false;

        // Numbering counters
        private static readonly Dictionary<long, int> GridNumberCounters = new Dictionary<long, int>();
        private static readonly Dictionary<string, int> BlockTypeCounters = new Dictionary<string, int>();
        private static bool GroupByBlockType = false;
        private static string GlobalThrusterTemplate = "Thruster {0}";
        private static bool AutoContinueNumbering = true;

        private static List<IMyTerminalControl> ControlsListMain = null;
        private static List<IMyTerminalControl> ControlsListThrusters = null;

        // ─────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────

        public override void LoadData()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NETWORK_ID, OnMessageReceived);
            MyLog.Default.WriteLine("BlockRenamer v" + MOD_VERSION + " initialized.");
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

            if (ControlsListMain != null) ControlsListMain.Clear();
            if (ControlsListThrusters != null) ControlsListThrusters.Clear();
        }

        public override void UpdateBeforeSimulation()
        {
            if (_isInitialized || MyAPIGateway.Session == null) return;
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
        //  Terminal Control Injection
        // ─────────────────────────────────────────────────────────────

        public void AddControlsToBlocks(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block == null || ControlsListMain == null) return;

            try
            {
                foreach (var control in ControlsListMain)
                {
                    bool exists = false;
                    foreach (var existing in controls)
                    {
                        if (existing.Id == control.Id) { exists = true; break; }
                    }
                    if (!exists) controls.Add(control);
                }

                if (block is IMyThrust && ControlsListThrusters != null)
                {
                    foreach (var control in ControlsListThrusters)
                    {
                        bool exists = false;
                        foreach (var existing in controls)
                        {
                            if (existing.Id == control.Id) { exists = true; break; }
                        }
                        if (!exists) controls.Add(control);
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine("BlockRenamer: Error - " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Force UI Refresh Hack
        // ─────────────────────────────────────────────────────────────
        private static void ForceTerminalRefresh(IMyTerminalBlock block)
        {
            if (block == null) return;

            // 1. Force refresh on the main block object
            block.UpdateVisual();

            // 2. Loop through all custom controls and force individual updates to bypass cache
            if (ControlsListMain != null)
            {
                foreach (var ctrl in ControlsListMain)
                {
                    ctrl.UpdateVisual();
                }
            }
            if (block is IMyThrust && ControlsListThrusters != null)
            {
                foreach (var ctrl in ControlsListThrusters)
                {
                    ctrl.UpdateVisual();
                }
            }

            // 3. Defer the name change toggle to the next game thread frame to invalidate UI hierarchy layout
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                var gui = MyAPIGateway.Gui;
                if (gui != null && gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                {
                    string currentName = block.CustomName;
                    block.CustomName = currentName + " ";
                    block.CustomName = currentName;
                }
            });
        }

        // ─────────────────────────────────────────────────────────────
        //  UI Factory - Main Controls
        // ─────────────────────────────────────────────────────────────

        private List<IMyTerminalControl> CreateControlList()
        {
            var list = new List<IMyTerminalControl>();

            // Top Custom Divider
            var topSeparator = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyTerminalBlock>("Renamer_Label_Top");
            topSeparator.Visible = (b) => true;
            topSeparator.SupportsMultipleBlocks = true;
            topSeparator.Label = MyStringId.GetOrCompute("------------------------------------------------------------------");
            list.Add(topSeparator);

            // Visibility Toggle Checkbox with Tips & Tricks Tooltip Help
            var showPanelCheckbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>("Renamer_ShowPanelCheckbox");
            showPanelCheckbox.Enabled = (b) => true;
            showPanelCheckbox.Visible = (b) => true;
            showPanelCheckbox.SupportsMultipleBlocks = true;
            showPanelCheckbox.Title = MyStringId.GetOrCompute(string.Format("Show Rename v{0} Controls", MOD_VERSION));
            showPanelCheckbox.Tooltip = MyStringId.GetOrCompute(
                "Toggle visibility of all block renaming controls.\n\n" +
                "[TIP]: If the UI panel does not show or hide immediately due to game engine caching,\n" +
                "simply select another block and click back to force an instant layout refresh."
             );
            showPanelCheckbox.Getter = (b) => GlobalShowRenamePanel;
            showPanelCheckbox.Setter = (b, value) =>
            {
                GlobalShowRenamePanel = value;
                ForceTerminalRefresh(b);
            };
            list.Add(showPanelCheckbox);

            // Main Label Panel Title
            var label0 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyTerminalBlock>("Renamer_Label_Title");
            label0.Visible = (b) => GlobalShowRenamePanel;
            label0.SupportsMultipleBlocks = true;
            label0.Label = MyStringId.GetOrCompute(string.Format("Block Renaming Controls v{0}", MOD_VERSION));
            list.Add(label0);

            // New Name Input
            var textbox0 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyTerminalBlock>("Renamer_Textbox");
            textbox0.Visible = (b) => GlobalShowRenamePanel;
            textbox0.SupportsMultipleBlocks = true;
            textbox0.Title = MyStringId.GetOrCompute("New Naming");
            textbox0.Getter = (b) =>
            {
                string value;
                if (b == null || !TempStringRenames.TryGetValue(b, out value)) value = "";
                return new StringBuilder(value);
            };
            textbox0.Setter = (b, Builder) => { if (b != null) TempStringRenames[b] = Builder.ToString(); };
            list.Add(textbox0);

            // Replace Operation Button
            var replaceBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_RenameButton");
            replaceBtn.Visible = (b) => GlobalShowRenamePanel;
            replaceBtn.SupportsMultipleBlocks = true;
            replaceBtn.Title = MyStringId.GetOrCompute("Replace");
            replaceBtn.Action = (b) =>
            {
                if (b == null) return;
                string value;
                if (!TempStringRenames.TryGetValue(b, out value)) value = "";
                SendNetworkRequest(b, "REPLACE", value);
            };
            list.Add(replaceBtn);

            // Prefix Operation Button
            var prefixBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_PrefixButton");
            prefixBtn.Visible = (b) => GlobalShowRenamePanel;
            prefixBtn.SupportsMultipleBlocks = true;
            prefixBtn.Title = MyStringId.GetOrCompute("Prefix");
            prefixBtn.Action = (b) =>
            {
                if (b == null) return;
                string value;
                if (!TempStringRenames.TryGetValue(b, out value)) value = "";
                SendNetworkRequest(b, "PREFIX", value);
            };
            list.Add(prefixBtn);

            // Suffix Operation Button
            var suffixBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_SuffixButton");
            suffixBtn.Visible = (b) => GlobalShowRenamePanel;
            suffixBtn.SupportsMultipleBlocks = true;
            suffixBtn.Title = MyStringId.GetOrCompute("Suffix");
            suffixBtn.Action = (b) =>
            {
                if (b == null) return;
                string value;
                if (!TempStringRenames.TryGetValue(b, out value)) value = "";
                SendNetworkRequest(b, "SUFFIX", value);
            };
            list.Add(suffixBtn);

            // Counter Format Definition Textbox
            var counterTxt = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyTerminalBlock>("Renamer_CounterFormatTextbox");
            counterTxt.Visible = (b) => GlobalShowRenamePanel;
            counterTxt.SupportsMultipleBlocks = true;
            counterTxt.Title = MyStringId.GetOrCompute("Counter Format (e.g. 01)");
            counterTxt.Getter = (b) =>
            {
                string value;
                if (b == null || !TempCounterFormat.TryGetValue(b, out value)) value = "01";
                return new StringBuilder(value);
            };
            counterTxt.Setter = (b, Builder) => { if (b != null) TempCounterFormat[b] = Builder.ToString(); };
            list.Add(counterTxt);

            // Current Counter Status Information Label
            var counterStatusLabel = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyTerminalBlock>("Renamer_Label_CounterStatus");
            counterStatusLabel.Visible = (b) => GlobalShowRenamePanel;
            counterStatusLabel.SupportsMultipleBlocks = true;
            counterStatusLabel.Label = MyStringId.GetOrCompute("Counter Status");
            list.Add(counterStatusLabel);

            // Sequential Continuous Numbering Toggle Checkbox
            var autoContinueCheckbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>("Renamer_AutoContinueCheckbox");
            autoContinueCheckbox.Visible = (b) => GlobalShowRenamePanel;
            autoContinueCheckbox.SupportsMultipleBlocks = true;
            autoContinueCheckbox.Title = MyStringId.GetOrCompute("Auto Continue Numbering");
            autoContinueCheckbox.Getter = (b) => AutoContinueNumbering;
            autoContinueCheckbox.Setter = (b, value) => AutoContinueNumbering = value;
            list.Add(autoContinueCheckbox);

            // Numeric Padding Separator Input Textbox
            var separatorTxt = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyTerminalBlock>("Renamer_NumberSeparatorTextbox");
            separatorTxt.Visible = (b) => GlobalShowRenamePanel;
            separatorTxt.SupportsMultipleBlocks = true;
            separatorTxt.Title = MyStringId.GetOrCompute("Number Separator (default: space)");
            separatorTxt.Getter = (b) =>
            {
                string value;
                if (b == null || !TempNumberSeparator.TryGetValue(b, out value)) value = " ";
                return new StringBuilder(value);
            };
            separatorTxt.Setter = (b, Builder) => { if (b != null) TempNumberSeparator[b] = Builder.ToString(); };
            list.Add(separatorTxt);

            // Group Categorization Filter Checkbox
            var groupCheckbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>("Renamer_GroupByTypeCheckbox");
            groupCheckbox.Visible = (b) => GlobalShowRenamePanel;
            groupCheckbox.SupportsMultipleBlocks = true;
            groupCheckbox.Title = MyStringId.GetOrCompute("Group by Block Type");
            groupCheckbox.Getter = (b) => GroupByBlockType;
            groupCheckbox.Setter = (b, value) =>
            {
                GroupByBlockType = value;
                if (value) BlockTypeCounters.Clear();
            };
            list.Add(groupCheckbox);

            // Reset Execution Counter Parameter Button
            var resetCounterBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_ResetCounterButton");
            resetCounterBtn.Visible = (b) => GlobalShowRenamePanel;
            resetCounterBtn.SupportsMultipleBlocks = true;
            resetCounterBtn.Title = MyStringId.GetOrCompute("Reset Counter");
            resetCounterBtn.Action = (b) =>
            {
                if (b == null || b.CubeGrid == null) return;
                long gridId = b.CubeGrid.EntityId;
                string format;
                if (!TempCounterFormat.TryGetValue(b, out format)) format = "001";

                string digitsOnly = new string(format.Where(char.IsDigit).ToArray());
                int startNumber = 0;
                if (!string.IsNullOrEmpty(digitsOnly)) int.TryParse(digitsOnly, out startNumber);

                GridNumberCounters[gridId] = startNumber;
                if (GroupByBlockType) BlockTypeCounters.Clear();
            };
            list.Add(resetCounterBtn);

            // Numeric Serialization Prefix Execution Button
            var numPrefixBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_NumberPrefixButton");
            numPrefixBtn.Visible = (b) => GlobalShowRenamePanel;
            numPrefixBtn.SupportsMultipleBlocks = true;
            numPrefixBtn.Title = MyStringId.GetOrCompute("Number Prefix");
            numPrefixBtn.Action = (b) => { if (b != null) ProcessNumbering(b, true); };
            list.Add(numPrefixBtn);

            // Numeric Serialization Suffix Execution Button
            var numBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_NumberSuffixButton");
            numBtn.Visible = (b) => GlobalShowRenamePanel;
            numBtn.SupportsMultipleBlocks = true;
            numBtn.Title = MyStringId.GetOrCompute("Number Suffix");
            numBtn.Action = (b) => { if (b != null) ProcessNumbering(b, false); };
            list.Add(numBtn);

            // Restore Block Default Definition Name Button
            var resetBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_ResetButton");
            resetBtn.Visible = (b) => GlobalShowRenamePanel;
            resetBtn.SupportsMultipleBlocks = true;
            resetBtn.Title = MyStringId.GetOrCompute("Reset to default");
            resetBtn.Action = (b) => { if (b != null) SendNetworkRequest(b, "RESET", ""); };
            list.Add(resetBtn);

            // Regular Expressions Sub-Panel UI Section Separator
            var regexSep = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyTerminalBlock>("Renamer_Label_RegexMid");
            regexSep.Visible = (b) => GlobalShowRenamePanel;
            regexSep.SupportsMultipleBlocks = true;
            regexSep.Label = MyStringId.GetOrCompute("------------------------------------------------------------------");
            list.Add(regexSep);

            // Regex String Pattern Lookup Textbox
            var findTxt = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyTerminalBlock>("RegRen_FindTextbox");
            findTxt.Visible = (b) => GlobalShowRenamePanel;
            findTxt.SupportsMultipleBlocks = true;
            findTxt.Title = MyStringId.GetOrCompute("Find (Pattern)");
            findTxt.Getter = (b) =>
            {
                string value;
                if (b == null || !TempStringFinds.TryGetValue(b, out value)) value = "";
                return new StringBuilder(value);
            };
            findTxt.Setter = (b, Builder) => { if (b != null) TempStringFinds[b] = Builder.ToString(); };
            list.Add(findTxt);

            // Regex Match Substitution Target Value Input Textbox
            var regexReplaceTxt = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyTerminalBlock>("RegRen_ReplaceWithTextbox");
            regexReplaceTxt.Visible = (b) => GlobalShowRenamePanel;
            regexReplaceTxt.SupportsMultipleBlocks = true;
            regexReplaceTxt.Title = MyStringId.GetOrCompute("Replace With");
            regexReplaceTxt.Getter = (b) =>
            {
                string value;
                if (b == null || !TempRegexReplace.TryGetValue(b, out value)) value = "";
                return new StringBuilder(value);
            };
            regexReplaceTxt.Setter = (b, Builder) => { if (b != null) TempRegexReplace[b] = Builder.ToString(); };
            list.Add(regexReplaceTxt);

            // Regular Expression Global Substitution Parse Button
            var regexBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("RegRen_ReplaceButton_v2");
            regexBtn.Visible = (b) => GlobalShowRenamePanel;
            regexBtn.SupportsMultipleBlocks = true;
            regexBtn.Title = MyStringId.GetOrCompute("Replace");
            regexBtn.Action = (b) =>
            {
                if (b == null) return;
                string findValue, replaceValue;
                if (!TempStringFinds.TryGetValue(b, out findValue)) findValue = "";
                if (!TempRegexReplace.TryGetValue(b, out replaceValue)) replaceValue = "";
                SendNetworkRequest(b, "REGEX", string.Format("{0}#{1}", findValue, replaceValue));
            };
            list.Add(regexBtn);

            // Bottom Separator
            var bottomSeparator = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyTerminalBlock>("Renamer_Label_Bottom");
            bottomSeparator.Visible = (b) => true;
            bottomSeparator.SupportsMultipleBlocks = true;
            bottomSeparator.Label = MyStringId.GetOrCompute("------------------------------------------------------------------");
            list.Add(bottomSeparator);

            return list;
        }

        // ─────────────────────────────────────────────────────────────
        //  UI Factory - Thruster Specific Extensions
        // ─────────────────────────────────────────────────────────────

        private List<IMyTerminalControl> CreateThrusterControlList()
        {
            var list = new List<IMyTerminalControl>();

            var thrusterSep = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyThrust>("Renamer_Label_ThrusterTop");
            thrusterSep.Visible = (b) => GlobalShowRenamePanel;
            thrusterSep.SupportsMultipleBlocks = true;
            thrusterSep.Label = MyStringId.GetOrCompute("------------------------------------------------------------------");
            list.Add(thrusterSep);

            var thrusterTxt = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyThrust>("TR_TemplateTextbox");
            thrusterTxt.Visible = (b) => GlobalShowRenamePanel;
            thrusterTxt.SupportsMultipleBlocks = true;
            thrusterTxt.Title = MyStringId.GetOrCompute("Template ({0} = direction)");
            thrusterTxt.Getter = (b) => new StringBuilder(GlobalThrusterTemplate);
            thrusterTxt.Setter = (b, Builder) => GlobalThrusterTemplate = Builder.ToString();
            list.Add(thrusterTxt);

            var btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyThrust>("TR_RenameButton");
            btn.Visible = (b) => GlobalShowRenamePanel;
            btn.SupportsMultipleBlocks = true;
            btn.Title = MyStringId.GetOrCompute("Rename by Direction");
            btn.Action = (b) => { if (b != null) SendNetworkRequest(b, "THRUST", GlobalThrusterTemplate); };
            list.Add(btn);

            return list;
        }

        // ─────────────────────────────────────────────────────────────
        //  Numbering Core Logic Implementation
        // ─────────────────────────────────────────────────────────────

        private void ProcessNumbering(IMyTerminalBlock block, bool isPrefix)
        {
            if (block == null || block.CubeGrid == null) return;
            long gridId = block.CubeGrid.EntityId;

            string format;
            if (!TempCounterFormat.TryGetValue(block, out format)) format = "01";

            string separator;
            if (!TempNumberSeparator.TryGetValue(block, out separator)) separator = " ";

            if (!AutoContinueNumbering)
            {
                string digitsOnly = new string(format.Where(char.IsDigit).ToArray());
                int startNumber = 0;
                if (!string.IsNullOrEmpty(digitsOnly)) int.TryParse(digitsOnly, out startNumber);

                if (GroupByBlockType && block.DefinitionDisplayNameText != null)
                    BlockTypeCounters[block.DefinitionDisplayNameText] = startNumber;
                else
                    GridNumberCounters[gridId] = startNumber;
            }

            int currentNumber;
            if (GroupByBlockType)
            {
                string blockType = block.DefinitionDisplayNameText ?? "UnknownBlockType";
                if (!BlockTypeCounters.ContainsKey(blockType))
                {
                    string digitsOnly = new string(format.Where(char.IsDigit).ToArray());
                    int startNumber = 0;
                    if (!string.IsNullOrEmpty(digitsOnly)) int.TryParse(digitsOnly, out startNumber);
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
                    if (!string.IsNullOrEmpty(digitsOnly)) int.TryParse(digitsOnly, out startNumber);
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
        //  Network Multi-Player Management Sync Hub
        // ─────────────────────────────────────────────────────────────

        private void SendNetworkRequest(IMyTerminalBlock block, string action, string value)
        {
            ApplyAction(block, action, value);
        }

        private void OnMessageReceived(ushort id, byte[] data, ulong sender, bool fromServer)
        {
            if (MyAPIGateway.Session == null || !MyAPIGateway.Session.IsServer) return;
            try
            {
                var message = Encoding.UTF8.GetString(data);
                var parts = message.Split('|');
                if (parts.Length < 3) return;

                long entityId;
                if (!long.TryParse(parts[0], out entityId)) return;

                var block = MyAPIGateway.Entities.GetEntityById(entityId) as IMyTerminalBlock;
                if (block == null) return;

                ApplyAction(block, parts[1], parts[2]);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine("BlockRenamer: Error - " + ex.Message);
            }
        }

        private void ApplyAction(IMyTerminalBlock block, string action, string value)
        {
            if (block == null) return;
            switch (action)
            {
                case "REPLACE": block.CustomName = value; break;
                case "PREFIX": block.CustomName = value + block.CustomName; break;
                case "SUFFIX": block.CustomName = block.CustomName + value; break;
                case "RESET": block.CustomName = block.DefinitionDisplayNameText; break;
                case "NUMPREFIX": block.CustomName = value + block.CustomName; break;
                case "NUM": block.CustomName += value; break;
                case "THRUST": ApplyThrusterRename(block as IMyThrust, value); break;
                case "REGEX":
                    var regexParts = value.Split('#');
                    if (regexParts.Length == 2)
                    {
                        try { block.CustomName = Regex.Replace(block.CustomName, regexParts[0], regexParts[1]); }
                        catch { }
                    }
                    break;
            }
        }

        private void ApplyThrusterRename(IMyThrust thruster, string template)
        {
            if (thruster == null) return;
            var direction = thruster.GridThrustDirection;
            string letter = "X";

            if (direction == Vector3I.Forward) letter = "B";
            else if (direction == Vector3I.Backward) letter = "F";
            else if (direction == Vector3I.Up) letter = "D";
            else if (direction == Vector3I.Down) letter = "U";
            else if (direction == Vector3I.Left) letter = "R";
            else if (direction == Vector3I.Right) letter = "L";

            thruster.CustomName = template.Contains("{0}") ? template.Replace("{0}", letter) : template + " " + letter;
        }
    }
}