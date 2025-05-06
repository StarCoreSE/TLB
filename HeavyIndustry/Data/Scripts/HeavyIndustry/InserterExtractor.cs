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

namespace InserterExtractor

{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_CargoContainer), false, "InserterExtractor")]

	public class InserterExtractor : MyGameLogicComponent
    {
		IMyCubeBlock block;
        MyInventory inventory;
        IMySlimBlock slim;

        private IMyCubeBlock extracted;
        private MyInventory extracted_inventory;
        private IMyCubeBlock inserted;
        private MyInventory inserted_inventory;

        List<IMySlimBlock> neighbours = new List<IMySlimBlock>();
        Queue<IMySlimBlock> blocksAdded = new Queue<IMySlimBlock>();

        bool extracting = true;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
			block = (IMyCubeBlock)Entity;
            slim = block.SlimBlock;

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                block.CubeGrid.OnBlockAdded += OnBlockAdded;
                block.CubeGrid.OnBlockRemoved += OnBlockRemoved;
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME | MyEntityUpdateEnum.EACH_FRAME;
            }
        }

        public override void Close()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                block.CubeGrid.OnBlockAdded -= OnBlockAdded;
                block.CubeGrid.OnBlockRemoved -= OnBlockRemoved;
            }
        }

        private bool IsFunctional()
		{
			return block != null && !block.MarkedForClose && block.CubeGrid?.Physics != null && block.IsFunctional;
		}

        public override void UpdateOnceBeforeFrame()
        {
            if(MyAPIGateway.Multiplayer.IsServer)
            {
                inventory = block.GetInventory(0) as MyInventory;

                slim.GetNeighbours(neighbours);

                foreach (IMySlimBlock nslim in neighbours)
                {
                    blocksAdded.Enqueue(nslim);
                }
            }
        }

        public void OnBlockAdded(IMySlimBlock nslim)
        {
            blocksAdded.Enqueue(nslim);
        }

        public void OnBlockRemoved(IMySlimBlock nslim)
        {
            if (nslim.FatBlock == extracted)
            {
                extracted = null;
                extracted_inventory = null;
            }
            else if (nslim.FatBlock == inserted)
            {
                inserted = null;
                inserted_inventory = null;
            }
        }

        public void CheckLocation(IMySlimBlock nslim)
        {
            IMyCubeBlock nblock = nslim.FatBlock;

            if (nblock == null || block == null || block.CubeGrid == null)
                return;

            Vector3I dir = nslim.Position - slim.Position;

            // Convert local forward to grid/world-aligned forward
            Vector3I sorterForward = Base6Directions.GetIntVector(block.Orientation.Forward);

            int dotProduct = Vector3I.Dot(dir, sorterForward);

            //MyAPIGateway.Utilities.ShowNotification($"{dotProduct}");

            if (dotProduct > 0 && inserted == null)
            {
                var inventory = GetAppropriateInventory(nblock, false);
                if (inventory != null)
                {
                    inserted = nblock;
                    inserted_inventory = inventory;
                    return;
                }
            }
            else if (dotProduct < 0 && extracted == null)
            {
                var inventory = GetAppropriateInventory(nblock, true);
                if (inventory != null)
                {
                    extracted = nblock;
                    extracted_inventory = inventory;
                }
            }
        }

        public MyInventory GetAppropriateInventory(IMyCubeBlock nblock, bool extract)
        {
            MyInventory inventory = null;

            if(nblock is IMyProductionBlock)
            {
                IMyProductionBlock pblock = nblock as IMyProductionBlock;
                if (extract)
                    inventory = pblock.OutputInventory as MyInventory;
                else
                    inventory = pblock.InputInventory as MyInventory;
            }

            //MyAPIGateway.Utilities.ShowNotification($"{nblock.InventoryCount}");
            if(inventory == null)
                inventory = nblock.GetInventory(0) as MyInventory;

            return inventory;
        }

        public void Insert()
        {
            if (inserted == null || inserted.MarkedForClose || inserted_inventory == null)
            {
                inserted = null;
                inserted_inventory = null;
                return;
            }

            if (inserted.IsFunctional == false)
                return;

            var item = inventory.GetItemAt(0);
            if (item.HasValue)
            {
                inserted_inventory.TransferItemFrom(inventory, 0, null, true, item.Value.Amount);
            }
        }

        public void Extract()
        {
            if (extracted == null || extracted.MarkedForClose || extracted_inventory == null)
            {
                extracted = null;
                extracted_inventory = null;
                return;
            }

            if (extracted.IsFunctional == false)
                return;

            var item = extracted_inventory.GetItemAt(0);
            if (item.HasValue)
            {
                inventory.TransferItemFrom(extracted_inventory, 0, null, true, item.Value.Amount);
            }
        }
        private void DebugStatus()
        {
            string msg = $"[InserterExtractor]\n";

            msg += $"Functional: {IsFunctional()}\n";
            // msg += $"Sorter Enabled: {block?.Enabled}\n";
            msg += $"Inventory Null: {inventory == null}\n";

            if (extracted != null)
                msg += $"Extracting from: {extracted.DisplayNameText}\n";
            else
                msg += "Extracting from: null\n";

            if (inserted != null)
                msg += $"Inserting to: {inserted.DisplayNameText}\n";
            else
                msg += "Inserting to: null\n";

            if (inventory != null)
                msg += $"Sorter Items: {inventory.ItemCount}\n";

            MyAPIGateway.Utilities.ShowNotification(msg, 1600, MyFontEnum.White);
        }

        public override void UpdateAfterSimulation()
        {
            if (blocksAdded.Count > 0 && MyAPIGateway.Multiplayer.IsServer)
            {
                var nslim = blocksAdded.Dequeue();
                if (extracted == null || inserted == null)
                {
                    neighbours.Clear();
                    slim.GetNeighbours(neighbours);

                    foreach (IMySlimBlock n in neighbours)
                    {
                        if (n.Position == nslim.Position)
                        {
                            CheckLocation(nslim);
                            break;
                        }
                    }
                }
            }
        }

        public override void UpdateAfterSimulation100()
		{
            //DebugStatus();

            if (MyAPIGateway.Multiplayer.IsServer && IsFunctional() && inventory != null)
            {
                if (inventory == null)
                    inventory = block.GetInventory(0) as MyInventory;


                if(!inventory.IsFull && extracting)
                {
                    Extract();
                }

                if (!inventory.Empty() && !extracting)
                {
                    Insert();
                }

                extracting = !extracting;
            }
        }
	}
}