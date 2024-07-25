using System;
using System.Collections.Generic;
using System.Reflection;
using NLog;
using Sandbox;
using Sandbox.Game;
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
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class BobAdminZoneSession : MySessionComponentBase
    {
        int timer = 0;
        bool initd = false;

        public BobAdminZoneSession()
        {
            MyLog.Default.WriteLineAndConsole("FUCK");
        }
        public void init()
        {
            MyAPIGateway.Entities.OnEntityAdd += HandleEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove += HandleEntityRemoved;

            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);

            foreach (IMyEntity entity in entities)
            {
                if (entity is IMyCubeGrid)
                    HandleEntityAdded
            }

            initd = true;
        }

        public override void UpdateAfterSimulation()
        {
            if (!initd)
                init();
        }

        private void HandleEntityAdded(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;

            IMyCubeGrid grid = entity as IMyCubeGrid;

            foreach (IMyCubeBlock block in grid.GetFatBlocks<IMyBeacon>())
            {
                if (block.SlimBlock.BlockDefinition.Id.SubtypeName.Contains("ADMIN"))
                {
                    MyLog.Default.WriteLineAndConsole($"{grid.EntityId} beacon added");
                    BobAdminZone.instance.beacons.Add(block as IMyBeacon);
                }
            }

            grid.OnBlockAdded += HandleBlockAdded;
            grid.OnBlockRemoved += HandleBlockRemoved;
        }

        private void HandleEntityRemoved(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid) || entity.Physics == null)
                return;

            IMyCubeGrid grid = entity as IMyCubeGrid;

            foreach (IMyCubeBlock block in grid.GetFatBlocks<IMyBeacon>())
            {
                if (block.SlimBlock.BlockDefinition.Id.SubtypeName.Contains("ADMIN"))
                {
                    MyLog.Default.WriteLineAndConsole($"{grid.EntityId} beacon rmd");
                    BobAdminZone.instance.beacons.Remove(block as IMyBeacon);
                }
            }

            grid.OnBlockAdded -= HandleBlockAdded;
            grid.OnBlockRemoved -= HandleBlockRemoved;
        }

        private void HandleBlockAdded(IMySlimBlock slim)
        {
            if (slim != null && slim.FatBlock is IMyBeacon && slim.BlockDefinition.Id.SubtypeName.Contains("ADMIN"))
            {
                MyLog.Default.WriteLineAndConsole($"{slim.FatBlock.CubeGrid.EntityId} beacon added");
                BobAdminZone.instance.beacons.Add(slim.FatBlock as IMyBeacon);
            }
        }

        private void HandleBlockRemoved(IMySlimBlock slim)
        {
            if (slim != null && slim.FatBlock is IMyBeacon && slim.BlockDefinition.Id.SubtypeName.Contains("ADMIN"))
            {
                MyLog.Default.WriteLineAndConsole($"{slim.FatBlock.CubeGrid.EntityId} beacon rmd");
                BobAdminZone.instance.beacons.Remove(slim.FatBlock as IMyBeacon);
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= HandleEntityAdded;
            MyAPIGateway.Entities.OnEntityRemove -= HandleEntityRemoved;
            initd = true;
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
            MyLog.Default.WriteLineAndConsole($"player conut: {all_players.Count}, beaky counut: {beacons.Count}");
            foreach (IMyPlayer myPlayer in all_players)
            {
                bool shouldPromote = false;
                foreach (IMyBeacon beacon in beacons)
                {
                    if (!beacon.Enabled || !beacon.IsFunctional)
                        continue;

                    double distanceSquared = (myPlayer.GetPosition() - beacon.WorldMatrix.Translation).LengthSquared();
                    if (distanceSquared < beacon.Radius * beacon.Radius)
                        shouldPromote = true;
                }

                if (shouldPromote && myPlayer.PromoteLevel != MyPromoteLevel.SpaceMaster && myPlayer.PromoteLevel != MyPromoteLevel.Admin)
                {
                    MySession mySession = MyAPIGateway.Session as MySession;
                    if (mySession != null && !temporary_admins.Contains(myPlayer))
                    {
                        mySession.SetUserPromoteLevel(myPlayer.SteamUserId, MyPromoteLevel.SpaceMaster);
                        mySession.EnableCreativeTools(myPlayer.SteamUserId, true);
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
                        mySession.EnableCreativeTools(myPlayer.SteamUserId, false);
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
