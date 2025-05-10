using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using System;
using VRageMath;
using System.Collections.Generic;
using Sandbox.Game;
using VRage.Game;
using VRage;
using VRage.Game.Entity;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Linq;
using VRage.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems.Conveyors;


namespace InserterExtractor

{

    public class GridInventories
    {
        IMyCubeGrid grid;
        public List<IMyTerminalBlock> inventory_blocks = new List<IMyTerminalBlock>();
        
        public GridInventories(IMyCubeGrid grid)
        {
            this.grid = grid;

            grid.OnBlockAdded += OnBlockAdded;
            grid.OnBlockRemoved += OnBlockRemoved;

            grid.OnGridMerge += HandleSplitMerge;
            grid.OnGridSplit += HandleSplitMerge;
            grid.OnMarkForClose += OnClose;

            Recalculate();
        }

        public void OnBlockAdded(IMySlimBlock slim)
        {
            if(slim?.FatBlock != null && slim?.FatBlock.GetInventory(0) != null)
            {
                inventory_blocks.Add(slim.FatBlock as IMyTerminalBlock);
            }
        }

        public void OnBlockRemoved(IMySlimBlock slim)
        {
            if (slim?.FatBlock != null && slim?.FatBlock.GetInventory(0) != null)
            {
                inventory_blocks.Remove(slim.FatBlock as IMyTerminalBlock);
            }
        }

        public void HandleSplitMerge(IMyCubeGrid him, IMyCubeGrid her)
        {
            //dinnae care laddy
            Recalculate();
        }

        public void Recalculate()
        {
            inventory_blocks.Clear();

            var ts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);

            if (ts != null)
            {
                ts.GetBlocksOfType<IMyTerminalBlock>(inventory_blocks, block => block.HasInventory && block.GetInventory(0) != null);
            }
        }

        public void OnClose(IMyEntity ent)
        {
            inventory_blocks.Clear();

            grid.OnBlockAdded -= OnBlockAdded;
            grid.OnBlockRemoved -= OnBlockRemoved;

            grid.OnGridMerge -= HandleSplitMerge;
            grid.OnGridSplit -= HandleSplitMerge;
            grid.OnClose -= OnClose;
        }
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)] // No continuous updates needed for this component
    public class InserterSessionComponent : MySessionComponentBase
    {

        public static InserterSessionComponent instance;

        public static Dictionary<long, GridInventories> inserter_grids = new Dictionary<long, GridInventories>();

        public override void LoadData()
        {
            base.LoadData();

            instance = this;
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
        }

        public void OnEntityAdd(IMyEntity entity)
        {
            IMyCubeGrid grid = entity as IMyCubeGrid;
            string data;
            if (grid != null && grid.Physics != null && !inserter_grids.ContainsKey(grid.EntityId))
            {
                inserter_grids.Add(grid.EntityId, new GridInventories(grid));
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;

            inserter_grids.Clear();
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Collector), false, "InserterExtractor")]

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

        GridInventories gint;

        bool extracting = true;

        int tier = 1;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
			block = (IMyCubeBlock)Entity;
            slim = block.SlimBlock;
            tier = GetTier();

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
                if (inventory != null)
                    inventory.ContentsAdded -= OnInventoryAdded;
            }
        }

        public int GetTier()
        {
            MyDefinitionBase def = block.SlimBlock.BlockDefinition;

            var subtypeName = def.Id.SubtypeName;

            if (subtypeName.Contains("T2"))
                return 2;
            else if (subtypeName.Contains("T3"))
                return 3;
            else if (subtypeName.Contains("T4"))
                return 4;

            return 1;
        }

        private bool IsFunctional()
		{
			return block != null && !block.MarkedForClose && block.CubeGrid?.Physics != null && block.IsFunctional && (block as IMyFunctionalBlock).Enabled;
		}

        public override void UpdateOnceBeforeFrame()
        {
            if(MyAPIGateway.Multiplayer.IsServer)
            {
                inventory = block.GetInventory(0) as MyInventory;

                InserterSessionComponent.inserter_grids.TryGetValue(block.CubeGrid.EntityId, out gint);

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

        public void OnInventoryAdded(MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            if(inserted == null)
            {
                InsertPushConveyor();
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


        public void InsertPushConveyor()
        {
            var item = inventory.GetItemAt(0);

            if (item.HasValue)// && (block as VRage.Game.ModAPI.Ingame.IMyInventoryOwner).UseConveyorSystem)
            {
                var amount = item.Value.Amount;

                //MyAPIGateway.Parallel.ForEach(gint.inventory_blocks.ToList(), term =>
                //{

                //    if(item != inventory.GetItemAt(0)) { return; }

                //    if (item == null || item.Value.Amount == 0 || term == null || !term.IsFunctional || !(term as IMyFunctionalBlock).Enabled) { return; }

                //    MyLog.Default.WriteLineAndConsole($"woweee, success {item.Value.Type} {item.Value.Amount}");

                //    var term_inventory = GetAppropriateInventory(term, false);
                //    if (term_inventory != null)
                //    {
                //        var transfer_amount = (VRage.MyFixedPoint)Math.Min((int)item.Value.Amount, 50);
                //        term_inventory.TransferItemFrom(inventory, 0, null, true, transfer_amount);
                //    }
                //});

                foreach(IMyTerminalBlock term in gint.inventory_blocks)
                {

                    if (item == null || item.Value.Amount == 0 || term == null || !term.IsFunctional) { continue; }

                    MyLog.Default.WriteLineAndConsole($"woweee, success {item.Value.Type} {item.Value.Amount}");

                    var term_inventory = GetAppropriateInventory(term, false);
                    if (term_inventory != null)
                    {
                        var transfer_amount = (VRage.MyFixedPoint)Math.Min((int)item.Value.Amount / 10, 50);
                        term_inventory.TransferItemFrom(inventory, 0, null, true, transfer_amount);
                    }
                }
            }
        }

        public void InsertPushConveyorLegacy()
        {
            var item = inventory.GetItemAt(0);

            if (item.HasValue)// && (block as VRage.Game.ModAPI.Ingame.IMyInventoryOwner).UseConveyorSystem)
            {

                var amount = item.Value.Amount;
                VRage.MyFixedPoint transferred_amount;

                block.CubeGrid.ConveyorSystem.PushGenerateItem(item.Value.Type, amount, out transferred_amount, block as IMyEntity, false);
                inventory.RemoveItems(item.Value.ItemId, transferred_amount);
                //MyAPIGateway.Utilities.ShowNotification($"SQUAWK : {item.Value.Type} {transferred_amount}");
            }
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
                var amount = (VRage.MyFixedPoint)((int)item.Value.Amount / 2);

                if (inserted is IMyAssembler || inserted is IMyShipWelder)
                {
                    var inserted_amount = (int)inserted_inventory.GetItemAmount((MyDefinitionId)item.Value.Type);

                    //MyAPIGateway.Utilities.ShowNotification($"ee {inserted_amount}");

                    if (inserted_amount > 50)
                        amount = 0;
                    else
                        amount = (VRage.MyFixedPoint)Math.Min((int)item.Value.Amount, 50 - inserted_amount);
                }

                inserted_inventory.TransferItemFrom(inventory, 0, null, true, amount);
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

                InserterSessionComponent.inserter_grids.TryGetValue(block.CubeGrid.EntityId, out gint);

                if (!inventory.IsFull && extracting)
                {
                    Extract();
                }

                if (!inventory.Empty() && !extracting)
                {
                    Insert();
                    InsertPushConveyor();
                }

                extracting = !extracting;
            }
        }
	}
}