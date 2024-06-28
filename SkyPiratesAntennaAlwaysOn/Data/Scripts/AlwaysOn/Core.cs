using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;


namespace AntennaAlwaysOn
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RadioAntenna), true)]
    public class Core : MyGameLogicComponent
    {
        public const float rangeSmallGrid = 1000f;
        public const float rangeLargeGrid = 3000f;

        private IMyRadioAntenna beacon;


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            beacon = Entity as IMyRadioAntenna;
            if (beacon.Radius < rangeSmallGrid)
                beacon.Radius = rangeSmallGrid;

            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        /// <summary>
        /// Avoid crashes when block is removed by stopping any posible action
        /// </summary>
        public override void Close()
        {
            NeedsUpdate = MyEntityUpdateEnum.NONE;
        }

        /// <summary>
        /// sets range minimums
        /// </summary>
        public override void UpdateBeforeSimulation()
        {
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                List<IMyTerminalControl> controls;
                MyAPIGateway.TerminalControls.GetControls<IMyRadioAntenna>(out controls);

                foreach (IMyTerminalControl control in controls)
                {
                    if (control.Id == "Radius")
                    {
                        if (beacon.CubeGrid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
                        {
                            ((IMyTerminalControlSlider)control).SetLimits(rangeSmallGrid, 50000f);
                        }
                        else if (beacon.CubeGrid.GridSizeEnum == VRage.Game.MyCubeSize.Large)
                        {
                            ((IMyTerminalControlSlider)control).SetLimits(rangeLargeGrid, 50000f);
                        }

                        // stop running this logic but keep the always on check.
                        NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// forces the beacon to always be on
        /// </summary>
        public override void UpdateBeforeSimulation100()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                beacon.Enabled = true;
            }
        }
    }
}
