using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using System;
using System.Text;
using System.Linq;
using VRage.Utils;
using VRage.ModAPI;
using VRage.Game;
using VRage;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI;
using Sandbox.Game.Weapons;

namespace MODERN_WARFARE_GUNS
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class AutoloaderSession : MySessionComponentBase
    {
        private const int updateTicks = 1;
        private int tick = 0;

        public static AutoloaderSession instance;
        public HashSet<IMyShipMergeBlock> ammoRacks = new HashSet<IMyShipMergeBlock>();
        public HashSet<IMyShipMergeBlock> usedRacks = new HashSet<IMyShipMergeBlock>();
        public HashSet<IMyGunObject<MyGunBase>> weapons = new HashSet<IMyGunObject<MyGunBase>>();

        public override void LoadData()
        {
            MyAPIGateway.Entities.OnEntityAdd += HandleEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove += HandleEntityRemoved;

            instance = this;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= HandleEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove -= HandleEntityRemoved;

            instance = null;
        }

        
        private void HandleEntityAdded(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;

            IMyCubeGrid grid = entity as IMyCubeGrid;
            grid.OnBlockAdded += HandleBlockAdded;
            grid.OnBlockRemoved += HandleBlockRemoved;

            foreach (IMyFunctionalBlock block in grid.GetFatBlocks<IMyFunctionalBlock>())
            {
                if (block is IMyShipMergeBlock)
                    ammoRacks.Add(block as IMyShipMergeBlock);

                if (block is IMyGunObject<MyGunBase>)
                {
                    (block as IMyGunObject<MyGunBase>).GunBase.CurrentAmmo = (block as IMyGunObject<MyGunBase>).GunBase.CurrentAmmoMagazineDefinition.Capacity;
                    weapons.Add(block as IMyGunObject<MyGunBase>);

                }
            }
        }

        private void HandleEntityRemoved(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;

            IMyCubeGrid grid = entity as IMyCubeGrid;
            grid.OnBlockAdded -= HandleBlockAdded;
            grid.OnBlockRemoved -= HandleBlockRemoved;

            foreach (IMyFunctionalBlock block in grid.GetFatBlocks<IMyFunctionalBlock>())
            {
                if (block is IMyShipMergeBlock)
                    ammoRacks.Remove(block as IMyShipMergeBlock);

                if (block is IMyGunObject<MyGunBase>)
                    weapons.Remove(block as IMyGunObject<MyGunBase>);
            }
        }

        private void HandleBlockAdded(IMySlimBlock slim)
        {
            if (slim.FatBlock == null)
                return;

            IMyCubeGrid grid = slim.CubeGrid;

            if (slim.FatBlock is IMyShipMergeBlock)
                ammoRacks.Add(slim.FatBlock as IMyShipMergeBlock);
            else if (slim.FatBlock is IMyGunObject<MyGunBase>)
            {
                (slim.FatBlock as IMyGunObject<MyGunBase>).GunBase.CurrentAmmo = (slim.FatBlock as IMyGunObject<MyGunBase>).GunBase.CurrentAmmoMagazineDefinition.Capacity;
                weapons.Add(slim.FatBlock as IMyGunObject<MyGunBase>);
            }
        }

        private void HandleBlockRemoved(IMySlimBlock slim)
        {
            if (slim.FatBlock == null)
                return;

            IMyCubeGrid grid = slim.CubeGrid;

            if (slim.FatBlock is IMyShipMergeBlock)
                ammoRacks.Remove(slim.FatBlock as IMyShipMergeBlock);
            else if (slim.FatBlock is IMyGunObject<MyGunBase>)
                weapons.Remove(slim.FatBlock as IMyGunObject<MyGunBase>);
        }

        private double VectorAngleBetween(Vector3D a, Vector3D b)
        { //returns radians
          //Law of cosines to return the angle between two vectors.

            if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
        }

        private Vector3D GetExtentOfBlockWorld(IMyCubeBlock block, bool front = true)
        {
            // Get the block's size in meters
            Vector3I span = block.SlimBlock.Max - block.SlimBlock.Min;

            float blockSize = block.CubeGrid.GridSize * (float)Math.Max(Math.Abs(span.X), Math.Max(Math.Abs(span.Y), Math.Abs(span.Z))) * 0.5f;
            MyAPIGateway.Utilities.ShowNotification($"bz {blockSize}", 16);

            if (front)
                return block.GetPosition() + (block.WorldMatrix.Forward * blockSize);
            else
                return block.GetPosition() - (block.WorldMatrix.Forward * blockSize);
        }

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            //MyAPIGateway.Utilities.ShowNotification($"wepps {weapons.Count}",16);
            //MyAPIGateway.Utilities.ShowNotification($"ammoRacks {ammoRacks.Count}", 16);

            if (tick % updateTicks == 0)
            {
                usedRacks.Clear();

                //lock(weapons)
                //{
                //    lock(ammoRacks)
                //    {
                //MyAPIGateway.Parallel.ForEach(weapons, weapon =>
                foreach (IMyGunObject<MyGunBase> weapon in weapons)
                {

                    if (weapon == null || !(weapon as IMyFunctionalBlock).IsFunctional || weapon.GunBase.CurrentAmmo > 0)
                        continue;
                    //return;

                    //MyAPIGateway.Utilities.ShowNotification($"eek", 16);


                    IMyCubeBlock block = weapon as IMyCubeBlock;

                    foreach (IMyShipMergeBlock ammoRack in ammoRacks)
                    {
                        if (ammoRack == null || !ammoRack.IsFunctional || usedRacks.Contains(ammoRack))
                            continue;

                        //MyAPIGateway.Utilities.ShowNotification($"eek2", 16);

                        if (weapon is IMyLargeTurretBase && (ammoRack.GetPosition() - block.GetPosition()).LengthSquared() < 5)
                        {
                            usedRacks.Add(ammoRack);
                            weapon.GunBase.CurrentAmmo = weapon.GunBase.CurrentAmmoMagazineDefinition.Capacity;
                            //MyAPIGateway.Utilities.ShowNotification($"eek3", 16);
                            break;
                        }

                        //MyAPIGateway.Utilities.ShowNotification($"eek4", 16);

                        double distsq = (ammoRack.GetPosition() - GetExtentOfBlockWorld(block, false)).LengthSquared();

                        Vector4 red = Color.Red.ToVector4();
                        Vector4 blu = Color.Blue.ToVector4();

                        MySimpleObjectDraw.DrawLine(ammoRack.GetPosition(), GetExtentOfBlockWorld(block, false), VRage.Utils.MyStringId.GetOrCompute("Square"), ref red, 0.1f);
                        MySimpleObjectDraw.DrawLine(GetExtentOfBlockWorld(block, true), GetExtentOfBlockWorld(block, false), VRage.Utils.MyStringId.GetOrCompute("Square"), ref blu, 0.1f);

                        //MyAPIGateway.Utilities.ShowNotification($"distsq {distsq}", 16);

                        if (distsq < 1.51f && (VectorAngleBetween(ammoRack.WorldMatrix.Forward, block.WorldMatrix.Forward) < 0.3 || VectorAngleBetween(ammoRack.WorldMatrix.Backward, block.WorldMatrix.Forward) < 0.3))
                        {
                            // ammoRack.CubeGrid.RazeBlock(ammoRack.SlimBlock.Position);
                            //MyAPIGateway.Utilities.ShowNotification($"eek5", 16);
                            usedRacks.Add(ammoRack);
                            weapon.GunBase.CurrentAmmo = weapon.GunBase.CurrentAmmoMagazineDefinition.Capacity;
                            break;
                        }
                    }
                }
                //});
                //    }
                //}

                foreach (IMyShipMergeBlock ammoRack in usedRacks)
                {
                    ammoRacks.Remove(ammoRack);
                    ammoRack.CubeGrid.RazeBlock(ammoRack.SlimBlock.Position);
                }
                tick = 0;
            }
            tick++;
        }
    }
    /*
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_GunBase), false)]
    public class GunGetter : MyGameLogicComponent
    {

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            (Entity as IMyGunObject<MyGunBase>).GunBase.CurrentAmmo = (Entity as IMyGunObject<MyGunBase>).GunBase.CurrentAmmoMagazineDefinition.Capacity;
            AutoloaderSession.instance.weapons.Add(Entity as IMyGunObject<MyGunBase>);
        }

        public override void Close()
        {
            AutoloaderSession.instance.weapons.Remove(Entity as IMyGunObject<MyGunBase>);
        }
    }
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MergeBlock), false)]
    public class MergeGetter : MyGameLogicComponent
    {

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if(Entity is IMyShipMergeBlock)
                AutoloaderSession.instance.ammoRacks.Add(Entity as IMyShipMergeBlock);
        }

        public override void Close()
        {
            if (Entity is IMyShipMergeBlock)
                AutoloaderSession.instance.ammoRacks.Remove(Entity as IMyShipMergeBlock);
        }
    }*/
}