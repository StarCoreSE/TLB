using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRage.ModAPI;

namespace HeavyIndustry                                   
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Refinery), false)]
    class RefineryTierConstraint : InventoryTierConstraint { };

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Assembler), false)]
    class AssemblerTierConstraint : InventoryTierConstraint { };

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CargoContainer), false)]
    class CargoTierConstraint : InventoryTierConstraint { };

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Collector), false)]
    class CollectorTierConstraint : InventoryTierConstraint { };

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipConnector), false)]
    class ConnectorTierConstraint : InventoryTierConstraint { };

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipWelder), false)]
    class WelderTierConstraint : InventoryTierConstraint { };

    class InventoryTierConstraint : MyGameLogicComponent
    {

        string restricted_icon = "Lock.png";

        IMyCubeBlock block;
        MyInventory inventory;
        MyInventoryConstraint og_constraint;

        bool constrained = false;

        int tier = 1;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {

            block = Entity as MyCubeBlock;
            tier = GetTier();

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;


        }

        public override void UpdateOnceBeforeFrame()
        {
            inventory = GetAppropriateInventory(block);

            if (inventory != null)
            {
                og_constraint = inventory.Constraint;
                inventory.InventoryContentChanged += HandleConstraints;
            }
        }

        public override void Close()
        {
            if (inventory != null)
            {
                inventory.InventoryContentChanged -= HandleConstraints;
            }
        }

        public void HandleConstraints(MyInventoryBase myInventory, MyPhysicalInventoryItem itemChanged, MyFixedPoint amount)
        {
            int count = inventory.GetItemsCount();

            if (!constrained && count >= tier + 1)
            {
                MyInventoryConstraint constraint = new MyInventoryConstraint(MyStringId.GetOrCompute($"T{tier} inventories can only hold {tier + 1} item types."), MyModContext.BaseGame.ModPath + "\\Textures\\Gui\\Icons\\" + restricted_icon);

                foreach(MyPhysicalInventoryItem item in inventory.GetItems())
                {
                    constraint.Add(new MyDefinitionId(item.Content.TypeId, item.Content.SubtypeId));
                }

                inventory.Constraint = constraint;

                constrained = true;
            }

            if(constrained && count < tier + 1)
            {
                inventory.Constraint = og_constraint;
                constrained = false;
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

        public MyInventory GetAppropriateInventory(IMyCubeBlock nblock)
        {
            MyInventory inventory = null;

            if (nblock is IMyProductionBlock)
            {
                IMyProductionBlock pblock = nblock as IMyProductionBlock;
                inventory = pblock.InputInventory as MyInventory;
            }

            //MyAPIGateway.Utilities.ShowNotification($"{nblock.InventoryCount}");
            if (inventory == null)
                inventory = nblock.GetInventory(0) as MyInventory;

            return inventory;
        }

    }
}
