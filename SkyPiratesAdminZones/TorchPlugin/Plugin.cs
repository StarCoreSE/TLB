using System;
using System.Collections.Generic;
using System.Reflection;
using NLog;
using Sandbox;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using Torch.API;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace BobAdminZone
{
    /*
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_EntityBase), false, "ADMIN_Zone_Beacon")]
    public class BobAdminZoneBlock : MyGameLogicComponent
    {
        IMyBeacon beacon;

        BobAdminZoneBlock()
        {
            this.RegisterForEntityEvent()
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            beacon = Entity as IMyBeacon;
            BobAdminZone.instance.beacons.Add(beacon);
        }

        public override void Close()
        {
            BobAdminZone.instance.beacons.Remove(beacon);
        }
    }
    */
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class BobAdminZoneSession : MySessionComponentBase
    {
        public BobAdminZoneSession()
        {
            MyLog.Default.WriteLineAndConsole("FUCK");
        }
        public override void LoadData()
        {
            MyAPIGateway.Entities.OnEntityAdd += HandleEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove += HandleEntityRemoved;
        }

        private void HandleEntityAdded(IMyEntity entity)
        {
            if (entity != null && entity is IMyBeacon && (entity as IMyCubeBlock).SlimBlock.BlockDefinition.Id.SubtypeId.String.Contains("ADMIN"))
            {
                MyLog.Default.WriteLineAndConsole("beaky added");
                BobAdminZone.instance.beacons.Add(entity as IMyBeacon);
            }
        }

        private void HandleEntityRemoved(IMyEntity entity)
        {
            if (entity != null && entity is IMyBeacon && (entity as IMyCubeBlock).SlimBlock.BlockDefinition.Id.SubtypeId.String.Contains("ADMIN"))
            {
                BobAdminZone.instance.beacons.Remove(entity as IMyBeacon);
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= HandleEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove -= HandleEntityRemoved;
        }
    }

    public class BobAdminZone : TorchPluginBase
    {
        public static BobAdminZone instance;
        public List<IMyBeacon> beacons = new List<IMyBeacon>();

        private DateTime lastPromotionTime = DateTime.MinValue;
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private List<IMyPlayer> all_players = new List<IMyPlayer>();
        private List<IMyPlayer> temporary_admins = new List<IMyPlayer>();

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            instance = this;
            torch.GameStateChanged += new TorchGameStateChangedDel(this.Torch_GameStateChanged);
        }

        private void Torch_GameStateChanged(MySandboxGame game, TorchGameState newState)
        {
            if (newState == TorchGameState.Loaded)
            {
                if (MyAPIGateway.Session != null && MyAPIGateway.Session.IsServer)
                {
                    // Getting the current executing assembly
                    Assembly[] assemblies = new Assembly[1];
                    assemblies[0] = Assembly.GetExecutingAssembly();

                    // Assuming these are mod assemblies; change this to false if they are not
                    bool isModAssembly = true;

                    // Registering the components from the assembly
                    MySession.Static.RegisterComponentsFromAssembly(assemblies, isModAssembly, null);
                }
            }
            else if (newState == TorchGameState.Unloading && MyAPIGateway.Session != null && MyAPIGateway.Session.IsServer)
            {
                // Cleanup any unloading state-specific logic here
            }
        }

        private void PromotePlayers()
        {
            all_players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(all_players, null);
            foreach (IMyPlayer myPlayer in all_players)
            {
                bool shouldPromote = false;
                foreach (IMyBeacon beacon in beacons)
                {
                    double distanceSquared = (myPlayer.GetPosition() - beacon.WorldMatrix.Translation).LengthSquared();
                    if (distanceSquared < beacon.Radius * beacon.Radius)
                        shouldPromote = true;
                }

                if (shouldPromote && myPlayer.PromoteLevel != MyPromoteLevel.SpaceMaster && myPlayer.PromoteLevel != MyPromoteLevel.Admin)
                {
                    MySession mySession = MyAPIGateway.Session as MySession;
                    if (mySession != null && !temporary_admins.Contains(myPlayer))
                    {
                        mySession.SetUserPromoteLevel(myPlayer.SteamUserId, MyPromoteLevel.Admin);
                        Log.Info($"Promoted {myPlayer.DisplayName} to {myPlayer.PromoteLevel}");
                        temporary_admins.Add(myPlayer);
                    }
                }
                else if (!shouldPromote && myPlayer.PromoteLevel == MyPromoteLevel.SpaceMaster)
                {
                    MySession mySession = MyAPIGateway.Session as MySession;
                    if (mySession != null && temporary_admins.Contains(myPlayer))
                    {
                        mySession.SetUserPromoteLevel(myPlayer.SteamUserId, MyPromoteLevel.None);
                        Log.Info($"Demoted {myPlayer.DisplayName} to {myPlayer.PromoteLevel}");
                        temporary_admins.Remove(myPlayer);
                    }
                }
            }
        }

        public override void Update()
        {
            base.Update();

            if ((DateTime.Now - lastPromotionTime).TotalSeconds >= 10)
            {
                PromotePlayers();
                lastPromotionTime = DateTime.Now;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
