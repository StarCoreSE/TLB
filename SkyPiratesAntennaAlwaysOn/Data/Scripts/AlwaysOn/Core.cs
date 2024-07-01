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

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            beacon = Entity as IMyRadioAntenna;
            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        /// <summary>
        /// forces the beacon to always be on
        /// </summary>
        public override void UpdateBeforeSimulation100()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (!beacon.Enabled) 
                {
                    beacon.Enabled = true;
                }

                if (!beacon.EnableBroadcasting) 
                {
                    beacon.EnableBroadcasting = true;
                }

                if (beacon.CubeGrid.GridSizeEnum == VRage.Game.MyCubeSize.Large)
                {
                    if (beacon.Radius < rangeSmallGrid) 
                    {
                        beacon.Radius = rangeSmallGrid;
                    }
                }
                else if (beacon.CubeGrid.GridSizeEnum == VRage.Game.MyCubeSize.Large)
                {
                    if (beacon.Radius < rangeLargeGrid)
                    {
                        beacon.Radius = rangeLargeGrid;
                    }
                }

            }
        }
    }
}
