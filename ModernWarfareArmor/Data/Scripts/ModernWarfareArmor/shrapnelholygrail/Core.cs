using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.Game;
using System;
using System.Collections.Generic;
using VRageMath;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using SpaceEngineers.Game.ModAPI;
using System.Linq;

namespace Shrapnel
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Core : MySessionComponentBase
    {
        HashSet<ChainLocation> chainLocations = new HashSet<ChainLocation>();
        Dictionary<long, IMyAttachableTopBlock> turretTossPair = new Dictionary<long, IMyAttachableTopBlock>();

        long tick;
        private Queue<ShrapnelData> queue = new Queue<ShrapnelData>();

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(int.MaxValue, ProcessDamage);
        }

        private void CreateExplosion(Vector3D myPos, float dmg, float radius, MyEntity ownerEntity, bool silentExplosion)
        {
            //Explosion Damage!
            //BoundingSphereD sphere = GetExplosionSphere(myPos);

            MyExplosionTypeEnum explosiontype = MyExplosionTypeEnum.MISSILE_EXPLOSION;
            BoundingSphereD sphere = new BoundingSphereD(myPos, radius);

            if (silentExplosion)
            {
                //MyAPIGateway.Utilities.ShowNotification("Silent explosion", 10000);
                var explosion = new MyExplosionInfo()
                {
                    PlayerDamage = dmg,
                    Damage = dmg,
                    ExplosionSphere = sphere,
                    StrengthImpulse = 0f,
                    ExplosionFlags = MyExplosionFlags.APPLY_FORCE_AND_DAMAGE | MyExplosionFlags.APPLY_DEFORMATION | MyExplosionFlags.FORCE_CUSTOM_END_OF_LIFE_EFFECT,
                    ExplosionType = explosiontype,
                    LifespanMiliseconds = 700,
                    ObjectsRemoveDelayInMiliseconds = 0,
                    ParticleScale = 1f,
                    VoxelCutoutScale = 1f,
                    Direction = null,
                    CheckIntersections = true,
                    Velocity = Vector3.Zero,
                    OriginEntity = 0L,
                    PlaySound = false,
                    CustomEffect = "",
                    CustomSound = null,
                    KeepAffectedBlocks = false,
                    IgnoreFriendlyFireSetting = true,
                    EffectHitAngle = MyObjectBuilder_MaterialPropertiesDefinition.EffectHitAngle.None,
                    DirectionNormal = null,
                    ShouldDetonateAmmo = true,
                    AffectVoxels = false,
                };
                MyExplosions.AddExplosion(ref explosion, true);
            }
            else
            {
                //MyAPIGateway.Utilities.ShowNotification("Loud explosion", 10000);
                var explosion = new MyExplosionInfo()
                {
                    PlayerDamage = dmg,
                    Damage = dmg,
                    ExplosionSphere = sphere,
                    StrengthImpulse = 0f,
                    ExplosionFlags = MyExplosionFlags.CREATE_DEBRIS | MyExplosionFlags.AFFECT_VOXELS | MyExplosionFlags.APPLY_FORCE_AND_DAMAGE | MyExplosionFlags.CREATE_DECALS | MyExplosionFlags.CREATE_PARTICLE_EFFECT | MyExplosionFlags.APPLY_DEFORMATION, ExplosionType = explosiontype,
                    LifespanMiliseconds = 700,
                    ObjectsRemoveDelayInMiliseconds = 0,
                    ParticleScale = 1f,
                    VoxelCutoutScale = 1f,
                    Direction = null,
                    CheckIntersections = true,
                    Velocity = Vector3.Zero,
                    OriginEntity = 0L,
                    PlaySound = true,
                    CustomEffect = "",
                    CustomSound = null,
                    KeepAffectedBlocks = false,
                    IgnoreFriendlyFireSetting = true,
                    EffectHitAngle = MyObjectBuilder_MaterialPropertiesDefinition.EffectHitAngle.None,
                    DirectionNormal = null,
                    ShouldDetonateAmmo = true,
                    AffectVoxels = true,
                };
                MyExplosions.AddExplosion(ref explosion, true);
            }
            
        }   

        public class ChainLocation
        {
            public Vector3D position;
            public int ammorackCount;
            public long lastupdateTick;
            public HashSet<long> entityIds = new HashSet<long>();

            public ChainLocation(Vector3D position, int ammorackCount, int tick)
            {
                this.position = position;
                this.ammorackCount = ammorackCount;
                this.lastupdateTick = tick;
            }
        }

        public void ProcessDamage(object target, ref MyDamageInformation info)
        {
            IMySlimBlock slim = target as IMySlimBlock;
            if (slim == null) return;

            if (info.Type == MyDamageType.Bullet)
            {
                //MyAPIGateway.Utilities.ShowNotification($"Damage: {info.Amount}, {slim.BlockDefinition.Id.SubtypeName}", 500);
                if (slim.BlockDefinition.Id.SubtypeName.Contains("AdvancedTurretRotor") && (slim.Integrity - info.Amount) < slim.MaxIntegrity * 0.7)
                {
                    IMyAttachableTopBlock block = slim.FatBlock as IMyAttachableTopBlock;
                    if(block != null && block.Base != null && !turretTossPair.ContainsKey(block.EntityId))
                    {
                        turretTossPair.Add(block.EntityId, block);
                        block.Base.Detach();
                    }
                }


                // Handle special armor interactions
                if (slim.BlockDefinition.Id.SubtypeName.Contains("Armor"))
                    HandleArmorInteractions(slim, ref info);

                //MyAPIGateway.Utilities.ShowNotification($"Damage: {info.Amount}, {slim.Integrity}", 500);

                // Bullets need to damage the ammorack to 70% integrity, but rockets will always trigger a chain reaction
                if (slim.BlockDefinition.Id.SubtypeName.Contains("AmmoRack") && (slim.Integrity - info.Amount) < slim.MaxIntegrity * 0.7)
                {
                    // Find the closest chain reaction location
                    ChainLocation closestChain = null;

                    foreach (ChainLocation chainlocation in chainLocations)
                    {
                        double distance = (slim.FatBlock.WorldMatrix.Translation - chainlocation.position).Length();
                        if (distance < 50)
                        {
                            closestChain = chainlocation;
                        }
                    }

                    // If close to an existing chain reaction, increase its power
                    if (closestChain != null && closestChain.ammorackCount < 100 && !closestChain.entityIds.Contains(slim.FatBlock.EntityId))
                    {
                        closestChain.ammorackCount ++;
                        closestChain.lastupdateTick = tick;
                        closestChain.entityIds.Add(slim.FatBlock.EntityId);
                        closestChain.position = (closestChain.position * (closestChain.ammorackCount - 1) + slim.FatBlock.WorldMatrix.Translation)/closestChain.ammorackCount;
                        // Silent explosion logic here
                        CreateExplosion(slim.FatBlock.WorldMatrix.Translation, 250, 10, null, true);
                    }
                    // If no nearby chain reaction, start a new one
                    else if (closestChain == null)
                    {
                        closestChain = new ChainLocation(slim.FatBlock.WorldMatrix.Translation, 0, 100);
                        chainLocations.Add(closestChain);
                        closestChain.ammorackCount++;
                        closestChain.lastupdateTick = tick;
                        closestChain.entityIds.Add(slim.FatBlock.EntityId);
                        closestChain.position = slim.FatBlock.WorldMatrix.Translation;
                        // Silent explosion logic here
                        CreateExplosion(slim.FatBlock.WorldMatrix.Translation, 250, 10, null, true);
                    }

                    // Ensure the block is destroyed
                    //info.Amount = slim.Integrity;
                    slim.FatBlock.CubeGrid.RazeBlock(slim.Position);
                    slim.FatBlock.Close();
                }

                // Create shrapnel if the block is destroyed
                if (slim.Integrity <= info.Amount)
                {
                    CreateShrapnel(slim, ref info);
                    return;
                }
            }
            else if (info.Type == MyDamageType.Explosion && !(slim.FatBlock is IMyWarhead))
            {
                // Handle explosion damage, with special multiplier for XL blocks
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
                CreateExplosion(slim.FatBlock.WorldMatrix.Translation + slim.FatBlock.WorldMatrix.Up, 2000f, 0.5f, slim.FatBlock as MyEntity,false);
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
            //MyAPIGateway.Utilities.ShowNotification($"Chain locations: {chainLocations.Count}", 16);
            tick++;
            int tasks = 0;
            while (queue.Count > 0 && tasks < 200)
            {
                tasks++;
                HandleShrapnel();
            }

            if (chainLocations.Count > 0)
            {
                List<ChainLocation> explosionsToTrigger = new List<ChainLocation>();

                foreach (ChainLocation chain in chainLocations)
                {
                    if (tick - chain.lastupdateTick > 10)
                    {
                        explosionsToTrigger.Add(chain);
                    }
                }

                foreach (ChainLocation chain in explosionsToTrigger)
                {
                    float totalDamage = 200f + (50 * chain.ammorackCount);
                    float totalRadius = 1.5f * chain.ammorackCount;
                    CreateExplosion(chain.position, totalDamage, totalRadius, null,false);
                    //MyAPIGateway.Utilities.ShowNotification($"Chain reaction with {chain.ammorackCount} ammo racks", 5000);
                    chainLocations.Remove(chain);
                }
                explosionsToTrigger.Clear();
            }

            foreach (IMyAttachableTopBlock block in turretTossPair.Values.ToList())
            {
                if(block.Base == null)
                {
                    IMyCubeGrid grid = block.CubeGrid;
                    MatrixD gridMatrix = grid.WorldMatrix;
                    gridMatrix.Translation = grid.WorldMatrix.Translation + block.WorldMatrix.Up * 0.5;
                    grid.WorldMatrix = gridMatrix;
                    grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, block.WorldMatrix.Up * 1e7, grid.Physics.CenterOfMassWorld, null);
                    turretTossPair.Remove(block.EntityId);
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
