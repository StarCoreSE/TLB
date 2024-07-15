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
using System.Linq;
using VRage.Utils;
using VRage.ModAPI;
using VRage.Game;

namespace SKY_PIRATES_CORE
{
    public class ControlledEntityMass : IMyHudStat
    {
        public const string NumberFormat = "###,###,###,###,###,###,##0";

        public float CurrentBuoyancy = 0f;

        public MyStringHash Id { get; private set; }
        public float MinValue { get; } = 0f;
        public float MaxValue { get; } = 10000000f;

        public string GetValueString()
        {
            if (CurrentBuoyancy > 0)
                return $"{CurrentBuoyancy}% B, {CurrentValue}";

            return $"0% B, {CurrentValue}";
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

        public ControlledEntityMass()
        {
            Id = MyStringHash.GetOrCompute("controlled_mass");
        }

        public void Update()
        {
            if (MyAPIGateway.Session?.ControlledObject == null)
            {
                CurrentValue = 0f;
                CurrentBuoyancy = 0f;
                return;
            }
            IMyTerminalBlock controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
            if (controlled != null)
            {
                IMyCubeGrid grid = controlled.CubeGrid;
                Zeppelin zepp;
                if (CellSession.instance.grids.TryGetValue(grid.EntityId, out zepp))
                {
                    // MyAPIGateway.Utilities.ShowNotification($"z.buoy {zepp.buoyancyForce} z.mass {zepp.mass}");
                    CurrentBuoyancy = (float)(int)(zepp.buoyancyForce / zepp.mass * 100f);
                    CurrentValue = zepp.mass;
                }
                else
                {
                    CurrentBuoyancy = 0f;
                    CurrentValue = (grid as MyCubeGrid).GetCurrentMass();
                }
            }
        }
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class CellSession : MySessionComponentBase
    {
        private const long updateTicks = 100;
        private long tick = 0;

        public static CellSession instance;
        public Dictionary<long, Zeppelin> grids = new Dictionary<long, Zeppelin>();

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
                MyAPIGateway.Parallel.ForEach(grids.Values.ToList(), zepp =>
                {
                    zepp.UpdateBuoyancyForce();
                });
                tick = 0;
            }

            MyAPIGateway.Parallel.ForEach(grids.Values.ToList(), zepp =>
            {
                zepp.ApplyBuoyancyForce();
                zepp.ApplyZeppelinTorque();
            });

            tick++;
        }

        private void HandleEntityAdded(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null || (entity as IMyCubeGrid).GridSizeEnum == MyCubeSize.Small)
                return;

            IMyCubeGrid grid = entity as IMyCubeGrid;

            Zeppelin zepp = new Zeppelin(grid);

            foreach (IMyCubeBlock block in grid.GetFatBlocks<IMyCollector>())
            {
                if (block.SlimBlock.BlockDefinition.Id.SubtypeId.String.Contains("Zeppelin"))
                {
                    (block as IMyTerminalBlock).ShowInInventory = false;
                    zepp.cells.Add(block as IMyFunctionalBlock);
                }
            }

            foreach (IMyCubeBlock block in grid.GetFatBlocks<IMyCockpit>())
            {
                zepp.cocks.Add(block as IMyCockpit);
                if (block.SlimBlock.BlockDefinition.Id.SubtypeId.String.Contains("ZeppelinClassic"))
                    zepp.isClassic = true;
            }

            grid.Physics.LinearVelocity = Vector3D.Zero;
            zepp.UpdateBuoyancyForce();
            zepp.ApplyBuoyancyForce();
            zepp.ApplyZeppelinTorque();


            if (!grids.ContainsKey(entity.EntityId))
                grids.Add(entity.EntityId, zepp);

            grid.OnBlockAdded += HandleBlockAdded;
            grid.OnBlockRemoved += HandleBlockRemoved;
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
            if (slim.FatBlock == null || slim.FatBlock.CubeGrid.GridSizeEnum == MyCubeSize.Small)
                return;

            IMyCubeGrid grid = slim.CubeGrid;
            Zeppelin zepp;
            if(grids.TryGetValue(grid.EntityId, out zepp))
            {
                if (slim.BlockDefinition.Id.SubtypeId.String.Contains("Zeppelin"))
                {
                    (slim.FatBlock as IMyTerminalBlock).ShowInInventory = false;
                    zepp.cells.Add(slim.FatBlock as IMyFunctionalBlock);
                    zepp.UpdateBuoyancyForce();
                }
                else if (slim.FatBlock is IMyCockpit)
                {
                    zepp.cocks.Add(slim.FatBlock as IMyCockpit);
                    if (slim.BlockDefinition.Id.SubtypeId.String == "ZClassicCockpit")
                        zepp.isClassic = true;
                }
                grids[grid.EntityId] = zepp;
            }
        }

        private void HandleBlockRemoved(IMySlimBlock slim)
        {
            if (slim.FatBlock == null || slim.FatBlock.CubeGrid.GridSizeEnum == MyCubeSize.Small)
                return;

            IMyCubeGrid grid = slim.CubeGrid;

            Zeppelin zepp;
            if(grids.TryGetValue(grid.EntityId, out zepp))
            {
                if (slim.BlockDefinition.Id.SubtypeId.String.Contains("Zeppelin"))
                {
                    zepp.cells.Remove(slim.FatBlock as IMyFunctionalBlock);
                    zepp.UpdateBuoyancyForce();
                }
                else if (slim.FatBlock is IMyCockpit)
                {
                    bool isClassic = false;
                    zepp.cocks.Remove(slim.FatBlock as IMyCockpit);

                    foreach (var cock in zepp.cocks)
                    {
                        if (slim.BlockDefinition.Id.SubtypeId.String == "ZClassicCockpit")
                                zepp.isClassic = true;
                        break;
                    }
                    zepp.isClassic = true;
                }
                grids[grid.EntityId] = zepp;
            }
        }
    }

    public class Zeppelin
    {
        public const double buoyancyConstant = 150000;
        public const float burnDamage = 2000f;
        public const double updateRate = 0.01667d;
        public const double quarterCycle = Math.PI * 0.5f;

        public bool isClassic = false;
        public IMyCubeGrid grid;
        public HashSet<IMyFunctionalBlock> cells = new HashSet<IMyFunctionalBlock>();
        public HashSet<IMyCockpit> cocks = new HashSet<IMyCockpit>();
        public double buoyancyForce = 0;
        public float mass = 0;
        public MyPlanet planet;
        private PID pitchPID = new PID(0.5, 0.0, 0.25);
        private PID rollPID = new PID(0.5, 0.0, 0.25);
        private PID buoyancyPID = new PID(1, 0.01, 0.1);

        public Zeppelin(IMyCubeGrid grid)
        {
            this.grid = grid;
        }

        public void UpdateBuoyancyForce()
        {
            buoyancyForce = 0;

            if (grid.IsStatic || cells.ToList().Count == 0)
            {
                buoyancyForce = 0;
                return;
            }

            mass = (grid as MyCubeGrid).GetCurrentMass();

            foreach (IMyFunctionalBlock cell in cells.ToList())
            {

                if (cell == null)
                    continue;

                IMySlimBlock slim = cell.SlimBlock;
                float damageRatio = (slim.BuildIntegrity - slim.CurrentDamage) / slim.MaxIntegrity;
                buoyancyForce += damageRatio * buoyancyConstant;

                if (!cell.IsFunctional  && slim.CurrentDamage > 0)
                    slim.DoDamage(burnDamage, MyDamageType.Bullet, true);

            }

            if (planet == null)
            {
                buoyancyForce = 0;
                planet = MyGamePruningStructure.GetClosestPlanet(grid.WorldMatrix.Translation);
            }
            else
            {
                buoyancyForce *= planet.GetAirDensity(grid.WorldMatrix.Translation);
            }

            // what in gods name
            if (grid.CustomName.Contains("SkyWhale"))
                buoyancyForce *= 3;
        }

        public void ApplyBuoyancyForce()
        {
            if (grid == null || grid.Physics == null || grid.Physics.Gravity == Vector3D.Zero || grid.IsStatic || planet == null)
                return;

            float force = (float)buoyancyForce;
            double verticalSpeed = Vector3D.Dot(-Vector3D.Normalize(grid.Physics.Gravity), grid.Physics.LinearVelocity);
            float response = 0f;

            var player = MyAPIGateway.Players.GetPlayerControllingEntity(grid);
            var cockpit = player?.Controller.ControlledEntity as IMyShipController;
            bool isUndampedDirection = (verticalSpeed < 0 && cockpit?.MoveIndicator.Dot(Vector3.Down) > 0) || (verticalSpeed > 0 && cockpit?.MoveIndicator.Dot(Vector3.Up) > 0);

            if (force > mass && (grid as MyCubeGrid).DampenersEnabled && !isUndampedDirection)
            {
                response = (float)buoyancyPID.ControllerResponse(-verticalSpeed*0.2f, updateRate);
            }
            else
                buoyancyPID.Reset();

            if (force > mass)
                force = mass;

            if (cockpit != null)
                force *= 1f + cockpit.MoveIndicator.Y * 0.3333f * (float)buoyancyForce / mass;

            force *= 1f + response;

            force = Math.Min((float)buoyancyForce, force);

            // + Dark's shitty little vertical drag.
            grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, Vector3D.Transform(-grid.Physics.Gravity * force * (1 - (float)verticalSpeed * 0.02f), MatrixD.Transpose(grid.WorldMatrix.GetOrientation())), null, null);
        }

        public void ApplyZeppelinTorque()
        {
            if (grid.Physics == null || grid.Physics.IsStatic)
                return;

            Vector3 velocity = grid.Physics.LinearVelocity;
            Vector3 gravity = Vector3.Normalize(grid.Physics.Gravity);

            foreach(IMyCockpit cockpit in cocks)
            {
                if (cockpit == null)
                    continue;

                if (isClassic)
                    ClassicZeppelinTorque(cockpit, gravity);
                else
                    NewZeppelinTorque(cockpit, gravity);

                break;
            }
        }

        public void ClassicZeppelinTorque(IMyCockpit cock, Vector3D gravity)
        {
            var grid = cock.CubeGrid;

            Vector3D forward = cock.WorldMatrix.Forward;
            Vector3D right = cock.WorldMatrix.Right;
            Vector3D up = cock.WorldMatrix.Up;

            //PID control for pitch and roll
            //find the error for pitch and roll
            double pitchError = VectorAngleBetween(forward, -gravity) - quarterCycle;
            double rollError = VectorAngleBetween(right, -gravity) - quarterCycle;

            //run the PID control
            double pitchAccel = pitchPID.ControllerResponse(pitchError, updateRate);
            double rollAccel = rollPID.ControllerResponse(-rollError, updateRate);

            //apply angular acceelrations here
            Vector3D angularVel = cock.CubeGrid.Physics.AngularVelocity;
            angularVel += right * pitchAccel;
            angularVel += forward * rollAccel;

            grid.Physics.AngularVelocity = angularVel;
        }

        public void NewZeppelinTorque(IMyCockpit cock, Vector3D gravity)
        {
            Vector3 up = cock.WorldMatrix.Up;
            float bank = -Vector3.Dot(up, Vector3.Normalize(gravity));
            float minValue = 0.98f;

            if (Math.Abs(bank) < minValue)
            {
                bank = 1.5f - bank * bank;

                IMyCubeGrid grid = cock.CubeGrid;
                Vector3D rightVector = Vector3D.Cross(gravity, up);

                Vector3D torque = 5 * grid.WorldAABB.Size.Length() * grid.Physics.Mass * rightVector * bank;
                grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, null, null, Vector3D.Transform(torque, MatrixD.Transpose(grid.WorldMatrix.GetOrientation())));
            }
        }

        private double VectorAngleBetween(Vector3D a, Vector3D b)
        { //returns radians
          //Law of cosines to return the angle between two vectors.

            if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
        }
    }

    public class PID
    {
        //default coefficients make a unity gain controller

        public double kP = 1;
        public double kI = 0;
        public double kD = 0;

        double errorSum = 0;
        double lastError = 0;

        public double integralDecay = 0.75; //Since whip does it too right
                                            //I actually haven't seen this in industry.

        long steps = 0;

        public PID()
        {

        }

        public PID(double P, double I, double D)
        {
            kP = P;
            kI = I;
            kD = D;
        }

        public double ControllerResponse(double error, double timeStep)
        {
            //this computes the controller response to the given error and timestep

            if (kI != 0)
            {
                //trapezoidal rule for integration. This could be overkill
                if (integralDecay != 0)
                {
                    errorSum *= integralDecay;
                }
                errorSum += (error + lastError) * 0.5 * timeStep;
                //If timeStep is constant, it can be taken out of this equation to reduce the number of floating point operations per tick
            }

            double errorD = (error - lastError) / timeStep;
            //If timeStep is constant, it can be taken out of this equation to reduce the number of floating point operations per tick
            if (steps++ == 0) errorD = 0;


            double y = kP * error;
            if (kD != 0) y += kD * errorD;
            if (kI != 0) y += kI * errorSum;

            lastError = error;

            return y;
        }

        public void Reset()
        {
            //this resets the state of the controller
            steps = 0;
            errorSum = 0;
            lastError = 0;
        }
    }
}