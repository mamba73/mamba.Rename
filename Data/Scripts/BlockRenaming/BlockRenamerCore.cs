// Data/Scripts/BlockRenaming/BlockRenamerCore.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using SpaceEngineers.Game.ModAPI;

namespace BlockRenaming
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class BlockRenamerCore : MySessionComponentBase
    {
        private const string MOD_VERSION = "1.5.5";
        public const ushort NETWORK_ID = 58432;

        private bool _isInitialized = false;
        
        // Temporary storage for UI inputs (Client-side only)
        private readonly Dictionary<IMyTerminalBlock, string> TempStringFinds = new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempStringRenames = new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempRegexReplace = new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempCounterFormat = new Dictionary<IMyTerminalBlock, string>();
        private readonly Dictionary<IMyTerminalBlock, string> TempNumberSeparator = new Dictionary<IMyTerminalBlock, string>();
        
        // State variables (Server-side and Client-side)
        private static readonly Dictionary<long, int> GridNumberCounters = new Dictionary<long, int>();
        
        // Per-block-type counters for grouped numbering
        private static readonly Dictionary<string, int> BlockTypeCounters = new Dictionary<string, int>();
        
        // Group by block type toggle
        private static bool GroupByBlockType = false;
        
        private static string GlobalThrusterTemplate = "Thruster {0}";

        // NEW FEATURE: Auto-continue numbering toggle
        private static bool AutoContinueNumbering = true; // Default ON
        
        // Control lists
        private List<IMyTerminalControl> ControlsListMain = null;
        private List<IMyTerminalControl> ControlsListThrusters = null;

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
            
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                _isInitialized = true;
                return;
            }

            _isInitialized = true;
            
            ControlsListMain = CreateControlList();
            ControlsListThrusters = CreateThrusterControlList();
            
            MyAPIGateway.TerminalControls.CustomControlGetter += AddControlsToBlocks;
            
            MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                this.SetUpdateOrder(MyUpdateOrder.NoUpdate);
            });
        }

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

        private List<IMyTerminalControl> CreateControlList()
        {
            var list = new List<IMyTerminalControl>();

            // Main separator
            var separator0 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyTerminalBlock>("Renamer_MainSeparator");
            separator0.Enabled = (b) => { return true; };
            separator0.SupportsMultipleBlocks = true;
            list.Add(separator0);

            // Version label
            var label0 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyTerminalBlock>("Renamer_Label");
            label0.Enabled = (b) => { return true; };
            label0.SupportsMultipleBlocks = true;
            label0.Label = MyStringId.GetOrCompute(string.Format("Block Renaming Controls v{0}", MOD_VERSION));
            list.Add(label0);

            // New naming textbox
            var textbox0 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyTerminalBlock>("Renamer_Textbox");
            textbox0.Enabled = (b) => { return true; };
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
            var replaceBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_RenameButton");
            replaceBtn.Enabled = (b) => { return true; };
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
            var prefixBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_PrefixButton");
            prefixBtn.Enabled = (b) => { return true; };
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
            var suffixBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_SuffixButton");
            suffixBtn.Enabled = (b) => { return true; };
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
            var counterTxt = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyTerminalBlock>("Renamer_CounterFormatTextbox");
            counterTxt.Enabled = (b) => { return true; };
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

            // NEW FEATURE: Current counter status label
            var counterStatusLabel = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyTerminalBlock>("Renamer_CounterStatusLabel");
            counterStatusLabel.Enabled = (b) => { 
                counterStatusLabel.Label = MyStringId.GetOrCompute(GetCurrentCounterStatus(b));
                return true; 
            };
            counterStatusLabel.SupportsMultipleBlocks = false;
            list.Add(counterStatusLabel);

            // NEW FEATURE: Auto-continue checkbox
            var autoContinueCheckbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>("Renamer_AutoContinueCheckbox");
            autoContinueCheckbox.Enabled = (b) => { return true; };
            autoContinueCheckbox.SupportsMultipleBlocks = true;
            autoContinueCheckbox.Title = MyStringId.GetOrCompute("Auto Continue Numbering");
            autoContinueCheckbox.Tooltip = MyStringId.GetOrCompute("Continue from last number instead of resetting to format");
            autoContinueCheckbox.Getter = (b) => AutoContinueNumbering;
            autoContinueCheckbox.Setter = (b, value) => AutoContinueNumbering = value;
            list.Add(autoContinueCheckbox);            

            // NEW FEATURE: Number separator textbox
            var separatorTxt = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyTerminalBlock>("Renamer_NumberSeparatorTextbox");
            separatorTxt.Enabled = (b) => { return true; };
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
                string newValue = Builder.ToString();
                if (string.IsNullOrEmpty(newValue))
                    newValue = "";
                TempNumberSeparator[b] = newValue;
            };
            list.Add(separatorTxt);

            // NEW FEATURE: Group by Block Type checkbox
            var groupCheckbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>("Renamer_GroupByTypeCheckbox");
            groupCheckbox.Enabled = (b) => { return true; };
            groupCheckbox.SupportsMultipleBlocks = true;
            groupCheckbox.Title = MyStringId.GetOrCompute("Group by Block Type");
            groupCheckbox.Tooltip = MyStringId.GetOrCompute("Auto-reset counter for each block type");
            groupCheckbox.Getter = (b) => GroupByBlockType;
            groupCheckbox.Setter = (b, value) =>
            {
                GroupByBlockType = value;
                if (value)
                    BlockTypeCounters.Clear();
            };
            list.Add(groupCheckbox);

            // Reset Counter button
            var resetCounterBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_ResetCounterButton");
            resetCounterBtn.Enabled = (b) => { return true; };
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
                {
                    int.TryParse(digitsOnly, out startNumber);
                }
                
                GridNumberCounters[gridId] = startNumber;
                if (GroupByBlockType)
                    BlockTypeCounters.Clear();
            };
            list.Add(resetCounterBtn);

            // Number Prefix button
            var numPrefixBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_NumberPrefixButton");
            numPrefixBtn.Enabled = (b) => { return true; };
            numPrefixBtn.SupportsMultipleBlocks = true;
            numPrefixBtn.Title = MyStringId.GetOrCompute("Number Prefix");
            numPrefixBtn.Action = (b) =>
            {
                ProcessNumbering(b, true);
            };
            list.Add(numPrefixBtn);

            // Number suffix button
            var numBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_NumberSuffixButton");
            numBtn.Enabled = (b) => { return true; };
            numBtn.SupportsMultipleBlocks = true;
            numBtn.Title = MyStringId.GetOrCompute("Number Suffix");
            numBtn.Action = (b) =>
            {
                ProcessNumbering(b, false);
            };
            list.Add(numBtn);

            // Reset button
            var resetBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("Renamer_ResetButton");
            resetBtn.Enabled = (b) => { return true; };
            resetBtn.SupportsMultipleBlocks = true;
            resetBtn.Title = MyStringId.GetOrCompute("Reset to default");
            resetBtn.Action = (b) => SendNetworkRequest(b, "RESET", "");
            list.Add(resetBtn);

            // Regex separator
            var regexSep = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyTerminalBlock>("RegRen_Separator");
            regexSep.Enabled = (b) => { return true; };
            regexSep.SupportsMultipleBlocks = true;
            list.Add(regexSep);
            
            // Regex find textbox
            var findTxt = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyTerminalBlock>("RegRen_FindTextbox");
            findTxt.Enabled = (b) => { return true; };
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

            // Regex Replace With textbox
            var regexReplaceTxt = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyTerminalBlock>("RegRen_ReplaceWithTextbox");
            regexReplaceTxt.Enabled = (b) => { return true; };
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
            var regexBtn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("RegRen_ReplaceButton_v2");
            regexBtn.Enabled = (b) => { return true; };
            regexBtn.SupportsMultipleBlocks = true;
            regexBtn.Title = MyStringId.GetOrCompute("Replace");
            regexBtn.Action = (b) =>
            {
                string findValue;
                string replaceValue;
                if (!TempStringFinds.TryGetValue(b, out findValue)) findValue = "";
                if (!TempRegexReplace.TryGetValue(b, out replaceValue)) replaceValue = "";
                
                SendNetworkRequest(b, "REGEX", string.Format("{0}#{1}", findValue, replaceValue));
            };
            list.Add(regexBtn);

            return list;
        }

        private List<IMyTerminalControl> CreateThrusterControlList()
        {
            var list = new List<IMyTerminalControl>();
            
            var thrusterSep = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyThrust>("TR_Separator");
            thrusterSep.Enabled = (b) => { return true; };
            thrusterSep.SupportsMultipleBlocks = true;
            list.Add(thrusterSep);
            
            var thrusterTxt = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyThrust>("TR_TemplateTextbox");
            thrusterTxt.Enabled = (b) => { return true; };
            thrusterTxt.SupportsMultipleBlocks = true;
            thrusterTxt.Title = MyStringId.GetOrCompute("Template ({0} = direction)");
            thrusterTxt.Getter = (b) => new StringBuilder(GlobalThrusterTemplate);
            thrusterTxt.Setter = (b, Builder) => GlobalThrusterTemplate = Builder.ToString();
            list.Add(thrusterTxt);

            var btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyThrust>("TR_RenameButton");
            btn.Enabled = (b) => { return true; };
            btn.SupportsMultipleBlocks = true;
            btn.Title = MyStringId.GetOrCompute("Rename by Direction");
            btn.Action = (b) => SendNetworkRequest(b, "THRUST", GlobalThrusterTemplate);
            list.Add(btn);
            
            return list;
        }

        private void ProcessNumbering(IMyTerminalBlock block, bool isPrefix)
        {
            // FIX: Declarations at the top to fix compiler errors
            long gridId = block.CubeGrid.EntityId;
            string format;
            if (!TempCounterFormat.TryGetValue(block, out format))
                format = "01";
            
            string separator;
            if (!TempNumberSeparator.TryGetValue(block, out separator) || string.IsNullOrEmpty(separator))
                separator = " ";

            if (!AutoContinueNumbering)
            {
                string digitsOnly = new string(format.Where(char.IsDigit).ToArray());
                int startNumber = 0;
                if (!string.IsNullOrEmpty(digitsOnly))
                {
                    int.TryParse(digitsOnly, out startNumber);
                }
                
                if (GroupByBlockType)
                {
                    string blockType = block.DefinitionDisplayNameText;
                    BlockTypeCounters[blockType] = startNumber;
                }
                else
                {
                    GridNumberCounters[gridId] = startNumber;
                }
            }
            
            int currentNumber;
            if (GroupByBlockType)
            {
                string blockType = block.DefinitionDisplayNameText;
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
            string formattedNumber = currentNumber.ToString("D" + digitCount);
            
            if (isPrefix)
                SendNetworkRequest(block, "NUMPREFIX", formattedNumber + separator);
            else
                SendNetworkRequest(block, "NUM", separator + formattedNumber);
        }

        private void SendNetworkRequest(IMyTerminalBlock block, string action, string value)
        {
            var data = string.Format("{0}|{1}|{2}", block.EntityId, action, value);
            MyAPIGateway.Multiplayer.SendMessageToServer(NETWORK_ID, Encoding.UTF8.GetBytes(data));
        }

        private void OnMessageReceived(ushort id, byte[] data, ulong sender, bool fromServer)
        {
            if (!MyAPIGateway.Session.IsServer) return;
            try
            {
                var message = Encoding.UTF8.GetString(data);
                var parts = message.Split('|');
                if (parts.Length < 3) return;
                
                long entityId;
                if (!long.TryParse(parts[0], out entityId)) return;
                var block = MyAPIGateway.Entities.GetEntityById(entityId) as IMyTerminalBlock;
                if (block == null) return;
                
                string action = parts[1];
                string value = parts[2];
                
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
            catch (Exception ex) { MyLog.Default.WriteLineAndConsole("Renamer Error: " + ex.Message); }
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

        private string GetCurrentCounterStatus(IMyTerminalBlock block)
        {
            if (block == null || block.CubeGrid == null) return "Counter: N/A";
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
