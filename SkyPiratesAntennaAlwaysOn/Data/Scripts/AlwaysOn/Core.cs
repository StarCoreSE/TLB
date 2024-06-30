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
        public const float rangeLargeGrid = 100f;

        private IMyRadioAntenna beacon;
        private bool waitframe = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (MyAPIGateway.Utilities.IsDedicated) return;

                beacon = Entity as IMyRadioAntenna;
            if (beacon.Radius < rangeSmallGrid)
                beacon.Radius = rangeSmallGrid;

            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME | MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }


        public override void UpdateOnceBeforeFrame()
        {
            if (waitframe)
            {
                waitframe = false;
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                return;
            }

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

                    break;
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
