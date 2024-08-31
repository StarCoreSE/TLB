using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using System;
using System.Text;
using System.Linq;
using VRage.Utils;
using VRage.ModAPI;
using VRage.Game;
using VRage;
using SpaceEngineers.Game.Entities.Blocks;
using Sandbox.Game.GameSystems;
using System.Security.AccessControl;
using VRage.Game.Entity;
using System.Numerics;
using System.Text.RegularExpressions;

namespace MODERN_WARFARE_CORE
{
    public static class Utilities
    {
        public static float UpdateAndCalculateMean(ref float[] values, float newValue)
        {
            int length = values.Length;

            // Shift all elements one position to the left
            for (int i = 1; i < length; i++)
            {
                values[i - 1] = values[i];
            }

            // Insert the new value at the end
            values[length - 1] = newValue;

            // Calculate the mean
            float sum = 0f;
            for (int i = 0; i < length; i++)
            {
                sum += values[i];
            }

            return sum / length;
        }
        public static double SpeedOfSound(float airDensity)
        {
            // Approximate curve based on https://www.engineeringtoolbox.com/elevation-speed-sound-air-d_1534.html, accurate down to 22.68 kPa.
            double speedOfSound = Math.Pow(8947200 * airDensity - 899699, 1 / 3.42938) + 236.712;
            if (speedOfSound < 295.1)
                speedOfSound = 295.1;

            return speedOfSound;
        }

        public static double Clamp(double value, double min, double max)
        {
            return Math.Max(Math.Min(value, max), min);
        }

        public static double BilinearInterpolation(double[,] array, double[] xrange, double[] yrange, double x, double y)
        {

            if (double.IsNaN(x)) x = xrange[0];
            if (double.IsNaN(y)) y = xrange[0];
            if (double.IsInfinity(x)) x = xrange[0];
            if (double.IsInfinity(y)) y = xrange[0];

            // Clamp x and y to be within the range
            x = Math.Max(xrange[0], Math.Min(x, xrange[xrange.Length - 1]));
            y = Math.Max(yrange[0], Math.Min(y, yrange[yrange.Length - 1]));

            // Find the bounding indices
            int xIndex1 = Array.BinarySearch(xrange, x);
            if (xIndex1 < 0) xIndex1 = ~xIndex1 - 1;
            xIndex1 = Math.Max(0, Math.Min(xIndex1, xrange.Length - 2)); // Ensure within bounds
            int xIndex2 = Math.Min(xIndex1 + 1, xrange.Length - 1);

            int yIndex1 = Array.BinarySearch(yrange, y);
            if (yIndex1 < 0) yIndex1 = ~yIndex1 - 1;
            yIndex1 = Math.Max(0, Math.Min(yIndex1, yrange.Length - 2)); // Ensure within bounds
            int yIndex2 = Math.Min(yIndex1 + 1, yrange.Length - 1);

            // Get the coordinates of the four bounding points
            double x1 = xrange[xIndex1];
            double x2 = xrange[xIndex2];
            double y1 = yrange[yIndex1];
            double y2 = yrange[yIndex2];

            double Q11 = array[yIndex1, xIndex1];
            double Q12 = array[yIndex2, xIndex1];
            double Q21 = array[yIndex1, xIndex2];
            double Q22 = array[yIndex2, xIndex2];

            // Calculate the interpolation weights
            double x1x = x2 - x;
            double x2x = x - x1;
            double y1y = y2 - y;
            double y2y = y - y1;

            double denominator = (x2 - x1) * (y2 - y1);

            // Perform bilinear interpolation
            double result = (Q11 * x1x * y1y + Q21 * x2x * y1y + Q12 * x1x * y2y + Q22 * x2x * y2y) / denominator;

            return result;
        }

        public static double LinearInterpolation(double[,] array, double x, double y)
        {
            int x0 = (int)Math.Floor(x);
            int x1 = x0 + 1;
            int y0 = (int)Math.Floor(y);
            int y1 = y0 + 1;

            if (x1 >= array.GetLength(0)) x1 = x0; // Handle edge cases
            if (y1 >= array.GetLength(1)) y1 = y0; // Handle edge cases

            double xFraction = x - x0;
            double yFraction = y - y0;

            // Interpolate along x for both y0 and y1
            double v0 = array[x0, y0] + (array[x1, y0] - array[x0, y0]) * xFraction;
            double v1 = array[x0, y1] + (array[x1, y1] - array[x0, y1]) * xFraction;

            // Interpolate along y
            return v0 + (v1 - v0) * yFraction;
        }
        public static double VectorAngleBetween(Vector3D a, Vector3D b)
        { //returns radians
          //Law of cosines to return the angle between two vectors.

            if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
        }
    }
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
                MechanicalPowerGrid vehicle;
                if (MechanicalPowerSession.instance.grids.TryGetValue(grid.EntityId, out vehicle))
                {
                    CurrentValue = (int)MathHelper.Clamp(vehicle.ice_fuel_consumed_per_second / vehicle.ice_fuel_consumed_per_second_max * 100f, 0f, 100f);
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
                MechanicalPowerGrid vehicle;
                if (MechanicalPowerSession.instance.grids.TryGetValue(grid.EntityId, out vehicle))
                {
                    CurrentValue = (float)(int)(vehicle.totalFuel / vehicle.ice_fuel_consumed_per_second * 0.016666f);
                    if (vehicle.totalFuel < 1)
                        CurrentValue = 0;

                    if (vehicle.ice_fuel_consumed_per_second < 1)
                        CurrentValue = -1f;
                }
            }
        }
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class MechanicalPowerSession : MySessionComponentBase
    {
        private const int updateTicks = 100;
        private int tick = 0;

        public static MechanicalPowerSession instance;
        public Dictionary<long, MechanicalPowerGrid> grids = new Dictionary<long, MechanicalPowerGrid>();

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
                MyAPIGateway.Parallel.ForEach(grids.Values.ToList(), vehicle =>
                {
                    vehicle.Update();
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

            MechanicalPowerGrid vehicle = new MechanicalPowerGrid(grid);

            foreach (IMyGasGenerator block in grid.GetFatBlocks<IMyGasGenerator>())
            {
                vehicle.engines.Add(block);
            }

            foreach (IMyThrust block in grid.GetFatBlocks<IMyThrust>())
            {
                vehicle.thrusters.Add(block);
            }

            foreach (IMyCargoContainer block in grid.GetFatBlocks<IMyCargoContainer>())
            {
                vehicle.cargos.Add(block);
            }

            if (!grids.ContainsKey(entity.EntityId))
                grids.Add(entity.EntityId, vehicle);
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

            MechanicalPowerGrid vehicle;
            grids.TryGetValue(grid.EntityId, out vehicle);
            if (vehicle == null)
                return;

            if (slim.FatBlock is IMyGasGenerator)
            {
                vehicle.engines.Add(slim.FatBlock as IMyGasGenerator);
                vehicle.cargos.Add(slim.FatBlock as IMyCubeBlock);
                grids[grid.EntityId] = vehicle;
            }
            else if (slim.FatBlock is IMyThrust)
            {
                vehicle.thrusters.Add(slim.FatBlock as IMyThrust);
                grids[grid.EntityId] = vehicle;
            }
            else if (slim.FatBlock is IMyCargoContainer)
            {
                vehicle.cargos.Add(slim.FatBlock as IMyCubeBlock);
                grids[grid.EntityId] = vehicle;
            }
        }

        private void HandleBlockRemoved(IMySlimBlock slim)
        {
            if (slim.FatBlock == null)
                return;

            IMyCubeGrid grid = slim.CubeGrid;

            MechanicalPowerGrid vehicle;
            grids.TryGetValue(grid.EntityId, out vehicle);
            if (vehicle == null)
                return;

            if (slim.FatBlock is IMyGasGenerator)
            {
                vehicle.engines.Remove(slim.FatBlock as IMyGasGenerator);
                vehicle.cargos.Remove(slim.FatBlock as IMyCubeBlock);
                vehicle.Update();
                grids[grid.EntityId] = vehicle;
            }
            else if (slim.FatBlock is IMyThrust)
            {
                vehicle.thrusters.Remove(slim.FatBlock as IMyThrust);
                vehicle.Update();
                grids[grid.EntityId] = vehicle;
            }
            else if (slim.FatBlock is IMyCargoContainer)
            {
                vehicle.cargos.Remove(slim.FatBlock as IMyCubeBlock);
                vehicle.Update();
                grids[grid.EntityId] = vehicle;
            }
        }
    }

    /*[MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false)]
    public class Engine : MyGameLogicComponent
    {
        MyResourceSourceComponent sourceComp;
        IMyFunctionalBlock block;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = Entity as IMyFunctionalBlock;
            sourceComp = new MyResourceSourceComponent();
            block.CubeGrid.Components.Add(sourceComp);
            sourceComp.Init(MyStringHash.GetOrCompute("SolarPanels"), new MyResourceSourceInfo()
            {
                DefinedOutput = 1,
                IsInfiniteCapacity = true,
                ProductionToCapacityMultiplier = 1,
                ResourceTypeId = MyResourceDistributorComponent.ElectricityId,
            });
        }

        public override void UpdateBeforeSimulation10()
        {
            if(block.Enabled && block.IsFunctional)
                sourceComp.Enabled = true;
            else
                sourceComp.Enabled = false;
        }
    }*/

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false, "JetThruster")]
    public class JetThruster : MyGameLogicComponent
    {
        IMyThrust thruster;
        IMyCubeGrid grid;
        MyPlanet planet;

        double mach = 0;
        double vang = 0;
        const double MIN_STALL_SPEED = 100;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            thruster = Entity as IMyThrust;
            grid = thruster.CubeGrid;
            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            if (planet == null)
                planet = MyGamePruningStructure.GetClosestPlanet(grid.WorldMatrix.Translation);

        }

        public override void UpdateBeforeSimulation()
        {
            if (planet == null || grid?.Physics == null) return;

            var speed = thruster.CubeGrid.Physics.LinearVelocity.Length();
            mach = speed / Utilities.SpeedOfSound(planet.GetAirDensity(grid.WorldMatrix.Translation));

            thruster.PowerConsumptionMultiplier = thruster.MaxThrust / thruster.MaxEffectiveThrust;

            ApplyStallTorque();
            ApplyBadJetNoVtol();
        }

        private void ApplyBadJetNoVtol()
        {
            float inclination = Vector3.Dot(Vector3.Normalize(grid.Physics.Gravity), -thruster.WorldMatrix.Backward);

            thruster.ThrustMultiplier = 1 + (float)mach * 1f;

            if (inclination > .53)
                thruster.ThrustMultiplier *= 2f - (.47f + inclination) * (.47f + inclination);
            //MyAPIGateway.Utilities.ShowNotification($"{thruster.ThrustMultiplier}", 16);
        }

        private void ApplyStallTorque()
        {
            Vector3 velocity = grid.Physics.LinearVelocity;
            Vector3 forward = thruster.WorldMatrix.Backward;

            float mismatch = -Vector3.Dot(forward, Vector3.Normalize(velocity));

            if (mismatch > -0.96f && velocity.Length() < MIN_STALL_SPEED)
            {
                Vector3D torque = grid.WorldAABB.Size.Length()* grid.Physics.Mass * Vector3D.Cross(velocity, -forward) * Math.Min(velocity.Length(), MIN_STALL_SPEED) * (0.49f + mismatch * mismatch / 10f) / 5000f;
                grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, null, null, Vector3D.Transform(torque, MatrixD.Transpose(grid.WorldMatrix.GetOrientation())));
            }
        }
    }
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false, "JetEngineIntake")]
    public class JetIntake : MyGameLogicComponent
    {
        IMyGasGenerator intake;
        IMyCubeGrid grid;
        MyPlanet planet;

        double mach = 0;
        double vang = 0;

        double[,] cd_interp_table = new double[,] {
            { 0.021433, 0.024394, 0.028970, 0.035160, 0.042966, 0.052386, 0.063421, 0.076071, 0.090336, 0.106216, 0.123711 },
            { 0.021978, 0.025081, 0.029877, 0.036365, 0.044546, 0.054420, 0.065987, 0.079245, 0.094197, 0.110841, 0.129178 },
            { 0.023892, 0.027519, 0.033124, 0.040707, 0.050269, 0.061808, 0.075327, 0.090823, 0.108298, 0.127751, 0.149182 },
            { 0.028552, 0.033598, 0.041397, 0.051947, 0.065250, 0.081306, 0.100114, 0.121674, 0.145987, 0.173052, 0.202869 },
            { 0.075666, 0.086826, 0.104072, 0.127405, 0.156826, 0.192333, 0.233927, 0.281609, 0.335377, 0.395232, 0.461174 },
            { 0.227650, 0.231582, 0.243378, 0.263038, 0.290562, 0.325950, 0.369202, 0.420318, 0.479298, 0.546142, 0.620850 },
            { 0.024749, 0.025640, 0.028312, 0.032766, 0.039002, 0.047020, 0.056819, 0.068400, 0.081763, 0.096907, 0.113833 },
            { 0.017889, 0.018354, 0.019750, 0.022077, 0.025335, 0.029524, 0.034644, 0.040694, 0.047675, 0.055588, 0.064431 },
            { 0.014364, 0.014664, 0.015564, 0.017064, 0.019165, 0.021866, 0.025166, 0.029067, 0.033568, 0.038670, 0.044371 },
            { 0.012128, 0.012342, 0.012984, 0.014054, 0.015551, 0.017477, 0.019830, 0.022611, 0.025820, 0.029457, 0.033522 },
            { 0.010553, 0.010715, 0.011201, 0.012011, 0.013144, 0.014602, 0.016384, 0.018490, 0.020919, 0.023673, 0.026750 },
            { 0.009370, 0.009498, 0.009881, 0.010519, 0.011413, 0.012562, 0.013967, 0.015627, 0.017542, 0.019713, 0.022139 },
            { 0.008442, 0.008546, 0.008857, 0.009375, 0.010101, 0.011034, 0.012174, 0.013522, 0.015077, 0.016839, 0.018809 },
            { 0.007692, 0.007778, 0.008036, 0.008467, 0.009069, 0.009844, 0.010790, 0.011909, 0.013200, 0.014663, 0.016298 },
            { 0.007071, 0.007144, 0.007362, 0.007726, 0.008235, 0.008889, 0.009689, 0.010634, 0.011725, 0.012962, 0.014343 },
        };

        double[] mrange = new double[] {0.0, 0.21428571, 0.42857143, 0.64285714, 0.85714286, 1.07142857, 1.28571429, 1.5, 1.71428571, 1.92857143, 2.14285714, 2.35714286, 2.57142857, 2.78571429, 3.0};
        double[] aoarange = new double[] {0.0, 0.02617994, 0.05235988, 0.07853982, 0.10471976, 0.13089969, 0.15707963, 0.18325957, 0.20943951, 0.23561945, 0.26179939};

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            intake = Entity as IMyGasGenerator;
            grid = intake.CubeGrid;
            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            if (planet == null && grid != null)
                planet = MyGamePruningStructure.GetClosestPlanet(grid.WorldMatrix.Translation);

            if (planet == null || grid?.Physics == null) return;

            var speed = intake.CubeGrid.Physics.LinearVelocity.Length();

            mach = speed / Utilities.SpeedOfSound(planet.GetAirDensity(grid.WorldMatrix.Translation));
            vang = Utilities.VectorAngleBetween(intake.CubeGrid.Physics.LinearVelocity, intake.WorldMatrix.Forward);
            double cd = Utilities.BilinearInterpolation(cd_interp_table, mrange, aoarange, mach, vang);

            ApplyDrag(cd);
        }

        public void ApplyDrag(double cd)
        {
            var blockMatrix = intake.WorldMatrix;
            Vector3D thrustDirection = blockMatrix.Forward; // Opposite of the thruster's forward direction
            double velocityInThrustDirection = Vector3D.Dot(grid.Physics.LinearVelocity, thrustDirection);
            // Apply drag force proportional to the velocity component in the thrust direction
            Vector3D dragForce = -thrustDirection * 30 * cd * velocityInThrustDirection * velocityInThrustDirection;

            //grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, dragForce, applyForceAt, null);
            var subgrids = MyAPIGateway.GridGroups.GetGroup(grid, VRage.Game.ModAPI.GridLinkTypeEnum.Logical);
            foreach (MyCubeGrid subgrid in subgrids)
            {
                subgrid.Physics.LinearVelocity += dragForce / grid.Physics.Mass * 0.0167;
            }
        }

    }


    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false, "MainHelicopterRotor")]
    public class MainHelicopterRotorLogic : MyGameLogicComponent
    {
        Vector3 thrustVector;
        Vector3 gravVector;

        // MODAPI
        IMyThrust thruster;
        IMyFunctionalBlock block;
        IMyCubeGrid grid;
        MyThrustDefinition def;

        private const float _dragCoefficient = 0.1f;  // Adjust this value to change the drag effect
        private const float _fadeTime = 5.0f;  // Time in seconds for drag to fade to zero
        private double _lastThrustTime = MyAPIGateway.Session?.ElapsedPlayTime.TotalSeconds ?? double.PositiveInfinity;

        MechanicalPowerGrid vehicle;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            grid = (Entity as IMyTerminalBlock).CubeGrid;
            thruster = (Entity as IMyThrust);
            block = (IMyFunctionalBlock)Entity;
            def = ((Entity as MyCubeBlock).BlockDefinition) as MyThrustDefinition;

            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }

        public static bool IsPositionInCylinder(Vector3D position, Vector3D cylinderCenterPosition, Vector3D cylinderAxis, double cylinderHeight, double cylinderRadius)
        {
            // quick radius check
            if ((position - cylinderCenterPosition).LengthSquared() > cylinderRadius * cylinderRadius * 1.05)
                return false;

            if (!Vector3D.IsUnit(ref cylinderAxis))
            {
                cylinderAxis = Vector3D.Normalize(cylinderAxis);
            }
            Vector3D dirn = position - cylinderCenterPosition;
            double height = Vector3D.Dot(dirn, cylinderAxis);
            if (Math.Abs(height) > cylinderHeight * 0.5f)
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

        private void SwatSuits()
        {
            Vector3D propLocation = block.WorldMatrix.Translation + block.WorldMatrix.Backward * 1;

            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            MyAPIGateway.Parallel.ForEach(players, player =>
            {
                if (player.Character != null && player.Character.Physics != null && !player.Character.IsDead && IsPositionInCylinder(player.Character.WorldMatrix.Translation, propLocation, block.WorldMatrix.Forward, 2, 12)) //
                {
                    player.Character.DoDamage(33f, MyDamageType.Bullet, true);
                    player.Character.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, MyUtils.GetRandomVector3() * 100000f, null, null);
                }
            });
        }

        public override void UpdateBeforeSimulation()
        {
            if (grid.Physics == null || block == null || vehicle == null || thruster.MaxEffectiveThrust == 0)
                return;

            ApplyHeliDrag();

            if (!block.IsFunctional || thruster.CurrentThrust == 0 || !block.Enabled)
                return;

            _lastThrustTime = MyAPIGateway.Session.ElapsedPlayTime.TotalSeconds;

            ApplyPlayerControlledTorque();
            ApplyHeliSpeedLimit();
            ApplyRotorTorque();
        }

        public void ApplyHeliSpeedLimit()
        {
            if(grid.Physics.LinearVelocity.Length() > 100)
            {
                grid.Physics.LinearVelocity = Vector3D.Normalize(grid.Physics.LinearVelocity) * 100;
            }
        }

        public void ApplyHeliDrag()
        {
            double currentTime = MyAPIGateway.Session.ElapsedPlayTime.TotalSeconds;
            double timeSinceThrust = currentTime - _lastThrustTime;

            // Calculate fading drag multiplier based on time elapsed
            float dragMultiplier = 1.0f - MathHelper.Clamp((float)(timeSinceThrust / _fadeTime), 0.0f, 1.0f) * 10;

            // Calculate the velocity in the direction opposite to the thrust direction
            Vector3D thrustDirection = thruster.WorldMatrix.Backward; // Opposite of the thruster's forward direction
            double velocityInThrustDirection = Vector3D.Dot(grid.Physics.LinearVelocity, thrustDirection);

            // Apply drag force proportional to the velocity component in the thrust direction
            Vector3D dragForce = -thrustDirection * velocityInThrustDirection * velocityInThrustDirection * _dragCoefficient * dragMultiplier;

            grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, dragForce, null, null);
        }

        private void ApplyPlayerControlledTorque()
        {

            // Get the cockpit or ship controller the player is using
            var cockpit = grid.ControlSystem.CurrentShipController as IMyShipController;
            if (cockpit == null || !cockpit.IsUnderControl)
                return;

            // Get rotation input from the player's control of the cockpit
            double pitch = -MathHelper.Clamp(cockpit.RotationIndicator.X * 0.05, -1, 1);
            double yaw = -MathHelper.Clamp(cockpit.RotationIndicator.Y * 0.05, -1, 1);
            double roll = -(double)cockpit.RollIndicator;

            if (pitch == 0 && yaw == 0 && roll == 0)
                return;

            // Adjust sensitivity
            float strength = 500000f; // Adjust this to control how much torque is applied per input
            Vector3D torque = new Vector3D(pitch, yaw, roll) * strength;
            Vector3D torque_about_cockpit = Vector3D.TransformNormal(torque, cockpit.LocalMatrix);

            /*
            MyAPIGateway.Utilities.ShowNotification($"tq {pitch:0.##}, {yaw:0.##}, {roll:0.##}", 16);
            
            Vector4 red = Color.Red.ToVector4();
            Vector4 blu = Color.Blue.ToVector4();
            Vector4 gre = Color.Green.ToVector4();

            MySimpleObjectDraw.DrawLine(grid.Physics.CenterOfMassWorld, grid.Physics.CenterOfMassWorld + grid.WorldMatrix.Right * torque_about_cockpit.X / 5000, MyStringId.GetOrCompute("Square"), ref red, 0.1f);
            MySimpleObjectDraw.DrawLine(grid.Physics.CenterOfMassWorld, grid.Physics.CenterOfMassWorld + grid.WorldMatrix.Up * torque_about_cockpit.Y / 5000, MyStringId.GetOrCompute("Square"), ref blu, 0.1f);
            MySimpleObjectDraw.DrawLine(grid.Physics.CenterOfMassWorld, grid.Physics.CenterOfMassWorld + grid.WorldMatrix.Forward * torque_about_cockpit.Z / 5000, MyStringId.GetOrCompute("Square"), ref gre, 0.1f);
            */

            // Apply torque to the grid based on player input
            grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, null, null, torque_about_cockpit);

        }

        public void ApplyRotorTorque()
        {
            // rotor torque, fck you
            if (vehicle.thrusters.Count < 2)
            {
                Vector3D torque = grid.WorldAABB.Size.Length() * grid.Physics.Mass * thruster.WorldMatrix.Forward * thruster.CurrentThrust / thruster.MaxThrust;
                grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, null, null, Vector3D.Transform(torque, MatrixD.Transpose(grid.WorldMatrix.GetOrientation())));
            }
            else
            {
                bool should_torque = true;
                foreach (IMyThrust thrust in vehicle.thrusters)
                {
                    if (thrust == thruster)
                        continue;

                    if (thrust.Enabled && thrust.IsFunctional && thrust.CurrentThrust > 0 && (thrust.GetPosition() - thruster.GetPosition()).LengthSquared() > 100)
                    {
                        should_torque = false;
                    }
                }

                if (should_torque)
                {
                    Vector3D torque = grid.WorldAABB.Size.Length() * grid.Physics.Mass * thruster.WorldMatrix.Forward * thruster.CurrentThrust / thruster.MaxThrust;
                    grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, null, null, Vector3D.Transform(torque, MatrixD.Transpose(grid.WorldMatrix.GetOrientation())));
                }
            }
        }

        public override void UpdateBeforeSimulation10()
        {
            if (grid.Physics == null || block == null || !block.Enabled || !block.IsFunctional || thruster.CurrentThrust == 0 || thruster.MaxEffectiveThrust == 0)
                return;

            if (vehicle == null)
            {
                if (!MechanicalPowerSession.instance.grids.TryGetValue(grid.EntityId, out vehicle))
                    return;
            }

            SwatSuits();
        }

        private float CalculateStallEffects()
        {
            float inclination = Vector3.Dot(gravVector, -thrustVector);


            if (inclination > .53)
                return 2f - (.47f + inclination) * (.47f + inclination);

            return 1f;
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false, "TailHelicopterRotor")]
    public class TailHelicopterRotorLogic : MyGameLogicComponent
    {
        // MODAPI
        IMyThrust thruster;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            thruster = (Entity as IMyThrust);

            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }


        public override void UpdateBeforeSimulation()
        {
            if (thruster.CubeGrid.Physics == null || thruster == null)
                return;

            thruster.ThrustOverridePercentage = 1f;
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorSuspension), false)]
    public class MotorSuspensionLogic : MyGameLogicComponent
    {
        IMyCubeGrid grid;
        IMyMotorSuspension suspension;
        IMyFunctionalBlock con;

        bool turned_off_by_bob = false; // Flag to check if the suspension was turned off by the converter

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            suspension = (Entity as IMyMotorSuspension);
            grid = suspension.CubeGrid;

            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            if (grid == null || grid.Physics == null || suspension == null)
                return;

            if (suspension.Enabled && con == null)
                UpdateConverterBlock();

            if (con == null)
                return;
            //MyAPIGateway.Utilities.ShowNotification($"Converter is not null, and enabled", 160);
            bool converter_is_working = con.Enabled && con.IsFunctional & (con as MyFueledPowerProducer).Capacity > 0;

            // Check if the suspension is enabled and the converter is not working
            if (suspension.Enabled && !converter_is_working)
            {
                // Disable the suspension, and set the flag
                suspension.Enabled = false;
                turned_off_by_bob = true;
            }
            if (turned_off_by_bob && converter_is_working)
            {
                // Vice versa
                suspension.Enabled = true;
                turned_off_by_bob = false;
            }
        }

        public void UpdateConverterBlock()
        {
            List<IMySlimBlock> n = new List<IMySlimBlock>();
            suspension.SlimBlock.GetNeighbours(n); // Get the neighbours of the SUSPENSION block

            foreach (IMySlimBlock slim in n)
            {
                if(slim.FatBlock == null) continue;

                if(slim.BlockDefinition.Id.SubtypeName == "SuspensionConverter")
                {
                    con = slim.FatBlock as IMyFunctionalBlock;
                    break;
                }
            }

            /*if (con == null)
            {
                suspension.Enabled = false;
                turned_off_by_bob = true;
            }
            else if(turned_off_by_bob)
            {
                suspension.Enabled = true;
                turned_off_by_bob = false;
            }*/
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_HydrogenEngine), false, "SuspensionConverter")]
    public class MotorConverterLogic : MyGameLogicComponent
    {
        IMyCubeGrid grid;
        IMyMotorSuspension suspension;
        IMyFunctionalBlock con;

        //bool turned_off_by_bob = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            con = (Entity as IMyFunctionalBlock);

            grid = con.CubeGrid;

            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            if (grid == null || grid.Physics == null || con == null)
                return;

            if (suspension == null)
                UpdateSuspensionBlock();

            if (suspension == null)
            {
                con.Enabled = false;
                //turned_off_by_bob = true;
            }
            else //(turned_off_by_bob && !con.Enabled)
            {
                con.Enabled = true;
                //turned_off_by_bob = false;
            }
        }

        public void UpdateSuspensionBlock()
        {
            con.ShowInTerminal = false;
            con.ShowInInventory = false;
            con.ShowInToolbarConfig = false;

            List<IMySlimBlock> n = new List<IMySlimBlock>();
            con.SlimBlock.GetNeighbours(n);

            foreach (IMySlimBlock slim in n)
            {
                if (slim.FatBlock == null) continue;

                if (slim.FatBlock is IMyMotorSuspension)
                {
                    suspension = slim.FatBlock as IMyMotorSuspension;
                    break;
                }
            }

            /*if (suspension == null)
            {
                con.Enabled = false;
                //turned_off_by_bob = true;
            }
            else //(turned_off_by_bob && !con.Enabled)
            {
                con.Enabled = true;
                //turned_off_by_bob = false;
            }*/
        }
    }

    public class MechanicalPowerGrid
    {
        public IMyCubeGrid grid;

        public bool is_npc = false;

        public float hydrogen_consumption = 0f;
        public float hydrogen_production = 0f;
        public float ice_fuel_consumed_per_second = 0f;
        public float ice_fuel_consumed_per_second_max = 0f;

        public float totalFuel = 0f;

        public HashSet<IMyThrust> thrusters = new HashSet<IMyThrust>();
        public HashSet<IMyGasGenerator> engines = new HashSet<IMyGasGenerator>();
        public HashSet<IMyCubeBlock> cargos = new HashSet<IMyCubeBlock>();

        public MechanicalPowerGrid(IMyCubeGrid grid)
        {
            this.grid = grid;
        }

        public void Update()
        {
            UpdateFuel();
            UpdateThrustAndProduction();
        }

        public void UpdateFuel()
        {
            totalFuel = 0f;

            foreach (IMyCubeBlock cargoes in cargos.ToList())
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
            var lastThrust = hydrogen_consumption;

            //MyAPIGateway.Utilities.ShowNotification($"engis: {engis.Count}\nprops: {props.Count}", 160);

            hydrogen_consumption = 0f;
            hydrogen_production = 0f;
            ice_fuel_consumed_per_second = 0f;
            ice_fuel_consumed_per_second_max = 0f;

            foreach (IMyGasGenerator engi in engines.ToList())
            {
                if (engi == null || !engi.Enabled || !engi.IsFunctional)
                    continue;

                MyResourceSourceComponent source = engi.Components.Get<MyResourceSourceComponent>();

                if (source != null)
                {
                    hydrogen_production += source.MaxOutput;
                    ice_fuel_consumed_per_second += source.CurrentOutput / source.MaxOutput * (engi.SlimBlock.BlockDefinition as MyOxygenGeneratorDefinition).IceConsumptionPerSecond;
                    ice_fuel_consumed_per_second_max += (engi.SlimBlock.BlockDefinition as MyOxygenGeneratorDefinition).IceConsumptionPerSecond;
                }
            }
            //MyAPIGateway.Utilities.ShowNotification($"ice_fuel_consumed_per_second:  {ice_fuel_consumed_per_second}", 1600);
            //MyAPIGateway.Utilities.ShowNotification($"ice_fuel_consumed_per_second_max:  {ice_fuel_consumed_per_second_max}", 1600);
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