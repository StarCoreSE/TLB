using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game.Components;
using VRage.Game;
using VRage.Game.ModAPI;
using System.Collections.Concurrent;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.Game.Lights;
using Sandbox.Common.ObjectBuilders;
using VRageRender.Lights;
using System.Reflection;

namespace ScriptedMissiles
{
    public static class VectorMath
    {
        public static Vector3D SafeNormalize(Vector3D a)
        {
            if (Vector3D.IsZero(a)) return Vector3D.Zero;
            if (Vector3D.IsUnit(ref a)) return a;
            return Vector3D.Normalize(a);
        }
        public static Vector3D Rejection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b)) return Vector3D.Zero;
            return a - a.Dot(b) / b.LengthSquared() * b;
        }
        public static double AngleBetween(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(
                    a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }
        public static double CosBetween(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return MathHelper.Clamp(
                    a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
        }
        public static bool IsDotProductWithinTolerance(Vector3D a, Vector3D b,
                                                       double tolerance)
        {
            double dot = Vector3D.Dot(a, b);
            double num =
                a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance);
            return Math.Abs(dot) * dot > num;
        }

        public static Vector3D VectorAzimuthElevation(IMyLargeTurretBase turret)
        {
            double el = turret.Elevation;
            double az = turret.Azimuth;
            Vector3D targetDirection;
            Vector3D.CreateFromAzimuthAndElevation(az, el, out targetDirection);
            return Vector3D.TransformNormal(targetDirection, turret.WorldMatrix);
        }

        public static void GetRotationAngles(ref Vector3D targetVector, ref MatrixD matrix, out double yaw, out double pitch)
        {
            MatrixD matrixTpose;
            MatrixD.Transpose(ref matrix, out matrixTpose);
            Vector3D localTargetVector;
            Vector3D.TransformNormal(ref targetVector, ref matrixTpose, out localTargetVector);
            Vector3D flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            yaw = VectorMath.AngleBetween(Vector3D.Forward, flattenedTargetVector) * Math.Sign(localTargetVector.X); // left is positive
            if (Math.Abs(yaw) < 1E-6 && localTargetVector.Z > 0)  // check for straight back case
                yaw = Math.PI;
            if (Vector3D.IsZero(flattenedTargetVector))  // check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = VectorMath.AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y);  // up is positive
        }

        public static void GetAzimuthAngle(ref Vector3D targetVector, ref MatrixD matrix, out double azimuth)
        {
            MatrixD matrixTpose;
            MatrixD.Transpose(ref matrix, out matrixTpose);
            Vector3D localTargetVector;
            Vector3D.TransformNormal(ref targetVector, ref matrixTpose, out localTargetVector);
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            azimuth = VectorMath.AngleBetween(Vector3D.Forward, flattenedTargetVector) * Math.Sign(localTargetVector.X); // left is positive
            if (Math.Abs(azimuth) < 1E-6 && localTargetVector.Z > 0)  // check for straight back case
                azimuth = Math.PI;
        }

        public static void GetElevationAngle(ref Vector3D targetVector, ref MatrixD matrix, out double pitch)
        {
            MatrixD matrixTpose;
            MatrixD.Transpose(ref matrix, out matrixTpose);
            Vector3D localTargetVector;
            Vector3D.TransformNormal(ref targetVector, ref matrixTpose, out localTargetVector);
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            if (Vector3D.IsZero(flattenedTargetVector))  // check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = VectorMath.AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y);  // up is positive
        }
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
    public class ScriptedMissileSession : MySessionComponentBase
    {
        private class ScriptedMissile
        {
            public IMyMissile missile;
            public IMyEntity target;
            private ScriptedMissileSession system; // Reference to GuidedMissileSystem

            private Vector3 lastLeadPosition = Vector3.Zero;

            private bool isBeamRider = false;
            private bool isHoming = false;
            private bool isProximityDetonator = false;

            private float time = 0;

            private const float ticktime = 1f / 60f;

            Vector3 direction = Vector3.Zero;
            Vector3 velocity = Vector3.Zero;

            Vector3 lastTargetPosition = Vector3.Zero;

            Vector3 position = Vector3.Zero;

            public ScriptedMissile(IMyMissile missile, IMyEntity target, ScriptedMissileSession system, bool isBeamRider = false, bool isHoming = false, bool isProximityDetonator = false)
            {
                this.missile = missile;
                this.target = target;
                this.system = system;
                this.isBeamRider = isBeamRider;
                this.isHoming = isHoming;
                this.isProximityDetonator = isProximityDetonator;

                direction = this.missile.WorldMatrix.Forward;

                position = this.missile.GetPosition();

                lastTargetPosition = position + direction * 10000;
                lastLeadPosition = position + direction * 10000;


                Update();
            }

            public Vector3 GetTargetPrediction()
            {
                if (target.MarkedForClose)
                {
                    return lastLeadPosition;
                }

                Vector3 missilePosition = missile.GetPosition();
                Vector3 missileVelocity = velocity;

                foreach (IMyMissile flare in system.flares.Values)
                {
                    Vector3 dist = flare.GetPosition() - missilePosition;
                    double flareAngle = VectorMath.AngleBetween(Vector3.Normalize(dist), direction);
                    //MyAPIGateway.Utilities.ShowNotification($"dist : {dist.Length()}", 1);
                    //Vector4 red = Color.Red.ToVector4();
                    //MySimpleObjectDraw.DrawLine(flare.GetPosition(), missilePosition, VRage.Utils.MyStringId.GetOrCompute("MiningBeam"), ref red, 1f);
                    if (flareAngle < 1.0 && dist.LengthSquared() < 10000)
                    {
                        lastLeadPosition = flare.GetPosition();
                        lastTargetPosition = flare.GetPosition();
                        return flare.GetPosition();
                    }
                }

                Vector3 targetPosition = target.GetPosition();

                Vector3 deltaPosition = targetPosition - missilePosition;

                double angle = VectorMath.AngleBetween(Vector3.Normalize(deltaPosition), direction);
                if (angle > 1.00 && angle < 3.00)
                {
                    MyAPIGateway.Utilities.ShowNotification($"ang : {angle}", 1);
                    return lastLeadPosition;
                }

                MyPhysicsComponentBase targetPhysics = null;

                if (target.Physics != null)
                    targetPhysics = target.Physics;

                if (targetPhysics == null && target.Parent != null && target.Parent.Physics != null)
                    targetPhysics = target.Parent.Physics;

                Vector3 targetVelocity;
                if (targetPhysics != null)
                {
                    targetVelocity = targetPhysics.LinearVelocity;
                }
                else
                {
                    targetVelocity = (targetPosition - lastTargetPosition) * 60;
                }

                Vector3 deltaVelocity = targetVelocity - missileVelocity;

                Vector3 leadPosition = targetPosition + (deltaPosition.Length() / deltaVelocity.Length()) * targetVelocity;

                lastLeadPosition = leadPosition;
                lastTargetPosition = targetPosition;

                return leadPosition;
            }

            public Vector3 GetBeamRiderPrediction()
            {
                IMyEntity entity = MyAPIGateway.Entities.GetEntityById(missile.LauncherId);

                if (entity == null) { return Vector3.Zero; } // Add a default return value to avoid compile error

                IMyLargeTurretBase turret = entity as IMyLargeTurretBase;

                if (turret == null) { return Vector3.Zero; } // Add a default return value to avoid compile error

                // Get the turret's orientation vectors
                double azimuth = turret.Azimuth;
                double elevation = turret.Elevation;

                //MyAPIGateway.Utilities.ShowNotification($"azi {azimuth} ele {elevation}");
                //Vector4 red = Color.Red.ToVector4();
                // MySimpleObjectDraw.DrawLine(shooterPosition, shooterPosition + shootingDirection * 1000, VRage.Utils.MyStringId.GetOrCompute("MiningBeam"), ref red, 1f);

                // Calculate the forward vector based on azimuth and elevation
                Vector3D shootingDirection = VectorMath.VectorAzimuthElevation(turret);

                Vector3D shooterPosition = turret.GetPosition();

                Vector3D missilePosition = missile.GetPosition();



                // Ensure the shooting direction is normalized
                shootingDirection = Vector3D.Normalize(shootingDirection);

                // Calculate the vector from the shooter to the missile
                Vector3D shooterToMissile = missilePosition - shooterPosition;

                // Project the shooterToMissile vector onto the shootingDirection to find the closest point on the shooting line
                Vector3D leadPosition = shooterPosition + (Vector3D.Dot(shooterToMissile, shootingDirection) + 100) * shootingDirection;

                lastLeadPosition = leadPosition;
                lastTargetPosition = leadPosition;

                return (Vector3)leadPosition;
            }

            public void Update()
            {

                if(isHoming || isBeamRider)
                {
                    UpdateDirection();

                    UpdateVelocity();

                    UpdateTransform();
                }

                if (isProximityDetonator)
                    UpdateProximityDetonation();

                time += ticktime;
            }

            public void UpdateVelocity()
            {
                MyMissileAmmoDefinition ammo = (MyMissileAmmoDefinition)missile.AmmoDefinition;
                float speed = Math.Min(ammo.MissileInitialSpeed + ammo.MissileAcceleration * time, ammo.DesiredSpeed);
                velocity = direction * speed;

                // yuck
                if (isBeamRider && ammo.MissileGravityEnabled)
                    velocity = missile.Physics.LinearVelocity * 0.25f + direction * (missile.Physics.LinearVelocity.Length() * 0.75f + ammo.MissileAcceleration * 0.01667f) + missile.Physics.Gravity * 0.01667f * 0.5f;

                if (missile.Physics != null)
                    missile.Physics.LinearVelocity = velocity;
            }

            public void UpdateDirection()
            {
                Vector3 predictionPosition = lastLeadPosition;

                if (isBeamRider)
                    predictionPosition = GetBeamRiderPrediction();
                else if(target != null)
                    predictionPosition = GetTargetPrediction();

                Vector3 predictionDirection = predictionPosition - missile.GetPosition();
                predictionDirection.Normalize();

                double angle = Math.Acos(Math.Max(Math.Min(Vector3.Dot(direction, predictionDirection), 1), -1)) / 50;

                Vector3 rotationAxis = Vector3.Cross(direction, predictionDirection);
                rotationAxis.Normalize();

                if (isBeamRider || (target != null && !target.MarkedForClose && target.Parent != null))
                {
                    Matrix rotationMatrix = Matrix.CreateFromQuaternion(Quaternion.CreateFromAxisAngle(rotationAxis, (float)(Math.Max(Math.Min(angle, 0.02), -0.02))));
                    direction = Vector3.Transform(direction, rotationMatrix);
                    direction.Normalize();
                }
            }

            public void UpdateTransform()
            {
                if (missile.Physics == null)
                {
                    position = position + velocity * ticktime;
                    missile.SetPosition(position);
                }

                MatrixD worldMatrix = missile.WorldMatrix;
                worldMatrix.Forward = direction;
                missile.WorldMatrix = worldMatrix;
            }

            public void UpdateProximityDetonation()
            {
                if (missile == null || missile.MarkedForClose || target == null || target.MarkedForClose)
                {
                    return;
                }

                MyMissileAmmoDefinition ammo = (MyMissileAmmoDefinition)missile.AmmoDefinition;
                Vector3 deltaPosition = target.GetPosition() - missile.GetPosition();

                if (deltaPosition.Length() < ammo.MissileExplosionRadius * 10)
                {
                    TryProximityDetonate(Vector3D.Normalize(deltaPosition), ammo.MissileExplosionRadius);
                }

            }

            private void TryProximityDetonate(Vector3D targetDirection, float radius)
            {
                IHitInfo hitinfo;
                MyAPIGateway.Physics.CastRay(missile.GetPosition() + targetDirection, target.GetPosition(), out hitinfo, 0, false);

                float value = 4f * radius;

                //if (hitinfo != null)
                //    MyAPIGateway.Utilities.ShowNotification($"ee {(hitinfo.Position - missile.GetPosition()).LengthSquared()}, {value * value}", 1600);

                if (hitinfo != null && (hitinfo.Position - missile.GetPosition()).LengthSquared() < value * value)
                {

                    missile.SetPosition(hitinfo.Position - targetDirection * radius * 0.5f);
                    //missile.Destroy();
                    //missile.DoDamage(100000f, MyStringHash.GetOrCompute("APS"), true);
                    system.missiles_to_be_exploded.Add(missile);
                }
            }
        }

        public static ScriptedMissileSession instance;

        Dictionary<long, ScriptedMissile> scriptedMissiles = new Dictionary<long, ScriptedMissile>();
        public Dictionary<long, IMyMissile> flares = new Dictionary<long, IMyMissile>();
        public HashSet<IMyMissile> all_missiles = new HashSet<IMyMissile>();
        public List<IMyMissile> missiles_to_be_exploded = new List<IMyMissile>();

        public override void BeforeStart()
        {
            MyAPIGateway.Missiles.OnMissileAdded += OnMissileAdded;
            MyAPIGateway.Missiles.OnMissileRemoved += OnMissileRemoved;
            instance = this;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Missiles.OnMissileAdded -= OnMissileAdded;
            MyAPIGateway.Missiles.OnMissileRemoved -= OnMissileRemoved;
        }

        private void OnMissileAdded(IMyMissile missile)
        {
            all_missiles.Add(missile);

            if (IsFlare(missile))
            {
                flares.Add(missile.EntityId, missile);
            }
            else if (IsSmoke(missile))
            {
                DispelTargetLocks(missile.LauncherId);
            }
            else if (IsBeamRider(missile) || IsHoming(missile) || IsProximity(missile))
            {
                missile.Synchronized = true;

                IMyEntity target = null;
                if (IsHoming(missile) || IsProximity(missile))
                    target = GetMissileTarget(missile);

                scriptedMissiles.Add(missile.EntityId, new ScriptedMissile(missile, target, this, IsBeamRider(missile), IsHoming(missile), IsProximity(missile)));
            }
        }

        private IMyEntity GetMissileTarget(IMyMissile missile)
        {
            IMyEntity target = null;

            List<IMyPlayer> myPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(myPlayers, (player) => player.IdentityId == missile.Owner || player.Controller?.ControlledEntity?.Entity?.EntityId == missile.LauncherId);

            if (myPlayers.Count > 0 && target == null)
            {
                IMyPlayer player = myPlayers.First();

                target = player?.Character?.Components?.Get<MyTargetLockingComponent>()?.TargetEntity;
                //MyAPIGateway.Utilities.ShowNotification($"target lock{target?.DisplayName}", 1600);
            }

            if (target == null)
            {
                IMyEntity entity = MyAPIGateway.Entities.GetEntityById(missile.LauncherId);
                if (entity != null)
                {
                    IMyLargeTurretBase launcher = entity as IMyLargeTurretBase;

                    if (launcher != null)
                    {
                        Sandbox.ModAPI.Ingame.MyDetectedEntityInfo info = launcher.GetTargetedEntity();
                        if (!info.IsEmpty() && info.EntityId != 0)
                        {
                            target = MyAPIGateway.Entities.GetEntityById(info.EntityId);
                            //MyAPIGateway.Utilities.ShowNotification($"target ai? {target?.DisplayName}", 1600);
                        }
                    }
                }

            }

            IMyCubeGrid targetGrid = target as IMyCubeGrid;
            if (targetGrid is IMyCubeGrid)
            {
                target = targetGrid.GetFatBlocks<IMyThrust>().Count() > 0 ? targetGrid.GetFatBlocks<IMyThrust>().First() : null;
                //MyAPIGateway.Utilities.ShowNotification($"target thrster?{target?.DisplayName}", 1600);

                if (target == null)
                {
                    target = targetGrid.GetFatBlocks<IMyTerminalBlock>().Count() > 0 ? targetGrid.GetFatBlocks<IMyTerminalBlock>().First() : null;
                    //MyAPIGateway.Utilities.ShowNotification($"target terminal? {target?.DisplayName}", 1600);
                }

                // fuck this
                if (target == null)
                {
                    var AttachedList = new List<IMyCubeGrid>();
                    MyAPIGateway.GridGroups.GetGroup(targetGrid, GridLinkTypeEnum.Physical, AttachedList);

                    if (AttachedList.Count > 1)
                    {
                        foreach(IMyCubeGrid attachedGrid in AttachedList)
                        {
                            if (attachedGrid == targetGrid)
                                continue;

                            target = attachedGrid.GetFatBlocks<IMyTerminalBlock>().Count() > 0 ? attachedGrid.GetFatBlocks<IMyTerminalBlock>().First() : null;

                            if (target != null)
                                break;
                        }
                    }
                }
            }

            return target;
        }

        private void DispelTargetLocks(long launcherEntId)
        {
            IMyCubeGrid grid = (MyAPIGateway.Entities.GetEntityById(launcherEntId) as IMyCubeBlock)?.CubeGrid;

            if (grid == null) return;

            List<IMyPlayer> myPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(myPlayers, (player) => player?.Character?.Components?.Get<MyTargetLockingComponent>()?.Target == grid);

            foreach (IMyPlayer player in myPlayers)
            {
                player.Character.Components.Get<MyTargetLockingComponent>().ReleaseTargetLock();
            }
        }

        private bool IsBeamRider(IMyMissile missile)
        {
            //MyAPIGateway.Utilities.ShowNotification($"ismissile : {missile.AmmoDefinition.Id.SubtypeName} : {missile.AmmoDefinition.Id.SubtypeName.Contains("Missile")}");
            if (missile.AmmoDefinition.Id.SubtypeName.Contains("BeamRider"))
                return true;

            return false;
        }

        private bool IsHoming(IMyMissile missile)
        {
            //MyAPIGateway.Utilities.ShowNotification($"ismissile : {missile.AmmoDefinition.Id.SubtypeName} : {missile.AmmoDefinition.Id.SubtypeName.Contains("Missile")}");
            if (missile.AmmoDefinition.Id.SubtypeName.Contains("Homing"))
                return true;

            return false;
        }
        private bool IsProximity(IMyMissile missile)
        {
            //MyAPIGateway.Utilities.ShowNotification($"ismissile : {missile.AmmoDefinition.Id.SubtypeName} : {missile.AmmoDefinition.Id.SubtypeName.Contains("Missile")}");
            if (missile.AmmoDefinition.Id.SubtypeName.Contains("Proximity"))
                return true;

            return false;
        }

        private bool IsFlare(IMyMissile missile)
        {
            if (missile.AmmoDefinition.Id.SubtypeName.Contains("Flare") || missile.AmmoDefinition.Id.SubtypeName.Contains("Firework"))
                return true;

            return false;
        }

        private bool IsSmoke(IMyMissile missile)
        {
            if (missile.AmmoDefinition.Id.SubtypeName.Contains("Smoke"))
                return true;

            return false;
        }

        private void OnMissileRemoved(IMyMissile missile)
        {
            all_missiles.Remove(missile);

            if (flares.ContainsKey(missile.EntityId))
                flares.Remove(missile.EntityId);

            if (scriptedMissiles.ContainsKey(missile.EntityId))
                scriptedMissiles.Remove(missile.EntityId);
        }

        private void CreateExplosionFromMissile(IMyMissile missile)
        {
            //Explosion Damage!
            MyMissileAmmoDefinition ammo = (MyMissileAmmoDefinition)missile.AmmoDefinition;
            BoundingSphereD sphere = new BoundingSphereD(missile.GetPosition(), ammo.MissileExplosionRadius);
            MyExplosionInfo explosion = new MyExplosionInfo(ammo.MissileExplosionDamage * ammo.ExplosiveDamageMultiplier, ammo.MissileExplosionDamage * ammo.ExplosiveDamageMultiplier, sphere, MyExplosionTypeEnum.MISSILE_EXPLOSION, true, true);
            explosion.CreateParticleEffect = true;
            explosion.LifespanMiliseconds = 150 + (int)ammo.MissileExplosionRadius * 45;
            MyExplosions.AddExplosion(ref explosion, true);
        }

        public override void UpdateBeforeSimulation()
        {

            missiles_to_be_exploded.Clear();

            foreach (var missile in scriptedMissiles)
            {
                missile.Value.Update();
            }

            foreach(var missile in missiles_to_be_exploded)
            {
                CreateExplosionFromMissile(missile);
                missile.Destroy();
            }
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeMissileTurret), false, "APSTurret")]
    public class APSTurret : MyGameLogicComponent
    {
        MyLight light;
        IMyGunObject<MyGunBase> gun;
        IMyLargeTurretBase turret;
        int tick;
        bool init = false;

        const int max_tick = 30;
        const string subtypeId = "Welder";

        List<IMyMissile> close_missiles = new List<IMyMissile>();

        public MyLight CreatAPSLight()
        {
            MyDefinitionId id = new MyDefinitionId(typeof(MyObjectBuilder_FlareDefinition), subtypeId);
            MyFlareDefinition flareDefinition = MyDefinitionManager.Static.GetDefinition(id) as MyFlareDefinition;

            light = MyLights.AddLight();
            light.Start("APSLight");
            light.Color = Color.Green;
            light.Range = 10;
            light.Falloff = 1f;
            light.Intensity = 10f;
            light.LightType = MyLightType.DEFAULT;
            light.ParentID = Entity.Render.GetRenderObjectID();
            light.LightOn = true;

            light.GlareOn = true;
            light.GlareQuerySize = 0.2f;
            light.GlareQueryFreqMinMs = 0f;
            light.GlareQueryFreqRndMs = 0f;
            light.GlareType = MyGlareTypeEnum.Distant;
            light.GlareMaxDistance = 5000f;

            if (flareDefinition != null && flareDefinition.SubGlares != null)
            {
                light.SubGlares = flareDefinition.SubGlares;
                light.GlareIntensity = flareDefinition.Intensity * 20f;
                light.GlareSize = flareDefinition.Size * 0.3f;
            }

            light.UpdateLight();

            return light;
        }

        public void RemoveAPSLight()
        {
            MyLights.RemoveLight(light);
            light = null;
        }
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            turret = Entity as IMyLargeTurretBase;
            gun = Entity as IMyGunObject<MyGunBase>;
            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }
        
        public override void UpdateBeforeSimulation()
        {

            if(!init)
            {
                init = true;
                turret.Enabled = false;
            }

            if (Entity == null || light == null)
                return;

            IMyCamera camera = MyAPIGateway.Session.Camera;
            Vector3D dirFromCam = Vector3D.Normalize(turret.GetPosition() - camera.WorldMatrix.Translation);
            light.Position = turret.GetPosition() - dirFromCam;
            light.UpdateLight();

            MyGunStatusEnum status;

            if (!gun.CanShoot(MyShootActionEnum.PrimaryAction, turret.OwnerId, out status))
                return;


            //GetAllMissilesInSphere try this eventally?
            foreach (IMyMissile missile in close_missiles)
            {
                BoundingSphereD sphere = new BoundingSphereD(turret.GetPosition(), 20);
                RayD ray = new RayD(missile.GetPosition(), missile.WorldMatrix.Forward);

                if ((missile.GetPosition() - turret.GetPosition()).LengthSquared() < 400 || ray.Intersects(sphere) < missile.LinearVelocity.Length() * 0.01667f)
                {
                    Vector3D turretDirection = missile.GetPosition() - turret.GetPosition();
                    double yaw, pitch;
                    MatrixD matrix = turret.WorldMatrix;
                    VectorMath.GetRotationAngles(ref turretDirection, ref matrix, out yaw, out pitch);
                    turret.Azimuth = (float)-yaw;
                    turret.Elevation = (float)pitch;
                    turret.SyncAzimuth();
                    turret.ShootOnce();

                    missile.DoDamage(100000f, MyStringHash.GetOrCompute("APS"), true);
                    break;
                }
            }
        }

        public override void UpdateBeforeSimulation10()
        {
            close_missiles.Clear();

            if (Entity == null || turret.CubeGrid.Physics == null || !turret.Enabled || !turret.IsFunctional || !turret.IsWorking)
            {
                if (light != null)
                    RemoveAPSLight();

                return;
            }

            if (light == null && !MyAPIGateway.Utilities.IsDedicated)
                light = CreatAPSLight();

            //MyAPIGateway.Utilities.ShowNotification($"tic {tick}", 160);
            tick++;

            MyGunStatusEnum status;
            bool canShoot = gun.CanShoot(MyShootActionEnum.PrimaryAction, turret.OwnerId, out status);

            if (tick > max_tick)
            {
                tick = 0;
                gun.GunBase.CurrentAmmo = 0;
                turret.Enabled = false;
                return;
            }
            else if(status == MyGunStatusEnum.Reloading || status == MyGunStatusEnum.OutOfAmmo)
            {
                tick = 0;
                turret.Enabled = false;
                return;
            }

            if (!canShoot)
                return;

            foreach (IMyMissile missile in ScriptedMissileSession.instance.all_missiles)
            {
                if ((missile.GetPosition() - turret.GetPosition()).LengthSquared() < 4000)
                {
                    close_missiles.Add(missile);
                }
            }
        }

        public override void Close()
        {
            if (light != null)
                RemoveAPSLight();
        }
    }
}

