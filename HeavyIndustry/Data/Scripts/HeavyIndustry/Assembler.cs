using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using System;
using VRage.Utils;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRageMath;
using System.Collections.Generic;
using Sandbox.Game;
using VRage.Game;

namespace Assembler

{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Assembler), false)]

	public class Assembler1Queue : MyGameLogicComponent
    {
		IMyAssembler block;
        MyInventory inventory;
        IMySlimBlock slim;

        List<MyProductionQueueItem> queue = new List<MyProductionQueueItem>();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
			block = (IMyAssembler)Entity;
            slim = block.SlimBlock;

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_FRAME;
            }
        }

        private bool IsFunctional()
		{
			return block != null && !block.MarkedForClose && block.CubeGrid?.Physics != null && block.IsFunctional;
		}

        public override void UpdateAfterSimulation10()
		{
            if (!IsFunctional()) { return; }

            if(!block.Repeating) { block.Repeating = true; }

            if (!block.IsQueueEmpty)
            {
                queue = block.GetQueue();

                if(queue != null && queue.Count > 1)
                {
                    for(int i = 1; i < queue.Count; i++)
                    {
                        block.RemoveQueueItem(i, queue[i].Amount);
                    }
                }
            }
        }
	}
}