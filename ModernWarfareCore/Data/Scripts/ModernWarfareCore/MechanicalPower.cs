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

namespace MODERN_WARFARE_CORE
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
            if (grid.Physics == null || block == null || !block.Enabled || !block.IsFunctional || thruster.CurrentThrust == 0 || thruster.MaxEffectiveThrust == 0 || vehicle == null)
                return;

            // rotor torque, fck you
            if(vehicle.thrusters.Count < 2)
            {
                ApplyRotorTorque();
            }
            else
            {
                bool should_torque = true;
                foreach(IMyThrust thrust in vehicle.thrusters)
                {
                    if (thrust == thruster)
                        continue;

                    if(thrust.Enabled && thrust.IsFunctional && thrust.CurrentThrust > 0 && (thrust.GetPosition() - thruster.GetPosition()).LengthSquared() > 100)
                    {
                        should_torque = false;
                    }
                }

                if (should_torque)
                    ApplyRotorTorque();
            }
        }

        public void ApplyRotorTorque()
        {
            Vector3D torque = grid.WorldAABB.Size.Length() * grid.Physics.Mass * thruster.WorldMatrix.Forward * thruster.CurrentThrust / thruster.MaxThrust;
            grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, null, null, Vector3D.Transform(torque, MatrixD.Transpose(grid.WorldMatrix.GetOrientation())));
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
        IMyMotorSuspension sus;
        IMyFunctionalBlock con;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            sus = (Entity as IMyMotorSuspension);
            grid = sus.CubeGrid;

            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            if (grid == null || grid.Physics == null || sus == null)
                return;

            if (sus.Enabled && con == null)
                UpdateConverterBlock();

            if (con == null)
                return;
            
            if (sus.Enabled && (!con.Enabled || !con.IsFunctional || (con as MyFueledPowerProducer).Capacity == 0))// || con.Capacity == 0))
                sus.Enabled = false;

        }

        public void UpdateConverterBlock()
        {
            List<IMySlimBlock> n = new List<IMySlimBlock>();
            sus.SlimBlock.GetNeighbours(n);

            foreach (IMySlimBlock slim in n)
            {
                if(slim.FatBlock == null) continue;

                if(slim.BlockDefinition.Id.SubtypeName == "SuspensionConverter")
                {
                    con = slim.FatBlock as IMyFunctionalBlock;
                    break;
                }
            }

            if (con == null)
                sus.Enabled = false;
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