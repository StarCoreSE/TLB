using System;
using System.Collections.Generic;
using NLog;
using Sandbox;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using Torch.API;
using VRage.Game.ModAPI;

namespace AutoPromoTorchFixed
{
    public class AutoPromoTorchFixed : TorchPluginBase
    {
        private DateTime lastPromotionTime = DateTime.MinValue;
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private List<IMyPlayer> all_players = new List<IMyPlayer>();
        private List<IMyPlayer> promoted_players = new List<IMyPlayer>();

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            torch.GameStateChanged += new TorchGameStateChangedDel(this.Torch_GameStateChanged);
        }

        private void Torch_GameStateChanged(MySandboxGame game, TorchGameState newState)
        {
            if (newState == TorchGameState.Loaded)
            {
                if (MyAPIGateway.Session != null && MyAPIGateway.Session.IsServer)
                {
                    // Initialize any loaded state-specific logic here
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
                if (myPlayer.PromoteLevel != MyPromoteLevel.SpaceMaster && myPlayer.PromoteLevel != MyPromoteLevel.Admin)
                {
                    MySession mySession = MyAPIGateway.Session as MySession;
                    if (mySession != null && !promoted_players.Contains(myPlayer))
                    {
                        mySession.SetUserPromoteLevel(myPlayer.SteamUserId, MyPromoteLevel.Admin);
                        Log.Info($"Promoted {myPlayer.DisplayName} to {myPlayer.PromoteLevel}");
                        promoted_players.Add(myPlayer);
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
