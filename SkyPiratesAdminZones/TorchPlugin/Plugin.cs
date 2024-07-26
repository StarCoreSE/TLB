using System;
using System.Collections.Generic;
using System.Linq;
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
using VRageMath;

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
            //MyLog.Default.WriteLineAndConsole("FUCK");
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
                    HandleEntityAdded(entity);
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
        private List<long> temporary_admins = new List<long>();

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

                    // Initialize temporary admins from current session
                    InitializeTemporaryAdmins();
                }
            }
            else if (newState == TorchGameState.Unloading && MyAPIGateway.Session != null && MyAPIGateway.Session.IsServer)
            {
                // Cleanup any unloading state-specific logic here
            }
        }

        public void InitializeTemporaryAdmins()
        {
            all_players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(all_players);

            foreach (var player in all_players)
            {
                if (player.PromoteLevel == MyPromoteLevel.SpaceMaster || player.PromoteLevel == MyPromoteLevel.Admin)
                {
                    if (!temporary_admins.Contains((long)player.SteamUserId))
                    {
                        temporary_admins.Add((long)player.SteamUserId);
                        Log.Info($"Added preexisting admin: {player.DisplayName} to temporary admins list.");
                    }
                }
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
                    if (!beacon.Enabled || !beacon.IsFunctional)
                        continue;

                    double distanceSquared = (myPlayer.GetPosition() - beacon.WorldMatrix.Translation).LengthSquared();
                    if (distanceSquared < beacon.Radius * beacon.Radius)
                    {
                        shouldPromote = true;
                        break; // Ensures we stop checking once a valid beacon is found.
                    }
                }

                if (shouldPromote && myPlayer.PromoteLevel != MyPromoteLevel.SpaceMaster && myPlayer.PromoteLevel != MyPromoteLevel.Admin)
                {
                    PromotePlayer(myPlayer);
                }
                else if (!shouldPromote && myPlayer.PromoteLevel == MyPromoteLevel.SpaceMaster)
                {
                    DemotePlayer(myPlayer);
                }
            }
        }

        private void PromotePlayer(IMyPlayer myPlayer)
        {
            MySession mySession = MyAPIGateway.Session as MySession;
            if (mySession != null && !temporary_admins.Contains(myPlayer.IdentityId))
            {
                mySession.SetUserPromoteLevel(myPlayer.SteamUserId, MyPromoteLevel.SpaceMaster);
                mySession.EnableCreativeTools(myPlayer.SteamUserId, true);
                Log.Info($"Promoted {myPlayer.DisplayName} to SpaceMaster");

                // Assuming `nearestBeacon` is the beacon causing the promotion
                var nearestBeacon = beacons.FirstOrDefault(beacon =>
                    Vector3D.DistanceSquared(myPlayer.GetPosition(), beacon.WorldMatrix.Translation) < beacon.Radius * beacon.Radius);

                if (nearestBeacon != null)
                {
                    string beaconName = nearestBeacon.CustomName ?? "a beacon";
                    MyVisualScriptLogicProvider.SendChatMessageColored(
                        $"You have been promoted to SpaceMaster because you are within range of {beaconName}.",
                        Color.Green, "Server", myPlayer.IdentityId, "White");
                }

                temporary_admins.Add(myPlayer.IdentityId); // Track this user as promoted
            }
        }

        private void DemotePlayer(IMyPlayer myPlayer)
        {
            MySession mySession = MyAPIGateway.Session as MySession;
            if (mySession != null && temporary_admins.Contains(myPlayer.IdentityId))
            {
                mySession.SetUserPromoteLevel(myPlayer.SteamUserId, MyPromoteLevel.None);
                mySession.EnableCreativeTools(myPlayer.SteamUserId, false);
                Log.Info($"Demoted {myPlayer.DisplayName} to None");

                // Assuming you want to mention the beacon they moved away from
                var farBeacon = beacons.FirstOrDefault(beacon =>
                    Vector3D.DistanceSquared(myPlayer.GetPosition(), beacon.WorldMatrix.Translation) >= beacon.Radius * beacon.Radius);

                if (farBeacon != null)
                {
                    string beaconName = farBeacon.CustomName ?? "a beacon";
                    MyVisualScriptLogicProvider.SendChatMessageColored(
                        $"You have been demoted to regular player status because you moved out of range of {beaconName}.",
                        Color.Red, "Server", myPlayer.IdentityId, "White");
                }

                temporary_admins.Remove(myPlayer.IdentityId); // Remove from the tracking list
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
