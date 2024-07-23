using System;
using System.Collections.Generic;
using NLog;
using Sandbox;
using Sandbox.Game;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using Torch.API;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace BobAdminZone
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_EntityBase), false, "ADMIN_Zone_Beacon")]
    public class BobAdminZoneBlock : MyGameLogicComponent
    {
        IMyBeacon beacon;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            beacon = Entity as IMyBeacon;
            if (beacon != null)
            {
                BobAdminZone.Log.Info($"BobAdminZoneBlock initialized for beacon: {beacon.EntityId}");
                BobAdminZone.instance.beacons.Add(beacon);
            }
            else
            {
                BobAdminZone.Log.Error("Failed to initialize BobAdminZoneBlock: Entity is not a beacon");
            }
        }

        public override void Close()
        {
            if (beacon != null)
            {
                BobAdminZone.Log.Info($"BobAdminZoneBlock closed for beacon: {beacon.EntityId}");
                BobAdminZone.instance.beacons.Remove(beacon);
            }
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
            Log.Info("BobAdminZone plugin initialized");
        }

        private void Torch_GameStateChanged(MySandboxGame game, TorchGameState newState)
        {
            Log.Info($"Game state changed to: {newState}");
            if (newState == TorchGameState.Loaded)
            {
                if (MyAPIGateway.Session != null && MyAPIGateway.Session.IsServer)
                {
                    Log.Info("Server session loaded");
                }
            }
            else if (newState == TorchGameState.Unloading && MyAPIGateway.Session != null && MyAPIGateway.Session.IsServer)
            {
                Log.Info("Server session unloading");
            }
        }

        private void PromotePlayers()
        {
            Log.Info("Starting PromotePlayers");
            all_players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(all_players, null);
            Log.Info($"Total players: {all_players.Count}");

            foreach (IMyPlayer myPlayer in all_players)
            {
                bool shouldPromote = false;
                foreach (IMyBeacon beacon in beacons)
                {
                    double distanceSquared = (myPlayer.GetPosition() - beacon.WorldMatrix.Translation).LengthSquared();
                    Log.Info($"Player {myPlayer.DisplayName} distance from beacon {beacon.EntityId}: {Math.Sqrt(distanceSquared)}");
                    if (distanceSquared < beacon.Radius * beacon.Radius)
                    {
                        shouldPromote = true;
                        Log.Info($"Player {myPlayer.DisplayName} is within range of beacon {beacon.EntityId}");
                        break;
                    }
                }

                if (shouldPromote && myPlayer.PromoteLevel != MyPromoteLevel.SpaceMaster && myPlayer.PromoteLevel != MyPromoteLevel.Admin)
                {
                    MySession mySession = MyAPIGateway.Session as MySession;
                    if (mySession != null && !temporary_admins.Contains(myPlayer))
                    {
                        try
                        {
                            mySession.SetUserPromoteLevel(myPlayer.SteamUserId, MyPromoteLevel.Admin);
                            Log.Info($"Promoted {myPlayer.DisplayName} to {MyPromoteLevel.Admin}");
                            temporary_admins.Add(myPlayer);
                        }
                        catch (Exception e)
                        {
                            Log.Error($"Failed to promote {myPlayer.DisplayName}: {e.Message}");
                        }
                    }
                }
                else if (!shouldPromote && myPlayer.PromoteLevel == MyPromoteLevel.SpaceMaster)
                {
                    MySession mySession = MyAPIGateway.Session as MySession;
                    if (mySession != null && temporary_admins.Contains(myPlayer))
                    {
                        try
                        {
                            mySession.SetUserPromoteLevel(myPlayer.SteamUserId, MyPromoteLevel.None);
                            Log.Info($"Demoted {myPlayer.DisplayName} to {MyPromoteLevel.None}");
                            temporary_admins.Remove(myPlayer);
                        }
                        catch (Exception e)
                        {
                            Log.Error($"Failed to demote {myPlayer.DisplayName}: {e.Message}");
                        }
                    }
                }
                else
                {
                    Log.Info($"No change for player {myPlayer.DisplayName}. Current level: {myPlayer.PromoteLevel}, ShouldPromote: {shouldPromote}");
                }
            }
            Log.Info("Finished PromotePlayers");
        }

        public override void Update()
        {
            base.Update();

            if ((DateTime.Now - lastPromotionTime).TotalSeconds >= 10)
            {
                Log.Info("Triggering PromotePlayers");
                PromotePlayers();
                lastPromotionTime = DateTime.Now;
            }
        }

        public override void Dispose()
        {
            Log.Info("BobAdminZone plugin disposed");
            base.Dispose();
        }
    }
}