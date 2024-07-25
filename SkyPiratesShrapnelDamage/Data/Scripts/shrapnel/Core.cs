﻿using Sandbox.Definitions;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.IO;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;

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

        public void ProcessDamage(object target, ref MyDamageInformation info)
        {
            IMySlimBlock slim = target as IMySlimBlock;
            if (slim == null) return;

            if (info.Type == MyDamageType.Deformation && slim.CubeGrid.GridSizeEnum == MyCubeSize.Large) 
            {
                info.Amount = 0;
                return;
            }

            if (info.Type == MyDamageType.Bullet || info.Type == MyDamageType.Rocket)
            {
                //MyLog.Default.Info($"Amount: {info.Amount}, Integrity: {slim.Integrity}, Overkill: {info.Amount - slim.Integrity}");

                if (slim.Integrity >= info.Amount) return;

                float overkill = info.Amount - slim.Integrity;
                info.Amount = slim.Integrity;

                List<IMySlimBlock> n = new List<IMySlimBlock>();
                slim.GetNeighbours(n);

                queue.Enqueue(new ShrapnelData()
                {
                    Neighbours = n,
                    OverKill = overkill
                });
            }
            else if (info.Type == MyDamageType.Explosion && !(slim.FatBlock is IMyWarhead))
            {
                queue.Enqueue(new ShrapnelData()
                {
                    OverKill = info.Amount,
                    Neighbours = new List<IMySlimBlock>() { slim },
                });
                info.Amount = 0;

            }

        }

        public override void UpdateBeforeSimulation()
        {
            int tasks = 0;
            while (queue.Count > 0 && tasks < 200)
            {
                tasks++;
                ShrapnelData data = queue.Dequeue();
                float count = 1f / data.Neighbours.Count;

                //MyLog.Default.Info($"queue: {queue.Count}, neighbour: {tasks}, overkill: {data.OverKill}, spread: {data.OverKill * count}");
                foreach (IMySlimBlock neighbour in data.Neighbours)
                {
                    if (neighbour == null) continue;


                    float generalMult = 1f;
                    if (neighbour.BlockDefinition is MyCubeBlockDefinition)
                    {
                        generalMult = ((MyCubeBlockDefinition)neighbour.BlockDefinition).GeneralDamageMultiplier;
                        if (generalMult > 1f) 
                        {
                            generalMult = 1f / generalMult;
                        }
                    }

                    neighbour.DoDamage(data.OverKill * count * generalMult, MyDamageType.Bullet, true);
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
