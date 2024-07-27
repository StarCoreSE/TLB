using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using VRage.Game;
using VRage.Game.Components;

namespace playerHUD
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class playerhud : MySessionComponentBase
    {
        bool shown = false;
        bool waiting = false;
        DateTime startTime;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
        }

        public override void BeforeStart()
        {
            if (MyAPIGateway.Session?.Player == null) return; // Only run on client
            MyVisualScriptLogicProvider.PlayerSpawned += PlayerSpawned;
            MyVisualScriptLogicProvider.RemoveQuestlogDetails();
            MyVisualScriptLogicProvider.SetQuestlog(false);
            shown = false;
            waiting = false;
        }

        private void PlayerSpawned(long playerId)
        {
            if (MyAPIGateway.Session?.Player == null) return; // Only run on client
            if (!shown && !waiting)
            {
                MyVisualScriptLogicProvider.SetQuestlog(true, "Keybinds");
                MyVisualScriptLogicProvider.AddQuestlogObjective("SHIFT+F2 = Build Rules", false, true);
                MyVisualScriptLogicProvider.AddQuestlogObjective("CTRL + F2 = Report Issue", false, true);
                waiting = true;
                startTime = DateTime.Now;
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Session?.Player == null) return; // Only run on client
            if (waiting && DateTime.Now - startTime >= TimeSpan.FromSeconds(10))
            {
                MyVisualScriptLogicProvider.RemoveQuestlogDetails();
                MyVisualScriptLogicProvider.SetQuestlog(false);
                waiting = false;
                shown = false;
            }
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Session?.Player == null) return; // Only run on client
            MyVisualScriptLogicProvider.PlayerSpawned -= PlayerSpawned;
        }
    }
}
