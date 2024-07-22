using Sandbox.Game;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
//using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI;
using static VRage.Game.MyObjectBuilder_ControllerSchemaDefinition;

namespace hydrorefiller
{
    /// <summary>
    /// it uhh refills your hydro man. trust.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation  )]
    public class Bigredbession : MySessionComponentBase
    {
        public double tock = 0;
        public int delay = 10;
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

            var UpdatedDictionary = new Dictionary<long, long>();
            foreach (var player in playerlist)
            {
                IMyCharacter character = player.Controller?.ControlledEntity?.Entity as IMyCharacter; 
                if (character != null && !character.IsDead)
                {
                    float hydro = MyVisualScriptLogicProvider.GetPlayersHydrogenLevel(player.Identity.IdentityId);
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