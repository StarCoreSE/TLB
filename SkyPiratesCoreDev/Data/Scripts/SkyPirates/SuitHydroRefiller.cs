using Sandbox.Game;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
//using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI;

namespace hydrorefiller
{
    /// <summary>
    /// it uhh refills your hydro man. trust.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Bigredbession : MySessionComponentBase
    {
        public double tock = 0;
        public int delay = 6;
        public static Dictionary<long, int> playerToDelay = new Dictionary<long, int>();

        public override void UpdateBeforeSimulation()
        {

            tock++;
            if (tock <= 60)
            {
                return;
            }
            tock = 0;

            List<IMyPlayer> playerlist = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerlist);
            
            Dictionary<long, int> UpdatedDictionary = new Dictionary<long, int>();
            foreach (var player in playerlist)
            {
                IMyCharacter character = player.Controller?.ControlledEntity?.Entity as IMyCharacter; 
                if (character != null && !character.IsDead)
                {
                    float hydro = MyVisualScriptLogicProvider.GetPlayersHydrogenLevel(player.Identity.IdentityId);
                    bool needsRefill = hydro <= 1f;

                    if (!playerToDelay.ContainsKey(player.Identity.IdentityId) && needsRefill)
                    {
                        UpdatedDictionary.Add(player.Identity.IdentityId, 0);
                    }
                    else if (playerToDelay.ContainsKey(player.Identity.IdentityId))
                    {
                        int elapsedTime = playerToDelay[player.Identity.IdentityId] + 1;
                        if (elapsedTime > delay)
                        {
                            MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(player.Identity.IdentityId, 1f);

                        }
                        else
                        {
                            UpdatedDictionary.Add(player.Identity.IdentityId, elapsedTime);
                        }
                    }

                    playerToDelay = UpdatedDictionary;
                }
            }

        }
    }
}