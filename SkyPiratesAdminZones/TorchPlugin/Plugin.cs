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

        private DateTime lastCheckTime = DateTime.MinValue;
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private List<IMyPlayer> all_players = new List<IMyPlayer>();
        private HashSet<long> playersInRange = new HashSet<long>();

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            instance = this;
            torch.GameStateChanged += Torch_GameStateChanged;
        }

        private void Torch_GameStateChanged(MySandboxGame game, TorchGameState newState)
        {
            if (newState == TorchGameState.Loaded)
            {
                if (MyAPIGateway.Session != null && MyAPIGateway.Session.IsServer)
                {
                    Assembly[] assemblies = new Assembly[1] { Assembly.GetExecutingAssembly() };
                    bool isModAssembly = true;
                    MySession.Static.RegisterComponentsFromAssembly(assemblies, isModAssembly, null);
                }
            }
            else if (newState == TorchGameState.Unloading && MyAPIGateway.Session != null && MyAPIGateway.Session.IsServer)
            {
                ClearPlayersInRange();
            }
        }

        private void ClearPlayersInRange()
        {
            Log.Info($"Clearing {playersInRange.Count} players in range.");
            playersInRange.Clear();
        }

        private void CheckPlayersInRange()
        {
            all_players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(all_players, null);

            HashSet<long> newPlayersInRange = new HashSet<long>();

            foreach (IMyPlayer player in all_players)
            {
                bool isWithinRange = IsPlayerWithinRange(player);

                if (isWithinRange)
                {
                    newPlayersInRange.Add(player.IdentityId);
                    if (!playersInRange.Contains(player.IdentityId))
                    {
                        PromotePlayer(player);
                    }
                }
                else if (player.PromoteLevel == MyPromoteLevel.SpaceMaster)
                {
                    DemotePlayer(player);
                }
            }

            playersInRange = newPlayersInRange;
        }

        private bool IsPlayerWithinRange(IMyPlayer player)
        {
            foreach (IMyBeacon beacon in beacons)
            {
                if (beacon.Enabled && beacon.IsFunctional)
                {
                    double distanceSquared = Vector3D.DistanceSquared(player.GetPosition(), beacon.WorldMatrix.Translation);
                    if (distanceSquared < beacon.Radius * beacon.Radius)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void PromotePlayer(IMyPlayer player)
        {
            MySession mySession = MyAPIGateway.Session as MySession;
            if (mySession != null && player.PromoteLevel != MyPromoteLevel.SpaceMaster && player.PromoteLevel != MyPromoteLevel.Admin)
            {
                mySession.SetUserPromoteLevel(player.SteamUserId, MyPromoteLevel.SpaceMaster);
                mySession.EnableCreativeTools(player.SteamUserId, true);
                Log.Info($"Promoted {player.DisplayName} to SpaceMaster");

                var nearestBeacon = beacons.FirstOrDefault(beacon =>
                    Vector3D.DistanceSquared(player.GetPosition(), beacon.WorldMatrix.Translation) < beacon.Radius * beacon.Radius);

                if (nearestBeacon != null)
                {
                    string beaconName = nearestBeacon.CustomName ?? "a beacon";
                    MyVisualScriptLogicProvider.SendChatMessageColored(
                        $"You have been promoted to SpaceMaster because you are within range of {beaconName}.",
                        Color.Green, "Server", player.IdentityId, "White");
                }
            }
        }

        private void DemotePlayer(IMyPlayer player)
        {
            MySession mySession = MyAPIGateway.Session as MySession;
            if (mySession != null && player.PromoteLevel == MyPromoteLevel.SpaceMaster)
            {
                mySession.SetUserPromoteLevel(player.SteamUserId, MyPromoteLevel.None);
                mySession.EnableCreativeTools(player.SteamUserId, false);
                Log.Info($"Demoted {player.DisplayName} to None");

                MyVisualScriptLogicProvider.SendChatMessageColored(
                    $"You have been demoted to regular player status because you moved out of range of all admin beacons.",
                    Color.Red, "Server", player.IdentityId, "White");
            }
        }

        public override void Update()
        {
            base.Update();

            if ((DateTime.Now - lastCheckTime).TotalSeconds >= 10)
            {
                CheckPlayersInRange();
                lastCheckTime = DateTime.Now;
            }
        }

        public override void Dispose()
        {
            ClearPlayersInRange();
            base.Dispose();
        }
    }
}
