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

namespace BobLockVisuals
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
    public class BobLockVisualsSession : MySessionComponentBase
    {

        private const float ticktime = 1f / 60f;
        private const float updatetime = 3f;

        private float time = updatetime;

        Dictionary<IMyPlayer, TargetLockVisual> targetLocks = new Dictionary<IMyPlayer, TargetLockVisual>();
        private class TargetLockVisual
        {
            const string subtypeId = "Welder";

            public IMyEntity attacker;
            MyLight targetLight;

            public TargetLockVisual(IMyEntity attacker)
            {
                this.attacker = attacker;
            }

            public void CreateLight()
            {
                MyDefinitionId id = new MyDefinitionId(typeof(MyObjectBuilder_FlareDefinition), subtypeId);
                MyFlareDefinition flareDefinition = MyDefinitionManager.Static.GetDefinition(id) as MyFlareDefinition;

                targetLight = MyLights.AddLight();
                targetLight.Start("TargetLight");
                targetLight.Color = Color.Red;
                targetLight.Range = 10;
                targetLight.Falloff = 1f;
                targetLight.Intensity = 10f;
                targetLight.LightType = MyLightType.DEFAULT;
                targetLight.LightOn = true;
                targetLight.Position = attacker.GetPosition() + (MyAPIGateway.Session.Camera.Position - attacker.GetPosition()) / 10;
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
                    targetLight.GlareSize = flareDefinition.Size * 0.3f;
                }

                targetLight.UpdateLight();
            }

            public void RemoveLight()
            {
                MyLights.RemoveLight(targetLight);
                targetLight = null;
            }

            public void Update()
            {
                targetLight.Position = attacker.GetPosition() + (MyAPIGateway.Session.Camera.Position - attacker.GetPosition()) / 10;
                targetLight.UpdateLight();
            }

        }

        public void DrawLocks()
        {
            foreach(TargetLockVisual targetLockVisual in targetLocks.Values)
            {
                targetLockVisual.Update();
            }
        }

        public void UpdateLocks()
        {
            List<IMyPlayer> myPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(myPlayers);

            foreach (IMyPlayer player in myPlayers)
            {
                if (player == MyAPIGateway.Session.Player)
                    continue;

                MyTargetLockingComponent targetLock = player?.Character?.Components?.Get<MyTargetLockingComponent>();

                if (targetLock != null && targetLock.Entity is IMyCubeGrid)
                {
                    TargetLockVisual lockVisual = new TargetLockVisual(player.Character);
                    lockVisual.CreateLight();
                }
                else
                {
                    TargetLockVisual lockVisual;
                    if(targetLocks.TryGetValue(player, out lockVisual))
                    {
                        lockVisual.RemoveLight();
                        targetLocks.Remove(player);
                    }
                }
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session?.Player?.Character == null || MyAPIGateway.Session.Camera == null)
                return;

            time += ticktime;

            DrawLocks();

            if (time % updatetime != 0f)
                return;

            UpdateLocks();
            time = ticktime;
        }
    }
}

