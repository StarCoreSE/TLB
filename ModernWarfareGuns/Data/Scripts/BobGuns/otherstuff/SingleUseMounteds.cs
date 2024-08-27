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
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
 
namespace BOB
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SmallMissileLauncherReload), false, "MountedMissile", "BasiliskGunBlock", "MountedMediumBomb", "MountedLargeBomb")]
    public class MountedBomb : MyGameLogicComponent
    {
        private IMyFunctionalBlock block;
        private IMyGunObject<MyGunBase> gun;
        private long lastShotTime;
        bool is_host;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
            is_host = MyAPIGateway.Session?.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer || MyAPIGateway.Utilities.IsDedicated;
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

                if (is_host);
                    block.CubeGrid.RazeBlock(block.SlimBlock.Position);
            }

        }
    }
}