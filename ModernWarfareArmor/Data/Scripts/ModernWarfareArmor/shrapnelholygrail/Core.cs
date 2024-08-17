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

namespace Shrapnel
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Core : MySessionComponentBase
    {
        private Queue<ShrapnelData> queue = new Queue<ShrapnelData>();

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(int.MaxValue, ProcessDamage);
        }

        private void CreateExplosion(Vector3D myPos, float dmg, float radius)
        {
            //Explosion Damage!
            BoundingSphereD sphere = new BoundingSphereD(myPos, radius);
            MyExplosionInfo bomb = new MyExplosionInfo(dmg, dmg, sphere, MyExplosionTypeEnum.MISSILE_EXPLOSION, true, true);
            bomb.CreateParticleEffect = true;
            bomb.LifespanMiliseconds = 150 + (int)radius * 45;
            bomb.ParticleScale = 0.15f;
            MyExplosions.AddExplosion(ref bomb, true);
        }

        public void ProcessDamage(object target, ref MyDamageInformation info)
        {

            IMySlimBlock slim = target as IMySlimBlock;
            if (slim == null) return;

            if (info.Type == MyDamageType.Bullet || info.Type == MyDamageType.Rocket)
            {
                //MyLog.Default.Info($"Amount: {info.Amount}, Integrity: {slim.Integrity}, Overkill: {info.Amount - slim.Integrity}");

                if (slim.BlockDefinition.Id.SubtypeName.Contains("Armor"))
                    HandleArmorInteractions(slim, ref info);

                // bang sizzle
                if (slim.BlockDefinition.Id.SubtypeName.Contains("AmmoRack"))
                {
                    CreateExplosion(slim.FatBlock.GetPosition(), 1500f, 1f);
                    info.Amount = 0;
                }


                if (slim.Integrity <= info.Amount)
                {
                    CreateShrapnel(slim, ref info);
                    return;
                }

            }
            else if (info.Type == MyDamageType.Explosion && !(slim.FatBlock is IMyWarhead))
                ConvertDamage(slim, ref info, info.Amount);
        }

        public void HandleArmorInteractions(IMySlimBlock slim, ref MyDamageInformation info)
        {

            if (slim.BlockDefinition.Id.SubtypeName.Contains("Reactive"))
            {
                CreateExplosion(slim.FatBlock.GetPosition() + slim.FatBlock.WorldMatrix.Up, 1000f, 0.5f);
                info.Amount = slim.Integrity;
            }

            if (slim.IsFullIntegrity)
            {
                // heavy armor and ceramic at full hp takes less small arms damage
                if ((slim.BlockDefinition.Id.SubtypeName.Contains("Heavy") || slim.BlockDefinition.Id.SubtypeName.Contains("Ceramic")) && info.Amount < slim.Integrity * 0.75f)
                {
                    info.Amount = (float)Math.Max(info.Amount - 400f, 0f);
                }
            }
            // compromised ceramic, but will not shrapnel
            else if (slim.BlockDefinition.Id.SubtypeName.Contains("Ceramic"))
            {
                float invertedRatio = 1f - slim.DamageRatio;
                info.Amount = (float)Math.Max(info.Amount / invertedRatio, slim.Integrity);
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
                overkill *= 2f;

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
        }
    }

    internal class ShrapnelData
    {
        public float OverKill { get; set; }
        public List<IMySlimBlock> Neighbours { get; set; }
    }
}
