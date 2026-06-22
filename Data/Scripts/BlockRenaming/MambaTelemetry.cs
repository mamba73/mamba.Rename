// /Data/Scripts/BlockRenaming/MambaTelemetry.cs
// MAMBA MambaTelemetry/MODULE_TYPE

using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.ModAPI;

namespace BlockRenaming
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class MambaTelemetry : MySessionComponentBase
    {
        private bool _isInitialized = false;
        private static long _lastEvaluatedBlockId = 0;

        public override void LoadData()
        {
            // Dynamically links to the public constant inside the core class to prevent hardcoding
            MambaLogger.Init(BlockRenamerCore.MOD_VERSION + "-DEBUG", true);
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
            MyAPIGateway.TerminalControls.CustomControlGetter += Diagnostics_ControlGetter;
            
            MambaLogger.Info("[TELEMETRY] Private debugging system initialized. Dynamic compilation link verified.");
        }

        protected override void UnloadData()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= Diagnostics_ControlGetter;
            MambaLogger.Close();
        }

        private void Diagnostics_ControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block == null) return;

            if (block.EntityId != _lastEvaluatedBlockId)
            {
                _lastEvaluatedBlockId = block.EntityId;

                string customName = block.CustomName ?? "Unnamed";
                string typeName = block.GetType().Name;
                string subtypeId = "Unknown";
                
                try
                {
                    if (block.BlockDefinition.SubtypeId != null)
                        subtypeId = block.BlockDefinition.SubtypeId;
                }
                catch { }

                int controlCount = controls != null ? controls.Count : 0;

                MambaLogger.Info(string.Format("[TELEMETRY-BLOCK] Focused: '{0}' | Type: {1} | Subtype: {2} | ID: {3} | Total Controls: {4}",
                    customName, typeName, subtypeId, block.EntityId, controlCount));

                if (controls != null && controls.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("[TELEMETRY-CONTROLS] Injected IDs: ");
                    foreach (var ctrl in controls)
                    {
                        if (ctrl != null && !string.IsNullOrEmpty(ctrl.Id))
                        {
                            sb.Append(ctrl.Id).Append(", ");
                        }
                    }
                    MambaLogger.Debug(sb.ToString().TrimEnd(',', ' '));
                }
            }
        }
    }
}