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
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation  )]
    public class Bigredbession : MySessionComponentBase
    {
        public double tock = 0;
        public int delay = 30;
        public static Dictionary<long, long> playerdictionary = new Dictionary<long, long>();

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

            foreach (var player in playerlist)
            {
                if (player.Controller?.ControlledEntity?.Entity is IMyCharacter)
                {
                    float hydro = MyVisualScriptLogicProvider.GetPlayersHydrogenLevel(player.Identity.IdentityId);

                    var UpdatedDictionary = new Dictionary<long, long>();
                    bool nohydro = hydro <= 0f;
                    // add to list if they are not in the list and they dotn have hydro
                    if (!playerdictionary.ContainsKey(player.Identity.IdentityId) && nohydro)
                    {
                        UpdatedDictionary[player.Identity.IdentityId] = 0;
                    }
                    else if (playerdictionary.ContainsKey(player.Identity.IdentityId))
                    {

                        var elapsedTime = playerdictionary[player.Identity.IdentityId] + 1;
                        if (elapsedTime > delay)
                        {
                            MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(player.Identity.IdentityId, 1f);

                        }
                        else
                        {
                            UpdatedDictionary[player.Identity.IdentityId] = elapsedTime;
                        }
                    }

                    playerdictionary = UpdatedDictionary;
                }
            }

        }
    }
}