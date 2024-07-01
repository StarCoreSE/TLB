using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using System.Collections.Generic;
using VRage.Game.Entity;
using Sandbox.Definitions;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using System.Text;
using System.Linq;
using VRage.Utils;

namespace SKY_PIRATES_CORE
{
    public class ControlledEntityHydrogenCapacity : IMyHudStat
    {
        public const string NumberFormat = "###,###,###,###,###,###,##0";

        public MyStringHash Id { get; private set; }
        public float MinValue { get; } = 0f;
        public float MaxValue { get; } = 100f;
        public string GetValueString() { return CurrentValue.ToString(); }// must never be null

        private float _currentValue = 0f;
        public float CurrentValue
        {
            get { return _currentValue; }
            set
            {
                if (_currentValue != value)
                {
                    _currentValue = value;
                }
            }
        }

        public ControlledEntityHydrogenCapacity()
        {
            Id = MyStringHash.GetOrCompute("controlled_hydrogen_capacity");
        }

        public void Update()
        {
            if (MyAPIGateway.Session?.ControlledObject == null)
            {
                CurrentValue = 0f;
                return;
            }
            IMyTerminalBlock controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
            if (controlled != null)
            {
                IMyCubeGrid grid = controlled.CubeGrid;
                PropellerGrid plane;
                if(PropellerSession.instance.grids.TryGetValue(grid.EntityId, out plane))
                {
                    CurrentValue = (float)(int)(100f - (plane.production - plane.thrust) / plane.production * 100f);
                    if (plane.production < 1)
                        CurrentValue = 0;
                }
            }
        }
    }

    public class ControlledEntityFuelTime : IMyHudStat
    {
        public const string NumberFormat = "###,###,###,###,###,###,##0";

        public MyStringHash Id { get; private set; }
        public float MinValue { get; } = 0f;
        public float MaxValue { get; } = 1000f;
        public string GetValueString()
        {
            if (CurrentValue < 0)
                return "Fuel Time: N/A";

            return $"Fuel Time: {CurrentValue} min"; 
        }// must never be null

        private float _currentValue = 0f;
        public float CurrentValue
        {
            get { return _currentValue; }
            set
            {
                if (_currentValue != value)
                {
                    _currentValue = value;
                }
            }
        }

        public ControlledEntityFuelTime()
        {
            Id = MyStringHash.GetOrCompute("controlled_estimated_time_remaining_energy");
        }

        public void Update()
        {
            if (MyAPIGateway.Session?.ControlledObject == null)
            {
                CurrentValue = 0f;
                return;
            }
            IMyTerminalBlock controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
            if (controlled != null)
            {
                IMyCubeGrid grid = controlled.CubeGrid;
                PropellerGrid plane;
                if (PropellerSession.instance.grids.TryGetValue(grid.EntityId, out plane))
                {
                    CurrentValue = (float)(int)(plane.totalFuel / plane.consumption / 60);
                    if (plane.totalFuel < 1)
                        CurrentValue = 0;

                    if (plane.consumption < 1)
                        CurrentValue = -1f;
                }
            }
        }
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class PropellerSession : MySessionComponentBase
    {
        private const int updateTicks = 100;
        private int tick = 0;

        public static PropellerSession instance;
        public Dictionary<long, PropellerGrid> grids = new Dictionary<long, PropellerGrid>();

        public override void LoadData()
        {
            MyAPIGateway.Entities.OnEntityAdd += HandleEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove += HandleEntityRemoved;

            instance = this;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= HandleEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove -= HandleEntityRemoved;

            grids.Clear();
            instance = null;
        }

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            if (tick % updateTicks == 0)
            {
                MyAPIGateway.Parallel.ForEach(grids.Values.ToList(), plane =>
                {
                    plane.UpdateThrustAndProduction();
                    plane.UpdateFuel();
                });
                tick = 0;
            }

            tick++;
        }

        private void HandleEntityAdded(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;

            IMyCubeGrid grid = entity as IMyCubeGrid;
            grid.OnBlockAdded += HandleBlockAdded;
            grid.OnBlockRemoved += HandleBlockRemoved;

            PropellerGrid plane = new PropellerGrid(grid);

            foreach (IMyGasGenerator block in grid.GetFatBlocks<IMyGasGenerator>())
            {
                plane.engis.Add(block);
            }

            foreach (IMyThrust block in grid.GetFatBlocks<IMyThrust>())
            {
                plane.props.Add(block);
            }

            foreach (IMyCargoContainer block in grid.GetFatBlocks<IMyCargoContainer>())
            {
                plane.cargs.Add(block);
            }

            plane.UpdateThrustAndProduction();
            plane.UpdateFuel();

            if(!grids.ContainsKey(entity.EntityId))
                grids.Add(entity.EntityId, plane);
        }

        private void HandleEntityRemoved(IMyEntity entity)
        {
            if (!grids.ContainsKey(entity.EntityId))
                return;

            grids.Remove(entity.EntityId);
            IMyCubeGrid grid = entity as IMyCubeGrid;
            grid.OnBlockAdded -= HandleBlockAdded;
            grid.OnBlockRemoved -= HandleBlockRemoved;
        }

        private void HandleBlockAdded(IMySlimBlock slim)
        {
            if (slim.FatBlock == null)
                return;

            IMyCubeGrid grid = slim.CubeGrid;

            PropellerGrid plane;
            grids.TryGetValue(grid.EntityId, out plane);
            if (plane == null)
                return;


            if(slim.FatBlock is IMyGasGenerator)
            {
                plane.engis.Add(slim.FatBlock as IMyGasGenerator);
                plane.cargs.Add(slim.FatBlock as IMyCubeBlock);
                plane.UpdateThrustAndProduction();
                grids[grid.EntityId] = plane;
            }
            else if(slim.FatBlock is IMyThrust)
            {
                plane.props.Add(slim.FatBlock as IMyThrust);
                plane.UpdateThrustAndProduction();
                grids[grid.EntityId] = plane;
            }
            else if (slim.FatBlock is IMyCargoContainer)
            {
                plane.cargs.Add(slim.FatBlock as IMyCubeBlock);
                grids[grid.EntityId] = plane;
            }
        }

        private void HandleBlockRemoved(IMySlimBlock slim)
        {
            if (slim.FatBlock == null)
                return;

            IMyCubeGrid grid = slim.CubeGrid;

            PropellerGrid plane;
            grids.TryGetValue(grid.EntityId, out plane);
            if (plane == null)
                return;

            if (slim.FatBlock is IMyGasGenerator)
            {
                plane.engis.Remove(slim.FatBlock as IMyGasGenerator);
                plane.cargs.Remove(slim.FatBlock as IMyCubeBlock);
                plane.UpdateFuel();
                plane.UpdateThrustAndProduction();
                grids[grid.EntityId] = plane;
            }
            else if (slim.FatBlock is IMyThrust)
            {
                plane.props.Remove(slim.FatBlock as IMyThrust);
                plane.UpdateThrustAndProduction();
                grids[grid.EntityId] = plane;
            }
            else if (slim.FatBlock is IMyCargoContainer)
            {
                plane.cargs.Remove(slim.FatBlock as IMyCubeBlock);
                plane.UpdateFuel();
                grids[grid.EntityId] = plane;
            }
        }
    }

    [MyEntityComponentDescriptor(
        typeof(MyObjectBuilder_Thrust),
        false,
        "ZeppelinPropeller",
        "ZeppelinPropellerSmall",
        "ZeppelinPropellerLarge",
        "Prop2B",
        "Prop3B",
        "Prop4B"
    )]
    public class PropellerGameLogic : MyGameLogicComponent
    {
        const string PIRATE_TAG = "SPRT";

        Vector3 thrustVector;
        Vector3 gravVector;

        float throttle;
        float thrust;
        float efficiency;
        float altitude;
        float speed;
        float damage;

        bool isNPC = false;

        // MODAPI
        IMyThrust thruster;
        IMyFunctionalBlock block;
        IMyCubeGrid grid;
        MyThrustDefinition def;
        MyPlanet planet;

        // upgrade values
        float intake = 0f;
        float stormdynamo = 0f;
        float turbocharger = 0f;
        float supercharger = 0f;
        float enginesensor = 0f;
        float overclocker = 0f;
        float fuelinjector = 0f;
        float carburetor = 0f;
        float nosinjector = 0f;

        float simpleUpgradeThrustEffects = 1f;
        float simpleUpgradePowerEffects = 1f;

        List<string> interferenceProps = new List<string>();

        float compressorEffects = 1f;
        float velocityEffects = 1f;
        float altitudeEffects = 1f;
        float stallEffects = 1f;
        float interferenceEffects = 1f;
        float stormEffects = 1f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            grid = (Entity as IMyTerminalBlock).CubeGrid;
            thruster = (Entity as IMyThrust);
            block = (IMyFunctionalBlock)Entity;
            def = ((Entity as MyCubeBlock).BlockDefinition) as MyThrustDefinition;

            thruster.AddUpgradeValue("intake", 0f);
            thruster.AddUpgradeValue("stormdynamo", 0f);
            thruster.AddUpgradeValue("turbocharger", 0f);
            thruster.AddUpgradeValue("supercharger", 0f);
            thruster.AddUpgradeValue("enginesensor", 0f);
            thruster.AddUpgradeValue("overclocker", 0f);
            thruster.AddUpgradeValue("fuelinjector", 0f);
            thruster.AddUpgradeValue("carburetor", 0f);
            thruster.AddUpgradeValue("nosinjector", 0f);

            thruster.OnUpgradeValuesChanged += OnUpgradeValuesChangedUpgrade;
            (Entity as IMyTerminalBlock).AppendingCustomInfo += OnWriteToTerminal;

            if (grid.GridSizeEnum == MyCubeSize.Large)
                damage = 30f;
            else
                damage = 300f;

            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void Close()
        {
            thruster.OnUpgradeValuesChanged -= OnUpgradeValuesChangedUpgrade;
            (Entity as IMyTerminalBlock).AppendingCustomInfo -= OnWriteToTerminal;
        }

        public void OnWriteToTerminal(IMyTerminalBlock terminalBlock, StringBuilder stringBuilder)
        {
            try
            {
                MyThrustDefinition thrustInternal = block.SlimBlock.BlockDefinition as MyThrustDefinition;
                float currentPowerUsage = thrustInternal.MinPowerConsumption + ((thrustInternal.MaxPowerConsumption - thrustInternal.MinPowerConsumption) * (thruster.CurrentThrust / thruster.MaxThrust));
                float currentFuelUsage = currentPowerUsage * thruster.PowerConsumptionMultiplier / (0.001f * thrustInternal.FuelConverter.Efficiency);

                stringBuilder.Clear(); // fuck you, keen
                stringBuilder.Append(
                    $"Note: Max Input value above is incorrect...\n" +
                    $"Current Efficiency: {(thruster.CurrentThrust / currentFuelUsage / 1000):0.##} kNs/L\n" +
                    $"Current Thrust: {(int)(thruster.CurrentThrust / 1000)} kN\n" +
                    $"Current H2 Input: {(int)currentFuelUsage} L/s\n" +
                    $"Total Power Multiplier: {thruster.PowerConsumptionMultiplier:0.##}\n" +
                    $"Total Thrust Multiplier: {(thruster.ThrustMultiplier * planet.GetAirDensity(grid.WorldMatrix.Translation)):0.##}\n"
                    );
                stringBuilder.Append($"Altitude Modifier: {(altitudeEffects * planet.GetAirDensity(grid.WorldMatrix.Translation)):0.##}\n");
                stringBuilder.Append($"Velocity Modifier: {velocityEffects:0.##}\n");
                stringBuilder.Append($"Stall Modifier: {stallEffects:0.##}\n");

                if (simpleUpgradeThrustEffects != 1)
                    stringBuilder.Append($"Upgrade Thrust Modifier: {simpleUpgradeThrustEffects:0.##}\n");
                if (simpleUpgradePowerEffects != 1)
                    stringBuilder.Append($"Upgrade Power Modifier: {simpleUpgradePowerEffects:0.##}\n");
                if (compressorEffects != 1)
                    stringBuilder.Append($"Compressor Modifier: {compressorEffects:0.##}\n");
                if (stormEffects != 1)
                    stringBuilder.Append($"Storm Dynamo Modifier: {stormEffects:0.##}\n");
                if (interferenceEffects != 1)
                {
                    stringBuilder.Append($"Flow Interference Modifier: {interferenceEffects:0.##}\nInterfering Props:\n");
                    foreach(string interference in interferenceProps)
                    {
                        stringBuilder.Append(interference);
                    }
                }

            }
            catch(Exception e)
            { }
        }

        public static bool IsPositionInCylinder(Vector3D position, Vector3D cylinderCenterPosition, Vector3D cylinderAxis, double cylinderHeight, double cylinderRadius)
        {
            if (!Vector3D.IsUnit(ref cylinderAxis))
            {
                cylinderAxis = Vector3D.Normalize(cylinderAxis);
            }
            Vector3D dirn = position - cylinderCenterPosition;
            double height = Vector3D.Dot(dirn, cylinderAxis);
            if (Math.Abs(height) > cylinderHeight * 0.5)
            {
                return false;
            }

            Vector3D perpDirn = dirn - height * cylinderAxis;
            if (perpDirn.LengthSquared() > cylinderRadius * cylinderRadius)
            {
                return false;
            }

            return true;
        }

        public override void UpdateBeforeSimulation100()
        {
            if (grid.Physics == null || block == null || !block.Enabled)
                return;

            if (block.GetOwnerFactionTag() == PIRATE_TAG) { isNPC = true; }

            if (!block.IsFunctional)
            {
                if(overclocker > 0)
                    block.SlimBlock.DoDamage(1000000f, MyDamageType.Bullet, true); // woopsie
                return;
            }

            if (thruster.CurrentThrust == 0 || thruster.MaxEffectiveThrust == 0)
                return;

           (block as IMyTerminalBlock).RefreshCustomInfo();

            if(!(block.SlimBlock.BlockDefinition.Id.SubtypeId.String.Contains("Small") && grid.GridSizeEnum == MyCubeSize.Large) && !isNPC)
                UpdateObstructions();
        }

        private void UpdateObstructions()
        {
            IMySlimBlock slim = block.SlimBlock;

            for (int i = -3; i < 4; i++)
            {
                for (int j = -3; j < 4; j++)
                {
                    Vector3I location = (Vector3I)block.LocalMatrix.Right * i + (Vector3I)block.LocalMatrix.Up * j;

                    location += FindPropellerTip(slim);

                    IMySlimBlock slimFound = LocationIsObstructed(slim, location);
                    if (slimFound != null)
                    {
                        slim.DoDamage(damage, MyDamageType.Bullet, true);
                        slimFound.DoDamage(damage, MyDamageType.Bullet, true);
                        block.Enabled = false;
                        return;
                    }
                }
            }
        }

        private Vector3I FindPropellerTip(IMySlimBlock slim)
        {
            if (grid.GridSizeEnum == MyCubeSize.Small)
            {
                Vector3I mid = (slim.Max - slim.Min);
                if (mid.Length() > 1)
                    return mid / 2 + (Vector3I)block.LocalMatrix.Backward * mid.Length() / 2 + slim.Min;
                else if (mid == block.LocalMatrix.Backward)
                    return slim.Max;
                else
                    return slim.Min;
            }
            else
            {
                Vector3I mid = (slim.Min - slim.Max);
                if (mid.Length() > 1)
                    return mid / 2 + (Vector3I)block.LocalMatrix.Forward * mid.Length() / 2 + slim.Max;
                else if (mid == block.LocalMatrix.Forward)
                    return slim.Min;
                else
                    return slim.Max;
            }

            return Vector3I.Zero;
        }

        private int CalculatePerpendicularDistance(Vector3I propellerPosition1, Vector3I propellerPosition2, Base6Directions.Direction flowDirection)
        {
            // Calculate the Manhattan distance
            Vector3I diff = propellerPosition2 - propellerPosition1;

            // Convert the flow direction into a vector
            Vector3I directionVector = Base6Directions.GetIntVector(flowDirection);

            // Project the difference onto the flow direction vector
            int projection = Vector3I.Dot(diff, directionVector);

            // Calculate the perpendicular distance from the tube's axis
            Vector3I perpendicularDiff = diff - projection * directionVector;
            
            return Math.Abs(perpendicularDiff.X) + Math.Abs(perpendicularDiff.Y) + Math.Abs(perpendicularDiff.Z);
        }

        private float CalculatePropellerInterference()
        {
            if (isNPC) { return 1f; }

            PropellerGrid plane;
            float interferenceModifier = 1f;
            Vector3I propellerPosition1 = FindPropellerTip(block.SlimBlock);
            if (PropellerSession.instance.grids.TryGetValue(grid.EntityId, out plane))
            {
                interferenceProps.Clear();
                foreach (IMyThrust prop in plane.props)
                {
                    IMySlimBlock slim = prop.SlimBlock;

                    if (slim == block.SlimBlock)
                        continue;

                    Vector3I propellerPosition2 = FindPropellerTip(slim);
                    float manhattandistance = (float)Vector3I.DistanceManhattan(propellerPosition2, propellerPosition1);
                    var perpendicularDistance = CalculatePerpendicularDistance(propellerPosition1, propellerPosition2, block.SlimBlock.Orientation.Forward);

                    if (manhattandistance < 10)
                    {
                        var interference = (1.5f + prop.CurrentThrustPercentage) / (1f + (perpendicularDistance + manhattandistance) / 2);
                        interference = Math.Min(1f, interference);

                        interferenceModifier -= interference;
                        interferenceProps.Add($"{interference:P2}, ({perpendicularDistance}) from {prop.CustomName} (too close!)\n");
                        continue;
                    }

                    if(grid.GridSizeEnum == MyCubeSize.Small && perpendicularDistance < 5)
                    {
                        var interference = (1.5f + prop.CurrentThrustPercentage) / (1f + perpendicularDistance);
                        interference = Math.Min(1f, interference);

                        interferenceModifier -= interference;
                        interferenceProps.Add($"{interference:P2}, ({perpendicularDistance}) from {prop.CustomName} (small props are inline!)\n");
                    }
                }
            }
            // MyAPIGateway.Utilities.ShowNotification($"interference: {Math.Min(0f, interference)}");
            return Math.Max(0f, interferenceModifier);
        }

        private IMySlimBlock LocationIsObstructed(IMySlimBlock slim, Vector3I location)
        {
            IMySlimBlock slimFound = grid.GetCubeBlock(location);
            if (slimFound != null && slimFound != slim)
                return slimFound;

            return null;
        }

        public override void UpdateBeforeSimulation()
        {
            if (!isNPC)
                return;

            if (grid.Physics == null || block == null || !block.Enabled || !block.IsFunctional)
                return;

            return;
            MyAPIGateway.Utilities.ShowNotification("eee", 16);

            thrustVector = thruster.WorldMatrix.Backward;
            thruster.ThrustOverridePercentage = 1f;
            grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, thrustVector * thruster.MaxEffectiveThrust * 3f, grid.Physics.CenterOfMassWorld, null);
        }


        public override void UpdateBeforeSimulation10()
        {
            if (grid.Physics == null || block == null || !block.Enabled || !block.IsFunctional || thruster.CurrentThrust == 0 || thruster.MaxEffectiveThrust == 0)
                return;

            if (planet == null)
            {
                planet = MyGamePruningStructure.GetClosestPlanet(block.WorldMatrix.Translation);
                return;
            }

            gravVector = Vector3.Normalize(grid.Physics.Gravity);
            thrustVector = thruster.WorldMatrix.Backward;

            throttle = thruster.CurrentThrust / thruster.MaxThrust;
            altitude = (float)Vector3D.Distance(block.WorldMatrix.Translation, planet.PositionComp.GetPosition());
            speed = (float)Vector3D.Dot(thrustVector, grid.Physics.LinearVelocity);

            //dgaf about 2 blade props
            if (grid.GridSizeEnum == MyCubeSize.Large && !block.SlimBlock.BlockDefinition.Id.SubtypeId.String.Contains("Small"))
            {

                Vector3D propLocation = block.WorldMatrix.Translation + block.WorldMatrix.Forward;

                // 6 blade prop has different location
                if (block.SlimBlock.BlockDefinition.Id.SubtypeId.String.Contains("Large"))
                    propLocation += block.WorldMatrix.Forward * 4;

                List<IMyPlayer> players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);
                MyAPIGateway.Parallel.ForEach(players, player =>
                {
                    if (player.Character != null && player.Character.Physics != null && !player.Character.IsDead && IsPositionInCylinder(player.Character.WorldMatrix.Translation, propLocation, block.WorldMatrix.Forward, 2.5, 8)) //
                    {
                        player.Character.DoDamage(33f * (1f + throttle), MyDamageType.Bullet, true);
                        player.Character.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, MyUtils.GetRandomVector3() * 100000f * (1f + throttle), null, null);
                    }
                });
            }

            if(overclocker >= 1.0)
                block.SlimBlock.DoDamage(throttle * (2f + (float)Math.Pow(overclocker,1.5f)), MyDamageType.Bullet, true);

            // instead of a linear curve for thrust, we have an exponential that starts efficient and gets more inefficient.
            float thrustModifier = (float)Math.Pow(throttle, 0.5);
            float powerModifier = throttle;

            simpleUpgradeThrustEffects = CalculateSimpleThrustModifiers();
            simpleUpgradePowerEffects = CalculateSimplePowerModifiers();

            compressorEffects = CalculateCompressorEffects();
            velocityEffects = CalculateVelocityEffects();
            altitudeEffects = CalculateAltitudeEffects();
            stallEffects = CalculateStallEffects();
            interferenceEffects = CalculatePropellerInterference();
            stormEffects = CalculateStormEffects();

            thrustModifier *= simpleUpgradeThrustEffects;
            thrustModifier *= compressorEffects;
            thrustModifier *= velocityEffects;
            thrustModifier *= altitudeEffects;
            thrustModifier *= stallEffects;
            thrustModifier *= interferenceEffects;
            thrustModifier *= stormEffects;

            powerModifier *= simpleUpgradePowerEffects;
            powerModifier *= 1f + (1f - interferenceEffects);
            powerModifier *= 1 / stormEffects;

            thruster.ThrustMultiplier = thrustModifier;
            thruster.PowerConsumptionMultiplier = powerModifier;
        }

        private void OnUpgradeValuesChangedUpgrade()
        {
            intake = thruster.UpgradeValues["intake"];
            stormdynamo = thruster.UpgradeValues["stormdynamo"];
            turbocharger = thruster.UpgradeValues["turbocharger"];
            supercharger = thruster.UpgradeValues["supercharger"];
            enginesensor = thruster.UpgradeValues["enginesensor"];
            overclocker = thruster.UpgradeValues["overclocker"];
            fuelinjector = thruster.UpgradeValues["fuelinjector"];
            carburetor = thruster.UpgradeValues["carburetor"];
            nosinjector = thruster.UpgradeValues["nosinjector"];
        }

        private float CalculateSimplePowerModifiers()
        {
            float modifier = 1f;

            modifier += fuelinjector / 4f;
            modifier += carburetor;
            modifier *= (float)Math.Pow(0.8, (double)enginesensor);
            modifier += turbocharger / 2f;
            modifier += supercharger / 2f;

            return modifier;
        }

        private float CalculateSimpleThrustModifiers()
        {
            float modifier = 1f;

            modifier += fuelinjector / 4f;
            modifier += overclocker;
            modifier += carburetor / 2f;

            return modifier;
        }

        private float CalculateCompressorEffects()
        {
            float compressorModifier = 1f;

            compressorModifier += Math.Max((-1f + 5 * throttle) * turbocharger * speed / 600f,-0.25f) + Math.Max(3 * supercharger * (1f - speed / 100f), -0.25f);

            return compressorModifier;
        }

        private float CalculateVelocityEffects()
        {
            if (speed < -5f)
                return 0.5f;

            float unmodifedEfficiency = 0.9f + 0.0024f * speed - 0.0000143f * speed * speed; // 0.25 at 300ms

            if (intake == 0f)
                return unmodifedEfficiency;

            float modifiedEfficiency = 0.9f + 0.002f * speed - 0.00001f * speed * speed; // 0.6 at 300ms

            return intake * modifiedEfficiency + (1f - intake) * unmodifedEfficiency;
        }

        private float CalculateStormEffects()
        {
            float stormModifier = 1f;

            if (altitude < planet.MinimumRadius * 1.016f)
            {
                stormModifier += 105 * stormdynamo * (planet.MinimumRadius * 1.03f / altitude - 1f); // magic numbers go yeet
            }

            return stormModifier;
        }

        // ingame does fine but we have things that work off them
        private float CalculateAltitudeEffects()
        {
            float altitudeModifier = 1f;

            if (nosinjector > 0f)
                altitudeModifier += nosinjector * (1f - planet.GetAirDensity(grid.WorldMatrix.Translation)) / 2f;

            return altitudeModifier;
        }

        private float CalculateStallEffects()
        {
            if (isNPC) { return 1f; }

            float inclination = Vector3.Dot(gravVector, -thrustVector);

            if (grid.GridSizeEnum == MyCubeSize.Large)
                return 1f;

            if (inclination > .53)
                return 2f - (.47f + inclination) * (.47f + inclination);

            return 1f;
        }
    }

    public class PropellerGrid
    {
        public IMyCubeGrid grid;
        public float thrust = 0f;
        public float production = 0f;
        public float consumption = 0f;
        public float totalFuel = 0f;

        public HashSet<IMyThrust> props = new HashSet<IMyThrust>();
        public HashSet<IMyGasGenerator> engis = new HashSet<IMyGasGenerator>();
        public HashSet<IMyCubeBlock> cargs = new HashSet<IMyCubeBlock>();
        private PID pid = new PID(1, 0.01, 0.1);

        public PropellerGrid(IMyCubeGrid grid)
        {
            this.grid = grid;
        }

        public void UpdateFuel()
        {
            totalFuel = 0f;

            foreach (IMyCubeBlock cargoes in cargs.ToList())
            {
                IMyInventory inventory = cargoes.GetInventory(0);
                if (inventory == null)
                    continue;

                MyFixedPoint value = inventory.GetItemAmount(MyDefinitionId.Parse("MyObjectBuilder_Ore/Ice"));
                if (value != null)
                    totalFuel += (float)value;
            }

            //MyAPIGateway.Utilities.ShowNotification($"fuel minutes: {totalFuel / consumption / 60}", 1600);
        }

        public void UpdateThrustAndProduction()
        {
            var lastThrust = thrust;

            //MyAPIGateway.Utilities.ShowNotification($"engis: {engis.Count}\nprops: {props.Count}", 160);

            thrust = 0f;
            production = 0f;
            consumption = 0f;

            foreach (IMyGasGenerator engi in engis.ToList())
            {
                if (engi == null || !engi.Enabled || !engi.IsFunctional)
                    continue;

                MyResourceSourceComponent source = engi.Components.Get<MyResourceSourceComponent>();

                if (source != null)
                {
                    production += source.MaxOutput;
                    consumption += source.CurrentOutput / source.MaxOutput * (engi.SlimBlock.BlockDefinition as MyOxygenGeneratorDefinition).IceConsumptionPerSecond;
                }
            }
            foreach (IMyThrust prop in props.ToList())
            {
                if (prop == null)
                    continue;

                MyResourceSinkComponent sink = (prop as MyThrust).Components.Get<MyResourceSinkComponent>();
                if(sink != null)
                {
                    thrust += sink.CurrentInput;
                }
            }
        }
    }

    //[MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false)]
    //public class FuelMetrics : MyGameLogicComponent
    //{
    //    private IMyGasGenerator _generator;
    //    private IMyTerminalBlock _terminalBlock;

    //    private float _iceConsumptionRate;
    //    private float _iceToGasRatio;
    //    private float _hydrogenProduction; 


    //    public override void Init(MyObjectBuilder_EntityBase objectBuilder)
    //    {
    //        _generator = (IMyGasGenerator)Entity;
    //        _terminalBlock = _generator as IMyTerminalBlock;
    //        _terminalBlock.AppendingCustomInfo += OnWriteToTerminal;

    //        LoadBlockDefinition();
    //        this.NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
    //    }

    //    public override void Close()
    //    {
    //        _terminalBlock.AppendingCustomInfo -= OnWriteToTerminal;
    //    }

    //    private void LoadBlockDefinition()
    //    {
    //        MyOxygenGeneratorDefinition blockDefinition = MyDefinitionManager.Static.GetCubeBlockDefinition(_generator.BlockDefinition) as MyOxygenGeneratorDefinition;
    //        if (blockDefinition != null)
    //        {
    //            _iceConsumptionRate = blockDefinition.IceConsumptionPerSecond;
    //            _iceToGasRatio = blockDefinition.ProducedGases[0].IceToGasRatio;
    //            _hydrogenProduction = _iceConsumptionRate * _iceToGasRatio; // Hydrogen production rate in L/s
    //        }
    //    }

    //    public override void UpdateAfterSimulation100()
    //    {
    //        try
    //        {
    //            LoadBlockDefinition();
    //        }
    //        catch (Exception e)
    //        {

    //        }
    //        _terminalBlock.RefreshCustomInfo();
    //    }

    //    public void OnWriteToTerminal(IMyTerminalBlock terminalBlock, StringBuilder stringBuilder)
    //    {
    //        try
    //        {
    //            stringBuilder.Clear(); // fuck you, keen
    //            stringBuilder.Append(
    //                $"Ice Consumption: {_iceConsumptionRate:F2} kg/s\n" +
    //                $"Hydrogen Production: {_hydrogenProduction:F2} L/s\n" +
    //                $"Efficiency: {_iceToGasRatio:F2} L/kg"
    //            );
    //        }
    //        catch (Exception e)
    //        { }
    //    }
    //}
}