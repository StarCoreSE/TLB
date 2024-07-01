using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using System;
using System.Linq;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRage.Game.Entity;

namespace TargetLeading
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Core : MySessionComponentBase
    {
        private List<IMyCubeGrid> _grids = new List<IMyCubeGrid>();
        private Dictionary<long, IMyGps> _gpsPoints = new Dictionary<long, IMyGps>();
        private bool _wasInTurretLastFrame = false;
        private LeadingData CurrentData = null;
        List<double> fuzzTimeToIntercept = new List<double>();

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            MyAPIGateway.Entities.OnEntityAdd += AddGrid;
            MyAPIGateway.Entities.OnEntityRemove += RemoveGrid;
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            MyAPIGateway.Entities.OnEntityAdd -= AddGrid;
            MyAPIGateway.Entities.OnEntityRemove -= RemoveGrid;
        }

        private void AddGrid(IMyEntity ent)
        {
            IMyCubeGrid grid = ent as IMyCubeGrid;
            if (grid != null)
            {
                _grids.Add(grid);
            }
        }

        private void RemoveGrid(IMyEntity ent)
        {
            IMyCubeGrid grid = ent as IMyCubeGrid;
            if (grid != null && _grids.Contains(grid))
            {
                _grids.Remove(grid);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            CurrentData = LeadingData.GetLeadingData(MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity, CurrentData);

            if (CurrentData == null)
            {
                ClearGPS();
                return;
            }

            _wasInTurretLastFrame = true;

            if (CurrentData.Ammo == null)
            {
                ClearGPS();
                return;
            }

            float projectileSpeed = CurrentData.Ammo.DesiredSpeed;
            float projectileRange = Math.Min(5000f, CurrentData.Ammo.MaxTrajectory + 100f);
            float projectileRangeSquared = projectileRange * projectileRange;

            foreach (IMyCubeGrid grid in _grids)
            {
                if (grid.Physics == null)
                {
                    RemoveGPS(grid.EntityId);
                    continue;
                }

                // Ignores grids that are probalby just trash or wheels
                if (((MyCubeGrid)grid).BlocksCount <= 7) 
                {
                    RemoveGPS(grid.EntityId);
                    continue;
                }

                Vector3D gridLoc = grid.WorldAABB.Center;

                if (grid.EntityId == CurrentData.EntityId
                    || Vector3D.DistanceSquared(gridLoc, CurrentData.Position) > projectileRangeSquared
                    || !GridHasHostileOwners(grid))
                {
                    RemoveGPS(grid.EntityId);
                    continue;
                }

                IHitInfo hit;
                MyAPIGateway.Physics.CastRay(grid.WorldAABB.Min, CurrentData.Position, out hit);

                IHitInfo hit2;
                MyAPIGateway.Physics.CastRay(grid.WorldAABB.Max, CurrentData.Position, out hit2);

                if ((hit != null && hit.HitEntity.EntityId != CurrentData.EntityId && hit.HitEntity.EntityId != CurrentData.GridId) &&
                    (hit2 != null && hit2.HitEntity.EntityId != CurrentData.EntityId && hit2.HitEntity.EntityId != CurrentData.GridId))
                {
                    RemoveGPS(grid.EntityId);
                    continue;
                }

                /*
                Vector3D interceptPoint = CalculateProjectileInterceptPosition(
                    projectileSpeed,
                    CurrentData.Velocity,
                    CurrentData.Position,
                    grid.Physics.LinearVelocity,
                    gridLoc);
                */

                Vector3D targetPos = gridLoc;
                Vector3D targetVel = grid.Physics.LinearVelocity;
                Vector3D targetAcc = grid.Physics.LinearAcceleration;
                Vector3D targetGravity = grid.Physics.Gravity;
                double targetMaxSpeed = 300;
                Vector3D shooterPos = CurrentData.Position;
                Vector3D shooterVel = CurrentData.Velocity;
                double projectileAccMag = 0;
                double gravityMultiplier = 0;
                double projectileMaxSpeed = CurrentData.Ammo.DesiredSpeed;
                double projectileInitSpeed = CurrentData.Ammo.DesiredSpeed;

                if (CurrentData.Ammo is MyMissileAmmoDefinition)
                {
                    MyMissileAmmoDefinition missileDef = (MyMissileAmmoDefinition)CurrentData.Ammo;
                    projectileAccMag = missileDef.MissileAcceleration;
                    if (!missileDef.MissileSkipAcceleration)
                    {
                        projectileInitSpeed = missileDef.MissileInitialSpeed;
                        projectileAccMag = missileDef.MissileAcceleration;
                    }
                    if (missileDef.MissileGravityEnabled)
                        gravityMultiplier = 1   ;
                }

                double timeToIntercept = CalculateTimeToIntercept(
                    projectileInitSpeed,
                    ref shooterVel,
                    ref shooterPos,
                    ref targetVel,
                    ref targetPos);

                if (projectileInitSpeed < 30) // bob's shitty way
                {
                    if ((targetPos - shooterPos).Dot(shooterVel) < 0)
                        timeToIntercept = 0;

                    Color color = Color.Red;

                    Vector3D interceptHeading = (targetPos - shooterPos) / timeToIntercept + targetVel - 0.5 * targetGravity * timeToIntercept;
                    double speed = interceptHeading.Length();
                    Vector3D interceptPoint = (targetPos - shooterPos).Length() * Vector3D.Normalize(interceptHeading) + shooterPos;
                    double targetDot = Vector3D.Normalize(shooterVel).Dot(Vector3D.Normalize(interceptHeading));
                    double diffSpeed = Math.Abs(shooterVel.Length() - speed);
                    if (diffSpeed < 10 && targetDot > 0.9)
                        color = Color.Green;

                    AddGPS(grid.EntityId, interceptPoint, $"SPEED: {Math.Round(speed)}", color);

                }
                else // whiplash's way
                {
                    Vector3D interceptPoint = TrajectoryEstimation(
                        timeToIntercept,
                        ref targetPos,
                        ref targetVel,
                        ref targetAcc,
                        ref targetGravity,
                        targetMaxSpeed,
                        ref shooterPos,
                        ref shooterVel,
                        projectileMaxSpeed,
                        projectileSpeed,
                        projectileAccMag,
                        gravityMultiplier);

                    AddGPS(grid.EntityId, interceptPoint, "", Color.Orange);
                }
            }
        }

        public static bool GridHasHostileOwners(IMyCubeGrid grid)
        {
            var gridOwners = grid.BigOwners;
            foreach (var pid in gridOwners)
            {
                MyRelationsBetweenPlayerAndBlock relation = MyAPIGateway.Session.Player.GetRelationTo(pid);
                if (relation == MyRelationsBetweenPlayerAndBlock.Enemies)
                {
                    return true;
                }
            }
            return false;
        }

        /*
        ** Whip's Projectile Time To Intercept - Modified 07/21/2019
        */
        double CalculateTimeToIntercept(
            double projectileSpeed,
            ref Vector3D shooterVelocity,
            ref Vector3D shooterPosition,
            ref Vector3D targetVelocity,
            ref Vector3D targetPosition)
        {
            double timeToIntercept = -1;

            Vector3D deltaPos = targetPosition - shooterPosition;
            Vector3D deltaVel = targetVelocity - shooterVelocity;
            Vector3D deltaPosNorm = VectorMath.SafeNormalize(deltaPos);

            double closingSpeed = Vector3D.Dot(deltaVel, deltaPosNorm);
            Vector3D closingVel = closingSpeed * deltaPosNorm;
            Vector3D lateralVel = deltaVel - closingVel;

            double diff = projectileSpeed * projectileSpeed - lateralVel.LengthSquared();
            double closingDistance = Vector3D.Dot(deltaPos, deltaPosNorm);

            if (diff < 0 && projectileSpeed <= 30) // shitty stuff...
            {
                return closingDistance / -closingSpeed;
            }
            else if(diff < 0 && projectileSpeed > 30)
            {
                return 0;
            }

            double projectileClosingSpeed = Math.Sqrt(diff) - closingSpeed;
            timeToIntercept = closingDistance / projectileClosingSpeed;
            return timeToIntercept;
        }

        Vector3D TrajectoryEstimation(
            double timeToIntercept,
            ref Vector3D targetPos,
            ref Vector3D targetVel,
            ref Vector3D targetAcc,
            ref Vector3D targetGravity,
            double targetMaxSpeed,
            ref Vector3D shooterPos,
            ref Vector3D shooterVel,
            double projectileMaxSpeed,
            double projectileInitSpeed = 0,
            double projectileAccMag = 0,
            double gravityMultiplier = 0)
        {
            bool projectileAccelerates = projectileAccMag > 1e-6;
            bool hasGravity = gravityMultiplier > 1e-6;

            double shooterVelScaleFactor = 1;
            if (projectileAccelerates)
            {
                /*
                This is a rough estimate to smooth out our initial guess based upon the missile parameters.
                The reasoning is that the longer it takes to reach max velocity, the more the initial velocity
                has an overall impact on the estimated impact point.
                */
                shooterVelScaleFactor = Math.Min(1, (projectileMaxSpeed - projectileInitSpeed) / projectileAccMag);
            }

            /*
            Estimate our predicted impact point and aim direction
            */
            Vector3D estimatedImpactPoint = targetPos + timeToIntercept * (targetVel - shooterVel * shooterVelScaleFactor);

            if (!projectileAccelerates && !hasGravity && targetAcc.LengthSquared() < 1e-2)
            {
                return estimatedImpactPoint; // No need to simulate
            }

            Vector3D aimDirection = estimatedImpactPoint - shooterPos;
            Vector3D aimDirectionNorm = VectorMath.SafeNormalize(aimDirection);
            Vector3D projectileVel = shooterVel;
            Vector3D projectilePos = shooterPos;

            if (projectileAccelerates)
            {
                projectileVel += aimDirectionNorm * projectileInitSpeed;
            }
            else
            {
                projectileVel += aimDirectionNorm * projectileMaxSpeed;
            }

            /*
            Target trajectory estimation. We do only 10 steps since PBs are instruction limited.
            */
            double dt = Math.Max(1.0 / 60.0, timeToIntercept * 0.1); // TODO: This can be a const somewhere
            double timeSum = 0;
            double maxSpeedSq = targetMaxSpeed * targetMaxSpeed;
            double projectileMaxSpeedSq = projectileMaxSpeed * projectileMaxSpeed;
            Vector3D targetAccStep = targetAcc * dt;
            Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt;
            Vector3D gravityStep = targetGravity * gravityMultiplier * dt;

            Vector3D aimOffset = Vector3D.Zero;
            double minDiff = double.MaxValue;

            for (int i = 0; i < 10; ++i)
            {
                targetVel += targetAccStep;
                if (targetVel.LengthSquared() > maxSpeedSq)
                    targetVel = Vector3D.Normalize(targetVel) * targetMaxSpeed;
                targetPos += targetVel * dt;

                if (projectileAccelerates)
                {
                    projectileVel += projectileAccStep;
                    if (projectileVel.LengthSquared() > projectileMaxSpeedSq)
                    {
                        projectileVel = Vector3D.Normalize(projectileVel) * projectileMaxSpeed;
                        projectileAccStep *= 0; // FIXME: Make this configurable. This is a quick patch for 1.200 rocket behavior 
                    }
                }

                if (hasGravity)
                {
                    projectileVel += gravityStep;
                }

                projectilePos += projectileVel * dt;

                Vector3D diff = (targetPos - projectilePos);
                double diffLenSq = diff.LengthSquared();
                if (diffLenSq < minDiff)
                {
                    minDiff = diffLenSq;
                    aimOffset = diff;
                }

                timeSum += dt;
                if (timeSum > timeToIntercept)
                {
                    break;
                }
            }

            Vector3D lateralOffset = VectorMath.Rejection(aimOffset, aimDirectionNorm);
            return estimatedImpactPoint + lateralOffset;
        }

        private void AddGPS(long gridId, Vector3D target, string name, Color color)
        {
            if (!_gpsPoints.ContainsKey(gridId))
            {
                _gpsPoints.Add(gridId, MyAPIGateway.Session.GPS.Create(gridId.ToString(), "", target, true));
                MyAPIGateway.Session.GPS.AddLocalGps(_gpsPoints[gridId]);
                MyVisualScriptLogicProvider.SetGPSColor(gridId.ToString(), color);
            }

            _gpsPoints[gridId].Coords = target;
            _gpsPoints[gridId].Name = name;
        }

        private void ClearGPS()
        {
            if (!_wasInTurretLastFrame)
                return;

            foreach (IMyGps gps in _gpsPoints.Values)
            {
                MyAPIGateway.Session.GPS.RemoveLocalGps(gps);
            }
            _gpsPoints.Clear();

            _wasInTurretLastFrame = false;
        }

        private void RemoveGPS(long id)
        {
            if (_gpsPoints.ContainsKey(id))
            {
                MyAPIGateway.Session.GPS.RemoveLocalGps(_gpsPoints[id]);
                _gpsPoints.Remove(id);
            }
        }
    }

    public static class VectorMath
    {
        public static Vector3D SafeNormalize(Vector3D a)
        {
            if (Vector3D.IsZero(a))
                return Vector3D.Zero;

            if (Vector3D.IsUnit(ref a))
                return a;

            return Vector3D.Normalize(a);
        }

        public static Vector3D Rejection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return Vector3D.Zero;

            return a - a.Dot(b) / b.LengthSquared() * b;
        }

        public static double AngleBetween(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }

        public static double CosBetween(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
        }

        public static bool IsDotProductWithinTolerance(Vector3D a, Vector3D b, double tolerance)
        {
            double dot = Vector3D.Dot(a, b);
            double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance);
            return Math.Abs(dot) * dot > num;
        }
    }


    public class LeadingData
    {
        public long EntityId;
        public long GridId;
        public MyAmmoDefinition Ammo = null;
        public Vector3D Position = Vector3D.Zero;
        public Vector3D Velocity = Vector3D.Zero;

        internal Vector3D offset = Vector3D.Zero;
        internal int toolbarIndex = -1;

        public static LeadingData GetLeadingData(IMyEntity block, LeadingData current)
        {
            try
            {
                if (block is IMyLargeTurretBase)
                {
                    IMyLargeTurretBase turret = block as IMyLargeTurretBase;

                    return new LeadingData()
                    {
                        EntityId = turret.EntityId,
                        GridId = turret.CubeGrid.EntityId,
                        Position = turret.GetPosition(),
                        Velocity = turret.CubeGrid.Physics.LinearVelocity,
                        Ammo = (turret as IMyGunObject<MyGunBase>).GunBase.CurrentAmmoDefinition
                    };
                }
                else if (block is IMyShipController)
                {
                    IMyShipController cockpit = block as IMyShipController;

                    LeadingData data = new LeadingData
                    {
                        EntityId = block.EntityId,
                        GridId = cockpit.CubeGrid.EntityId,
                        Velocity = cockpit.CubeGrid.Physics.LinearVelocity,
                        Position = cockpit.CubeGrid.WorldAABB.Center
                    };

                    if (current != null)
                    {
                        SerializableDefinitionId definition = GetWeaponDef(cockpit, ref data.toolbarIndex);
                        if (current.toolbarIndex != data.toolbarIndex)
                        {
                            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                            cockpit.CubeGrid.GetBlocks(blocks, (b) =>
                            {
                                return b.BlockDefinition.Id == definition &&
                                b.FatBlock != null; // && TEEHEE I MESSED UP THE MODELS PLAYER BEWARE
                                //b.FatBlock.Orientation.Forward == cockpit.Orientation.Forward;
                            });

                            foreach (IMySlimBlock b in blocks)
                            {
                                data.offset += data.Position - b.FatBlock.PositionComp.WorldAABB.Center;

                                if (data.Ammo == null)
                                {
                                    data.Ammo = (b.FatBlock as IMyGunObject<MyGunBase>).GunBase.CurrentAmmoDefinition;
                                }   
                            }

                            data.offset = data.offset / blocks.Count;
                        }
                        else
                        {
                            data.offset = current.offset;
                            data.Ammo = current.Ammo;
                        }
                    }

                    data.Position = data.Position - data.offset;
                    return data;
                }
            }
            catch (Exception e)
            {
                // bad fix i know
                //MyLog.Default.Info(e.ToString());
            }

            return null;
        }

        public static SerializableDefinitionId GetWeaponDef(IMyShipController cockpit, ref int index)
        {
            MyObjectBuilder_ShipController builder = cockpit.GetObjectBuilderCubeBlock(false) as MyObjectBuilder_ShipController;

            if (builder == null || builder.Toolbar == null || !builder.Toolbar.SelectedSlot.HasValue)
            {
                return default(SerializableDefinitionId);
            }

            MyObjectBuilder_Toolbar toolbar = builder.Toolbar;
            var item = toolbar.Slots[toolbar.SelectedSlot.Value];
            if (!(item.Data is MyObjectBuilder_ToolbarItemWeapon))
            {
                return default(SerializableDefinitionId);
            }

            index = toolbar.SelectedSlot.Value;
            return (item.Data as MyObjectBuilder_ToolbarItemWeapon).defId;
        }
    }

    /// <summary>
    /// Generic polynomial in the form of: a0 + a1*x + a2*x^2 + ... + an*x^n
    /// </summary>
    public class PolynomialEquation
    {
        public readonly double[] Coefficients;
        public uint Order { get; private set; }

        public PolynomialEquation(uint order)
        {
            Order = order;
            Coefficients = new double[order + 1];
        }

        public double Evaluate(double x)
        {
            double value = 0;
            double xn = 1;
            for (int n = 0; n <= Order; ++n)
            {
                value += Coefficients[n] * xn;
                xn *= x;
            }
            return value;
        }

        public double Derivative(double x)
        {
            double deriv = 0;
            double xn_1 = 1;
            for (int n = 1; n <= Order; ++n)
            {
                deriv += n * Coefficients[n] * xn_1;
                xn_1 *= x;
            }

            return deriv;
        }
    }

    /// <summary>
    /// Class that solves the time to intercept for an inertial projectile and
    /// a target with constant acceleration. This uses Newton's method to numerically
    /// solve a quartic equation within a certain precision.
    /// </summary>
    public class ProjectileIntercept
    {
        double _projectileSpeed = 0;
        public double ProjectileSpeed
        {
            get { return _projectileSpeed; }
            set
            {
                if (value != _projectileSpeed)
                {
                    _projectileSpeed = value;
                    _oneOverSpeedSq = 1.0 / (_projectileSpeed * _projectileSpeed);
                }
            }
        }

        private double _oneOverSpeedSq;


        /// <summary>
        /// Estimates the root of an input function that is closest to the input estimate using Newton's
        /// method. The success of this method largely depends on the initial estimate that is provided.
        /// </summary>
        /// <param name="estimate">Estimate that will be updated by Newton's method</param>
        /// <param name="function">Function, f(x)</param>
        /// <param name="functionDerivative">Function derivative, f'(x)</param>
        /// <param name="tolerance">Convergence tolerance</param>
        /// <param name="maxSteps">Max number of iteration steps</param>
        /// <returns>Boolean indicating if Newton's method converged on a solution.</returns>
        public static bool NewtonsMethod(ref double estimate, Func<double, double> function, Func<double, double> functionDerivative, double tolerance = 1e-3, int maxSteps = 10)
        {
            for (int ii = 0; ii < maxSteps; ++ii)
            {
                double value = function(estimate);
                if (Math.Abs(value) < tolerance)
                {
                    return true;
                }
                estimate = estimate - value / functionDerivative(estimate);
            }
            return false;
        }


        private PolynomialEquation _interceptEquation = new PolynomialEquation(4);

        public ProjectileIntercept(double projectileSpeed)
        {
            ProjectileSpeed = projectileSpeed;
        }

        public bool Solve(ref double timeToIntercept, Vector3D relativePosition, Vector3D relativeVelocity, Vector3D acceleration, double tolerance = 1e-3, int maxIterations = 10)
        {
            _interceptEquation.Coefficients[4] = acceleration.LengthSquared() * 0.25 * _oneOverSpeedSq;
            _interceptEquation.Coefficients[3] = Vector3D.Dot(relativeVelocity, acceleration) * _oneOverSpeedSq;
            _interceptEquation.Coefficients[2] = (Vector3D.Dot(relativePosition, acceleration) + relativeVelocity.LengthSquared()) * _oneOverSpeedSq - 1.0;
            _interceptEquation.Coefficients[1] = 2.0 * Vector3D.Dot(relativePosition, relativeVelocity) * _oneOverSpeedSq;
            _interceptEquation.Coefficients[0] = relativePosition.LengthSquared() * _oneOverSpeedSq;

            return NewtonsMethod(ref timeToIntercept, _interceptEquation.Evaluate, _interceptEquation.Derivative, tolerance, maxIterations);
        }
    }
}
