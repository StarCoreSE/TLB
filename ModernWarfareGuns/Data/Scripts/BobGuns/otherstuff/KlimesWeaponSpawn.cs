using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game;

using Sandbox.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using Sandbox.Definitions;
//using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using ParallelTasks;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using Sandbox.Game.Lights;

using System.Threading;
using System.Text;
using VRage.Utils;
using VRage.Library.Utils;
using Sandbox.Game.SessionComponents;
using Sandbox.Graphics;
using VRage;
using Sandbox.Game.Entities.Cube;
using VRage.Game.Entity;


namespace bob
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class gunAdd : MySessionComponentBase
    {

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                //MyVisualScriptLogicProvider.PlayerSpawned += PlayerSpawned; //
            }
        }

        public override void BeforeStart()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.PlayerSpawned += PlayerSpawned;
            }
        }

        private void PlayerSpawned(long playerId)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.ClearAllToolbarSlots(playerId);
                MyDefinitionId reuse_def;

                if (MyDefinitionId.TryParse("MyObjectBuilder_PhysicalGunObject/HandDrillItem", out reuse_def))
                {
                    MyVisualScriptLogicProvider.RemoveFromPlayersInventory(playerId, reuse_def, 1);
                }
                if (MyDefinitionId.TryParse("MyObjectBuilder_PhysicalGunObject/AutomaticRifleItem", out reuse_def))
                {
                    MyVisualScriptLogicProvider.RemoveFromPlayersInventory(playerId, reuse_def, 1);
                }
                if (MyDefinitionId.TryParse("MyObjectBuilder_PhysicalGunObject/AngleGrinderItem", out reuse_def))
                {
                    MyVisualScriptLogicProvider.SetToolbarSlotToItem(1, reuse_def, playerId);
                }
                if (MyDefinitionId.TryParse("MyObjectBuilder_PhysicalGunObject/WelderItem", out reuse_def))
                {
                    MyVisualScriptLogicProvider.SetToolbarSlotToItem(2, reuse_def, playerId);
                }
                if (MyDefinitionId.TryParse("MyObjectBuilder_PhysicalGunObject/SemiAutoPistolItem", out reuse_def))
                {
                    MyVisualScriptLogicProvider.AddToPlayersInventory(playerId, reuse_def, 1);
                    MyVisualScriptLogicProvider.SetToolbarSlotToItem(0, reuse_def, playerId);
                }
                if (MyDefinitionId.TryParse("MyObjectBuilder_AmmoMagazine/MinigunMag", out reuse_def))
                {
                    MyVisualScriptLogicProvider.AddToPlayersInventory(playerId, reuse_def, 3);
                }
            }
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.PlayerSpawned -= PlayerSpawned;
            }
        }
    }
}