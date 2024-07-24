using System;
using System.Collections.Generic;
using NLog;
using Sandbox;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using Torch.API;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;


namespace BobAdminZone
{
    
    public static class AdminZoneData
    {
        public static List<IMyBeacon> AdminBeacons = new List<IMyBeacon>();
    }

    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class AdminZoneSessionComponent : MySessionComponentBase
    {
        private int updateCounter = 0;

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (!MyAPIGateway.Session.IsServer) return;

                updateCounter++;
                if (updateCounter % 100 != 0) return; // Every ~1.6 seconds

                MyVisualScriptLogicProvider.SendChatMessage("FUCK");

                AdminZoneData.AdminBeacons.Clear();

                HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
                MyAPIGateway.Entities.GetEntities(entities, e => e is IMyBeacon);

                foreach (IMyEntity entity in entities)
                {
                    IMyBeacon beacon = entity as IMyBeacon;
                    if (beacon != null && beacon.BlockDefinition.SubtypeName == "ADMIN_Zone_Beacon" && beacon.IsWorking)
                    {
                        AdminZoneData.AdminBeacons.Add(beacon);
                    }
                }

                // In-game logging (will appear in SpaceEngineers log)
                MyLog.Default.Info($"[AdminZone] Found {AdminZoneData.AdminBeacons.Count} admin beacons");
            }
            catch (Exception ex)
            {
                MyLog.Default.Error($"[AdminZone] Error updating beacons: {ex.Message}");
            }
        }
    }

    public class BobAdminZone : TorchPluginBase
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private DateTime lastPromotionTime = DateTime.MinValue;
        private List<IMyPlayer> allPlayers = new List<IMyPlayer>();

        public override void Update()
        {
            base.Update();

            if ((DateTime.Now - lastPromotionTime).TotalSeconds >= 10)
            {
                ProcessAdminZones();
                lastPromotionTime = DateTime.Now;
            }
        }

        private void ProcessAdminZones()
        {
            Log.Info($"Processing admin zones. Found {AdminZoneData.AdminBeacons.Count} admin beacons");

            allPlayers.Clear();
            MyAPIGateway.Players.GetPlayers(allPlayers);

            foreach (IMyPlayer player in allPlayers)
            {
                bool inAdminZone = false;
                foreach (IMyBeacon beacon in AdminZoneData.AdminBeacons)
                {
                    Vector3D playerPos = player.GetPosition();
                    Vector3D beaconPos = beacon.GetPosition();
                    double distanceSquared = Vector3D.DistanceSquared(playerPos, beaconPos);

                    if (distanceSquared <= beacon.Radius * beacon.Radius)
                    {
                        inAdminZone = true;
                        break;
                    }
                }

                if (inAdminZone && player.PromoteLevel != MyPromoteLevel.Admin)
                {
                    MySession.Static.SetUserPromoteLevel(player.SteamUserId, MyPromoteLevel.Admin);
                    Log.Info($"Promoted {player.DisplayName} to Admin");
                }
                else if (!inAdminZone && player.PromoteLevel == MyPromoteLevel.Admin)
                {
                    MySession.Static.SetUserPromoteLevel(player.SteamUserId, MyPromoteLevel.None);
                    Log.Info($"Demoted {player.DisplayName} to Player");
                }
            }
        }
    }
}