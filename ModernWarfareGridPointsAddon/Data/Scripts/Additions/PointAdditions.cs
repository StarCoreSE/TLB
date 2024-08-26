using System;
using System.Collections.Generic;
using System.Xml;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;

namespace ShipPoints
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    internal class PointAdditions : MySessionComponentBase
    {
        readonly Dictionary<string, int> PointValues = new Dictionary<string, int>
        {
            ["SmallBlockBatteryBlock"] = 14,

        };

        private readonly Func<string, MyTuple<string, float>> _climbingCostRename = ClimbingCostRename;

        private static MyTuple<string, float> ClimbingCostRename(string blockDisplayName)
        {
            float costMultiplier = 0;

            switch (blockDisplayName)
            {
                case "TesetEntry1":
                    blockDisplayName = "Test Entry 1";
                    costMultiplier = 0f;
                    break;

            }

            return new MyTuple<string, float>(blockDisplayName, costMultiplier);
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyAPIGateway.Utilities.SendModMessage(2546247, PointValues);
            MyAPIGateway.Utilities.SendModMessage(2546247, _climbingCostRename);
        }
    }
}
