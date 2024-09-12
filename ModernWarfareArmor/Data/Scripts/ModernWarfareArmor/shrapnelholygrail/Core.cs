using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.Game;
using System;
using System.Collections.Generic;
using System.IO;
using VRageMath;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using System.Collections;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Entities.Debris;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI;
using VRage.ModAPI;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.Weapons;

namespace Shrapnel
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Core : MySessionComponentBase
    {
        HashSet<ChainLocation> chainLocations = new HashSet<ChainLocation>();
        int tick;
        private Queue<ShrapnelData> queue = new Queue<ShrapnelData>();

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(int.MaxValue, ProcessDamage);
        }

        private void CreateExplosion(Vector3D myPos, float dmg, float radius, MyEntity ownerEntity)
        {
            //Explosion Damage!
            //BoundingSphereD sphere = GetExplosionSphere(myPos);
            BoundingSphereD sphere = new BoundingSphereD(myPos, radius);
            MyExplosionInfo bomb = new MyExplosionInfo(dmg, dmg, sphere, MyExplosionTypeEnum.MISSILE_EXPLOSION, true, true);
            bomb.CreateParticleEffect = true;
            bomb.LifespanMiliseconds = 150 + (int)radius * 45;
            bomb.ParticleScale = 0.30f * radius;
            bomb.OwnerEntity = ownerEntity;

            MyExplosions.AddExplosion(ref bomb, true);
        }   

        public class ChainLocation
        {
            public Vector3D pt;
            public int xp;
            public int duration;

            public ChainLocation(Vector3D pt, int xp, int duration)
            {
                this.pt = pt;
                this.xp = xp;
                this.duration = duration;
            }
        }

        public void ProcessDamage(object target, ref MyDamageInformation info)
        {

            IMySlimBlock slim = target as IMySlimBlock;
            if (slim == null) return; // || info.Type == MyDamageType.Deformation


            if (info.Type == MyDamageType.Bullet || info.Type == MyDamageType.Rocket)
            {
                //MyLog.Default.Info($"Amount: {info.Amount}, Integrity: {slim.Integrity}, Overkill: {info.Amount - slim.Integrity}");

                if (slim.BlockDefinition.Id.SubtypeName.Contains("Armor"))
                    HandleArmorInteractions(slim, ref info);


                // bang sizzle
                if (slim.BlockDefinition.Id.SubtypeName.Contains("AmmoRack") && slim.Integrity - info.Amount < slim.Integrity * 0.7)
                {

                    ChainLocation closestChain = null;
                    double closestDistance = 999999;

                    // iterate throuuh all chain locs
                    foreach(ChainLocation chainLoc in chainLocations)
                    {
                        double dist = (slim.FatBlock.WorldMatrix.Translation - chainLoc.pt).Length();
                        if(dist < closestDistance && dist < 100)
                        {
                            closestDistance = dist;
                            closestChain = chainLoc;
                        }
                    }

                    // if we are close enough
                    if(closestChain != null && closestChain.xp < 10)
                    {
                        closestChain.xp += 1;
                        CreateExplosion(slim.FatBlock.WorldMatrix.Translation, 1500f + 100f * closestChain.xp, 1f + 1f * closestChain.xp, null);
                        //MyAPIGateway.Utilities.ShowNotification($"bang, {closestChain.xp}!", 3000);
                    }
                    // or if we are alone, add
                    else if (closestChain == null)
                    {
                        chainLocations.Add(new ChainLocation(slim.FatBlock.WorldMatrix.Translation, 0, 100));
                        CreateExplosion(slim.FatBlock.WorldMatrix.Translation, 1500f, 1f, null);
                        //MyAPIGateway.Utilities.ShowNotification($"nope!", 3000);
                    }

                    info.Amount = slim.Integrity;

                }

                if (slim.Integrity <= info.Amount)
                {
                    CreateShrapnel(slim, ref info);
                    return;
                }

            }
            else if (info.Type == MyDamageType.Explosion && !(slim.FatBlock is IMyWarhead))
            {
                if (slim.BlockDefinition.Id.SubtypeName.Contains("XL"))
                    ConvertDamage(slim, ref info, 5 * info.Amount);
                else
                    ConvertDamage(slim, ref info, info.Amount);

            }
        }
        public BoundingSphereD GetExplosionSphere(Vector3D myPos)
        {
            float radiusToCheckForAmmoBlock = 0.25f; // Static value for range of ammo racks that will be added to the explosion
            float explosionDamageConstant = 1f; // Value for the damage of the explosion
            float newExplosionRadius = 0; // Radius of the explosion


            BoundingSphereD ammoBlockDetectionSphere = new BoundingSphereD(myPos, radiusToCheckForAmmoBlock);
            List<MyEntity> EntitiesInSideAmmoBlockChecker = new List<MyEntity>();
            MyGamePruningStructure.GetAllEntitiesInSphere(ref ammoBlockDetectionSphere, EntitiesInSideAmmoBlockChecker);
            foreach (MyEntity Entity in EntitiesInSideAmmoBlockChecker)
            {
                if (Entity is IMyShipMergeBlock && (Entity as IMyShipMergeBlock).SlimBlock.BlockDefinition.Id.SubtypeName.Contains("AmmoRack"))
                {
                    IMySlimBlock slimBlock = (Entity as IMyShipMergeBlock).SlimBlock;

                    newExplosionRadius += (float)Math.Pow(explosionDamageConstant, 2.5);
                    slimBlock.CubeGrid.RazeBlock(slimBlock.Position); // Destroy the ammo rack
                }
            }
            //MyAPIGateway.Utilities.ShowNotification("Explosion Radius: " + newExplosionRadius.ToString(), 10000);
            return new BoundingSphereD(myPos, newExplosionRadius);
        }

        public void HandleArmorInteractions(IMySlimBlock slim, ref MyDamageInformation info)
        {
            if (slim.BlockDefinition.Id.SubtypeName.Contains("Reactive"))
            {
                CreateExplosion(slim.FatBlock.WorldMatrix.Translation + slim.FatBlock.WorldMatrix.Up, 2000f, 0.5f, slim.FatBlock as MyEntity);
                info.Amount = slim.Integrity;
            }
            else if(slim.BlockDefinition.Id.SubtypeName.Contains("Heavy") && info.Amount < slim.Integrity * 0.75f ) 
            {
                info.Amount = (float)Math.Max(info.Amount - 200f * slim.Integrity / slim.MaxIntegrity, 0f);

                /*
                MyMissileAmmoDefinition missileProps = (MyAPIGateway.Entities.GetEntityById(info.AttackerId) as IMyGunObject<MyGunBase>)?.GunBase?.CurrentAmmoDefinition as MyMissileAmmoDefinition;
                if(missileProps == null)
                {
                    MyAPIGateway.Utilities.ShowNotification($"{MyAPIGateway.Entities.GetEntityById(info.AttackerId) == null}, " +
                        $"{MyAPIGateway.Entities.GetEntityById(info.AttackerId) as IMyGunObject<MyGunBase> == null}, " +
                        $"{(MyAPIGateway.Entities.GetEntityById(info.AttackerId) as IMyGunObject<MyGunBase>)?.GunBase?.CurrentAmmoDefinition as MyMissileAmmoDefinition == null}", 10000);
                }*/
            }
            /*
            if (slim.IsFullIntegrity)
            {
                // heavy armor and ceramic at full hp takes less small arms damage
                if (slim.BlockDefinition.Id.SubtypeName.Contains("Ceramic") && info.Amount < slim.Integrity * 0.75f)
                {
                    MyAPIGateway.Utilities.ShowNotification($"hmm {info.Amount}", 10000);
                    info.Amount = (float)Math.Max(info.Amount - 400f, 0f);
                }
            }
            // compromised ceramic, but will not shrapnel
            else */if (slim.BlockDefinition.Id.SubtypeName.Contains("Ceramic"))
            {
                //MyAPIGateway.Utilities.ShowNotification($"hmm {info.Amount}", 10000);
                info.Amount = (float)Math.Max(info.Amount * slim.MaxIntegrity / slim.Integrity, slim.Integrity);
            }
        }

        public void ConvertDamage(IMySlimBlock slim, ref MyDamageInformation info, float damage)
        {
            queue.Enqueue(new ShrapnelData()
            {
                OverKill = damage,
                Neighbours = new List<IMySlimBlock>() { slim },
            });
            info.Amount = 0;
        }

        public void CreateShrapnel(IMySlimBlock slim, ref MyDamageInformation info)
        {
            float overkill = info.Amount - slim.Integrity;

            if (slim.BlockDefinition.Id.SubtypeName.Contains("Heavy"))
                overkill = overkill * 2f + 200;

            info.Amount = slim.Integrity;

            List<IMySlimBlock> n = new List<IMySlimBlock>();
            slim.GetNeighbours(n);

            queue.Enqueue(new ShrapnelData()
            {
                Neighbours = n,
                OverKill = overkill
            });
        }

        public void HandleShrapnel()
        {
            ShrapnelData data = queue.Dequeue();
            float count = 1f / data.Neighbours.Count;

            //MyLog.Default.Info($"queue: {queue.Count}, neighbour: {tasks}, overkill: {data.OverKill}, spread: {data.OverKill * count}");
            foreach (IMySlimBlock neighbour in data.Neighbours)
            {
                if (neighbour == null) continue;


                float generalMult = 1f;
                if (neighbour.BlockDefinition is MyCubeBlockDefinition)
                {
                    
                    // light resists spalling
                    if (neighbour.BlockDefinition.Id.SubtypeName.Contains("Light"))
                        generalMult *= 0.25f;

                    generalMult = ((MyCubeBlockDefinition)neighbour.BlockDefinition).GeneralDamageMultiplier;
                    if (generalMult > 1f)
                    {
                        generalMult = 1f / generalMult;
                    }
                }

                neighbour.DoDamage(data.OverKill * count * generalMult, MyDamageType.Bullet, true);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            int tasks = 0;
            while (queue.Count > 0 && tasks < 200)
            {
                tasks++;
                HandleShrapnel();
            }

            if(chainLocations.Count > 1)
            {
                tick++;

                if(tick > 100)
                {
                    chainLocations.Clear();
                    tick = 0;
                }
            }
        }
    }

    internal class ShrapnelData
    {
        public float OverKill { get; set; }
        public List<IMySlimBlock> Neighbours { get; set; }
    }
}
