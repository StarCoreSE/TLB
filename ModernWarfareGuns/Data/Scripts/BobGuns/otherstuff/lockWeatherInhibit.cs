using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using Sandbox.Game.EntityComponents;

namespace BobLockVisuals
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
    public class BobLockVisualsSession : MySessionComponentBase
    {
        List<IMyPlayer> myPlayers = new List<IMyPlayer>();
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
            "None",
        };

        private float time = updatetime;

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            time += ticktime;

            foreach (IMyPlayer player in myPlayers)
            {
                MyTargetLockingComponent targetLock = player?.Character?.Components?.Get<MyTargetLockingComponent>();
                if (targetLock == null) { return; }
                if (!lockSafeWeather.Contains(MyAPIGateway.Session.WeatherEffects.GetWeather(player.Character.WorldMatrix.Translation)) && MyAPIGateway.Session.WeatherEffects.GetWeatherIntensity(player.Character.WorldMatrix.Translation) > 0.75f)
                    targetLock.ReleaseTargetLock();
            }

            if (time < updatetime)
                return;

            myPlayers.Clear();
            MyAPIGateway.Players.GetPlayers(myPlayers);
            time = ticktime;
        }
    }
}

