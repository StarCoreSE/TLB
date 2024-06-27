using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
 
namespace Digi
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SmallMissileLauncherReload), false, "MountedLargeBomb", "MountedGlideBomb", "MountedMediumBomb", "MountedMissile")]
    public class MountedBomb : MyGameLogicComponent
    {
        private IMyFunctionalBlock block;
        private IMyGunObject<MyGunBase> gun;
        private long lastShotTime;
 
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }
 
        public override void UpdateOnceBeforeFrame()
        {
            block = (IMyFunctionalBlock)Entity;
            gun = (IMyGunObject<MyGunBase>)Entity;
        }
 
        public override void UpdateBeforeSimulation()
        {
            if (block == null || !block.IsFunctional || block?.CubeGrid?.Physics == null)
                return;

            gun.GunBase.CurrentAmmo = 1;

            var shotTime = gun.GunBase.LastShootTime.Ticks;
 
            if(shotTime > lastShotTime && !MyAPIGateway.Session.CreativeMode)
            {
                lastShotTime = shotTime;
                block.CubeGrid.RazeBlock(block.SlimBlock.Position);
            }

        }
    }
}