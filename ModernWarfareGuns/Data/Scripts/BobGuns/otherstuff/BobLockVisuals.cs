using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game.Components;
using VRage.Game;
using VRage.Game.ModAPI;
using System.Collections.Concurrent;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.Game.Lights;
using Sandbox.Common.ObjectBuilders;
using VRageRender.Lights;
using System.Reflection;
using System.Reflection.Emit;

namespace BobLockVisuals
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
    public class BobLockVisualsSession : MySessionComponentBase
    {

        private const float ticktime = 1f / 60f;
        private const float updatetime = 3f;
        private List<string> lockSafeWeather = new List<string>
        {
            "SnowLight",
            "ThunderstormLight",
            "RainLight",
            "MarsSnow",
            "FogLight",
            "Dust",
            "AlienThunderstormLight",
            "AlienRainLight",
            "Clear",
        };
        public static BobLockVisualsSession instance;

        private float time = updatetime;

        Dictionary<string, TargetLockVisual> targetLocks = new Dictionary<string, TargetLockVisual>();
        HashSet<string> targetLocksKeysToRemove = new HashSet<string>();

        private class TargetLockVisual
        {
            const string subtypeId = "Welder";

            MyLight targetLight;
            MyTargetLockingComponent targetLock;
            BobLockVisualsSession system;
            string key;

            IMyEntity attacker;
            IMyEntity defender;

            public TargetLockVisual(MyTargetLockingComponent targetLock, IMyEntity attacker, IMyEntity defender, string key, BobLockVisualsSession system)
            {
                this.targetLock = targetLock;
                this.attacker = attacker;
                this.defender = defender;
                this.system = system;
                this.key = key;
            }

            public void CreateLight()
            {
                if (targetLight != null)
                    return;

                MyDefinitionId id = new MyDefinitionId(typeof(MyObjectBuilder_FlareDefinition), subtypeId);
                MyFlareDefinition flareDefinition = MyDefinitionManager.Static.GetDefinition(id) as MyFlareDefinition;

                targetLight = MyLights.AddLight();
                targetLight.Start("TargetLight");
                targetLight.Color = Color.Red;
                targetLight.Range = 10;
                targetLight.Falloff = 1f;
                targetLight.Intensity = 10f;
                targetLight.LightType = MyLightType.DEFAULT;
                targetLight.LightOn = false;
                targetLight.Position = attacker.Physics.CenterOfMassWorld + (MyAPIGateway.Session.Camera.Position - attacker.Physics.CenterOfMassWorld) / 10;
                targetLight.GlareOn = true;
                targetLight.GlareQuerySize = 0.2f;
                targetLight.GlareQueryFreqMinMs = 0f;
                targetLight.GlareQueryFreqRndMs = 0f;
                targetLight.GlareType = MyGlareTypeEnum.Distant;
                targetLight.GlareMaxDistance = 5000f;

                if (flareDefinition != null && flareDefinition.SubGlares != null)
                {
                    targetLight.SubGlares = flareDefinition.SubGlares;
                    targetLight.GlareIntensity = flareDefinition.Intensity * 20f;
                    targetLight.GlareSize = flareDefinition.Size * 0.5f;
                }

                targetLight.UpdateLight();
            }

            public void RemoveLight()
            {
                if(targetLight != null)
                {
                    MyLights.RemoveLight(targetLight);
                    targetLight = null;
                }
            }

            public void Update()
            {
                if(targetLock == null || defender == null || attacker == null || attacker.MarkedForClose || defender.MarkedForClose || !targetLock.IsTargetLocked)
                {
                    system.targetLocksKeysToRemove.Add(key);
                    RemoveLight();
                    return;
                }

                if (MyAPIGateway.Session?.Player != MyAPIGateway.Players.GetPlayerControllingEntity(defender))
                {
                    RemoveLight();
                    // MyAPIGateway.Utilities.ShowNotification($"lights out: lock {targetLock == null}; atak {attacker.DisplayName}; def {defender.DisplayName}; gri {MyAPIGateway.Session?.Player == MyAPIGateway.Players.GetPlayerControllingEntity(defender)}", 16);
                    return;
                }

                if (targetLight == null)
                    CreateLight();

                targetLight.Position = attacker.Physics.CenterOfMassWorld + (MyAPIGateway.Session.Camera.Position - attacker.Physics.CenterOfMassWorld) / 10;
                targetLight.UpdateLight();
            }
        }
        public override void BeforeStart()
        {
            instance = this;
        }
        public void DrawLocks()
        {
            if (MyAPIGateway.Session?.Player?.Character == null || MyAPIGateway.Session.Camera == null)
                return;
                // MyAPIGateway.Utilities.ShowNotification($"drawing {targetLocks.Count}", 16);

            foreach (TargetLockVisual targetLockVisual in targetLocks.Values)
            {
                targetLockVisual.Update();
            }
        }

        public void UpdateLocks()
        {
            List<IMyPlayer> myPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(myPlayers);

            //MyAPIGateway.Utilities.ShowNotification($"updated {targetLocks.Count}", 600);

            foreach (IMyPlayer player in myPlayers)
            {
                MyTargetLockingComponent targetLock = player?.Character?.Components?.Get<MyTargetLockingComponent>();
                // MyAPIGateway.Utilities.ShowNotification($"lock? {targetLock?.TargetEntity?.DisplayName}", 600);

                if (targetLock != null && targetLock.TargetEntity is IMyCubeGrid && player.Controller.ControlledEntity is IMyCubeBlock)
                {
                    if (!lockSafeWeather.Contains(MyAPIGateway.Session.WeatherEffects.GetWeather(player.Character.WorldMatrix.Translation))){
                        targetLock.ReleaseTargetLock();
                        continue;
                    }



                    IMyEntity attackerGrid = (player.Controller.ControlledEntity as IMyCubeBlock).CubeGrid;
                    string key = $"lock:{attackerGrid.EntityId}:{targetLock.TargetEntity.EntityId}";
                    if(!targetLocks.ContainsKey(key))
                    {
                        // MyAPIGateway.Utilities.ShowNotification($"added lock {key}", 6000);
                        TargetLockVisual lockVisual = new TargetLockVisual(targetLock, attackerGrid, targetLock.TargetEntity, key, instance);
                        targetLocks.Add(key, lockVisual);
                    }
                }
            }

            foreach(string key in targetLocksKeysToRemove)
            {
                // MyAPIGateway.Utilities.ShowNotification($"rm lock {key}", 6000);
                targetLocks.Remove(key);
            }
            targetLocksKeysToRemove.Clear();
        }

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            time += ticktime;

            DrawLocks();

            if (time < updatetime)
                return;

            UpdateLocks();
            time = ticktime;
        }
    }
}

