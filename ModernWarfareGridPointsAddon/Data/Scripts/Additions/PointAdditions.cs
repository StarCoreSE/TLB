using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;

namespace ShipPoints
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    internal class PointAdditions : MySessionComponentBase
    {
        private readonly Dictionary<string, int> PointValues = new Dictionary<string, int>
        {
            // to show up on the HUD, it needs to be listed here
            ["SmallBlockBatteryBlock"] = 0,
            ["TinyDieselEngine"] = 0,
            ["SmallDieselEngine"] = 0,
            ["MediumDieselEngine"] = 0,

            // Primary weapons
            ["PolybolosGunBlock"] = 2,
            ["CulverinRocketPod"] = 2,
            ["TrebuchetGunBlock"] = 4,
            ["OnagerGunBlock"] = 5,
            ["ArbalestGunBlock"] = 7,
            ["BallistaGunBlock"] = 7,
            ["HwachaGunBlock"] = 7,
            ["ScorpionGunBlock"] = 9,
            ["CatapultGunBlock"] = 10,

            // Secondary weapons (all are turrets; may only have one)
            ["DaggerLauncher"] = 1,
            ["TorchLauncher"] = 2,
            ["CutlassLauncher"] = 2,
            ["ShieldTurret"] = 3,
            ["SwordLauncher"] = 4,
            ["MaceLauncher"] = 4,
            ["SpearLauncher"] = 5,

            // Ordnance
            ["SmallWarhead"] = 1,
            ["MountedMediumBomb"] = 1,
            ["SmallBombBay"] = 3,
            ["MediumBombBay"] = 4,
            ["MountedLargeBomb"] = 1,
            ["MountedMissile"] = 5,
            ["BombardLauncher"] = 10,
            ["BasiliskGunBlock"] = 10,

            // Utilities
            ["Camera"] = 1,
            ["TLBSmallFlareLauncher"] = 2,
            ["TLBSmallSmokeLauncher"] = 3,
            ["APSTurret"] = 4,
            ["DronePMW"] = 5,
            ["SurvivalKit"] = 5,

            // Propulsion 
            ["3xPlaneWheels"] = 1,
            ["8x2x2Wheels"] = 1,
            ["4x3x3Wheels"] = 1,
            ["2x5x5Wheels"] = 1,
            ["2xWing"] = 1,
            ["4xControlSurface"] = 1,
            ["Gyro"] = 1,
            ["JetThrusterIntakeCombo"] = 2,
            ["HelicopterRotor"] = 3,
            ["HelicopterTailRotor"] = 1,
            ["X4X5"] = 1,
            ["2xElectricFans"] = 1,
        };

        private readonly Dictionary<string, int> FuzzyPoints = new Dictionary<string, int>();
        private readonly Func<string, MyTuple<string, float>> _climbingCostRename = ClimbingCostRename;

        private static MyTuple<string, float> ClimbingCostRename(string blockSubtypeName)
        {
            float costMultiplier = 0;

            switch (blockSubtypeName)
            {
                case "TestEntry1":
                    costMultiplier = 0f;
                    break;
            }

            return new MyTuple<string, float>(blockSubtypeName, costMultiplier);
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            // Add fuzzy rules
            FuzzyPoints.Add("aero-wing", 1);


            // Process fuzzy rules
            foreach (var kvp in FuzzyPoints)
            {
                foreach (var block in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    var cubeBlock = block as MyCubeBlockDefinition;
                    if (cubeBlock != null && cubeBlock.Id.SubtypeName.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!PointValues.ContainsKey(cubeBlock.Id.SubtypeName))
                        {
                            PointValues[cubeBlock.Id.SubtypeName] = kvp.Value;
                        }
                    }
                }
            }

            MyAPIGateway.Utilities.SendModMessage(2546247, PointValues);
            MyAPIGateway.Utilities.SendModMessage(2546247, _climbingCostRename);
        }
    }
}