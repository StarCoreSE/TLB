using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace wala
{

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class TurretControllerSession : MySessionComponentBase
    {
        public static Dictionary<long, IMyTurretControlBlock> ctcs = new Dictionary<long, IMyTurretControlBlock>();

        public override void LoadData()
        {
            MyAPIGateway.Entities.OnEntityAdd += HandleEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove += HandleEntityRemoved;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= HandleEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove -= HandleEntityRemoved;
            ctcs.Clear();
        }

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            foreach (IMyTurretControlBlock ctc in ctcs.Values)
            {
                if (ctc == null || ctc.MarkedForClose)
                    continue;

                if (ctc.AIEnabled && !ctc.SlimBlock.BlockDefinition.Id.SubtypeName.Contains("Large"))
                    ctc.AIEnabled = false;
            }
        }

        public static void HandleEntityAdded(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;

            IMyCubeGrid grid = entity as IMyCubeGrid;
            grid.OnBlockAdded += HandleBlockAdded;
            grid.OnBlockRemoved += HandleBlockRemoved;

            foreach (IMyTurretControlBlock ctc in grid.GetFatBlocks<IMyTurretControlBlock>())
            {
                if (!ctcs.ContainsKey(ctc.EntityId))
                {
                    ctcs.Add(ctc.EntityId, ctc);
                }
            }
        }

        public static void HandleEntityRemoved(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;

            IMyCubeGrid grid = entity as IMyCubeGrid;
            grid.OnBlockAdded -= HandleBlockAdded;
            grid.OnBlockRemoved -= HandleBlockRemoved;

            foreach (IMyTurretControlBlock ctc in grid.GetFatBlocks<IMyTurretControlBlock>())
            {
                if (ctcs.ContainsKey(ctc.EntityId))
                {
                    ctcs.Remove(ctc.EntityId);
                }
            }
        }

        public static void HandleBlockAdded(IMySlimBlock slim)
        {
            if (slim.FatBlock == null || !(slim.FatBlock is IMyTurretControlBlock))
                return;

            ctcs.Add(slim.FatBlock.EntityId, slim.FatBlock as IMyTurretControlBlock);
        }

        public static void HandleBlockRemoved(IMySlimBlock slim)
        {
            if (slim.FatBlock == null || !(slim.FatBlock is IMyTurretControlBlock))
                return;

            ctcs.Remove(slim.FatBlock.EntityId);
        }
    }
}