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

namespace Cython.GuidedMissiles
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
	public class GuidedMissileSystem: MySessionComponentBase
	{
        private class MissileGuider
        {
            public IMyMissile missile;
            public IMyEntity target;
            private GuidedMissileSystem system; // Reference to GuidedMissileSystem

            private Vector3 lastLeadPosition = Vector3.Zero;

            private float time = 0;

            private const float ticktime = 1f / 60f;

            Vector3 direction = Vector3.Zero;
            Vector3 velocity = Vector3.Zero;

            Vector3 lastTargetPosition = Vector3.Zero;

            Vector3 position = Vector3.Zero;

            public MissileGuider(IMyMissile missile, IMyEntity target, GuidedMissileSystem system)
            {
                this.missile = missile;
                this.target = target;
                this.system = system;   

                direction = this.missile.WorldMatrix.Forward;

                position = this.missile.GetPosition();

                lastTargetPosition = target.GetPosition();
                lastLeadPosition = position + direction * 10000;


                UpdateVelocity();
            }

            private double VectorAngleBetween(Vector3D a, Vector3D b)
            { //returns radians
              //Law of cosines to return the angle between two vectors.

                if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
                    return 0;
                else
                    return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
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
                    double flareAngle = VectorAngleBetween(Vector3.Normalize(dist), direction);
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

                double angle = VectorAngleBetween(Vector3.Normalize(deltaPosition), direction);
                if (angle > 1.00)
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

            public void Update()
            {
                UpdateDirection();

                UpdateVelocity();

                UpdateTransform();

                time += ticktime;
            }
            public void UpdateVelocity()
            {
                MyMissileAmmoDefinition ammo = (MyMissileAmmoDefinition)missile.AmmoDefinition;
                float speed = Math.Min(ammo.MissileInitialSpeed + ammo.MissileAcceleration * time, ammo.DesiredSpeed);
                velocity = direction * speed;

                if (missile.Physics != null)
                    missile.Physics.LinearVelocity = velocity;
            }


            public void UpdateDirection()
            {
                Vector3 predictionPosition = GetTargetPrediction();

                Vector3 predictionDirection = predictionPosition - missile.GetPosition();
                predictionDirection.Normalize();

                double angle = Math.Acos(Math.Max(Math.Min(Vector3.Dot(direction, predictionDirection), 1), -1)) / 50;

                Vector3 rotationAxis = Vector3.Cross(direction, predictionDirection);
                rotationAxis.Normalize();

                if (!target.MarkedForClose && target.Parent != null)
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
        }

        Dictionary<long, MissileGuider> missileGuiders = new Dictionary<long, MissileGuider>();
        Dictionary<long, IMyMissile> flares = new Dictionary<long, IMyMissile>();

        public override void BeforeStart()
        {
            MyAPIGateway.Missiles.OnMissileAdded += OnMissileAdded;
            MyAPIGateway.Missiles.OnMissileRemoved += OnMissileRemoved;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Missiles.OnMissileAdded -= OnMissileAdded;
            MyAPIGateway.Missiles.OnMissileRemoved -= OnMissileRemoved;
        }

        private void OnMissileAdded(IMyMissile missile)
        {
            if(IsFlare(missile))
            {
                flares.Add(missile.EntityId, missile);
            }
            else if (IsSmoke(missile))
            {
                DispelTargetLocks(missile.LauncherId);
            }

            if (!IsMissile(missile))
                return;

            IMyEntity target = null;

            List<IMyPlayer> myPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(myPlayers, (player) => player.IdentityId == missile.Owner || player.Controller?.ControlledEntity?.Entity?.EntityId == missile.LauncherId);

            if(myPlayers.Count > 0 && target == null)
            {
                IMyPlayer player = myPlayers.First();

                target = player?.Character?.Components?.Get<MyTargetLockingComponent>()?.TargetEntity;
            }

            if (target == null)
            {
                IMyEntity entity = MyAPIGateway.Entities.GetEntityById(missile.LauncherId);
                if (entity != null)
                {
                    IMyLargeTurretBase launcher = entity as IMyLargeTurretBase;
                    
                    if(launcher != null)
                    {
                        Sandbox.ModAPI.Ingame.MyDetectedEntityInfo info = launcher.GetTargetedEntity();
                        if (!info.IsEmpty() && info.EntityId != 0)
                        {
                            target = MyAPIGateway.Entities.GetEntityById(info.EntityId);
                        }
                    }
                }
                
            }

            IMyCubeGrid targetGrid = target as IMyCubeGrid;
            if (targetGrid is IMyCubeGrid)
            {
                target = targetGrid.GetFatBlocks<IMyThrust>().Count() > 0 ? targetGrid.GetFatBlocks<IMyThrust>().First() : null;

                if(target == null)
                    target = targetGrid.GetFatBlocks<IMyTerminalBlock>().Count() > 0 ? targetGrid.GetFatBlocks<IMyTerminalBlock>().First() : null;

                
            }

            if(target != null)
            {
                missile.Synchronized = true;
                missileGuiders.Add(missile.EntityId, new MissileGuider(missile, target, this));
            }
        }

        private void DispelTargetLocks(long launcherEntId)
        {
            IMyCubeGrid grid = (MyAPIGateway.Entities.GetEntityById(launcherEntId) as IMyCubeBlock)?.CubeGrid;

            if (grid == null) return;

            List<IMyPlayer> myPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(myPlayers, (player) => player?.Character?.Components?.Get<MyTargetLockingComponent>()?.Target == grid);

            foreach(IMyPlayer player in myPlayers)
            {
                player.Character.Components.Get<MyTargetLockingComponent>().ReleaseTargetLock();
            }
        }

        private bool IsMissile(IMyMissile missile)
        {
            //MyAPIGateway.Utilities.ShowNotification($"ismissile : {missile.AmmoDefinition.Id.SubtypeName} : {missile.AmmoDefinition.Id.SubtypeName.Contains("Missile")}");
            if (missile.AmmoDefinition.Id.SubtypeName.Contains("Missile"))
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
            if (flares.ContainsKey(missile.EntityId))
                flares.Remove(missile.EntityId);

            if (missileGuiders.ContainsKey(missile.EntityId))
                missileGuiders.Remove(missile.EntityId);
        }

        public override void UpdateBeforeSimulation()
        {
            foreach (var missileGuider in missileGuiders)
            {
                missileGuider.Value.Update();
            }
        }
    }
}

