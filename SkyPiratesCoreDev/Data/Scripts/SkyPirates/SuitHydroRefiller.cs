using Sandbox.Game;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Components.Session;
using VRage.Game.ModAPI;

namespace hydrorefiller
{
    /// <summary>
    /// it uhh refills your hydro man. trust.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class hydrosuitgogoodly : MySessionComponentBase
    {

        private int ticks;
        private List<IMyPlayer> players = new List<IMyPlayer>();
        private float newfuel;
        private float fillAmount = 1f;

        public override void UpdateBeforeSimulation()
        {
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            unchecked
            {
                ticks++;
            }

            if (ticks % 900 == 0)
            {
                foreach (IMyPlayer player in players)
                {
                    if (player.Character == null)
                        continue;

                    var jetpack = player.Character.Components.Get<MyCharacterJetpackComponent>();

                    if (jetpack == null)
                        continue;

                    if (!jetpack.Running || jetpack.FinalThrust.LengthSquared() == 0)
                    {
                        var oxyComp = player.Character.Components.Get<MyCharacterOxygenComponent>();

                        float fuel = oxyComp.GetGasFillLevel(MyCharacterOxygenComponent.HydrogenId);

                        if (fuel < 1)
                        {
                            if (fuel < 1 - fillAmount)
                            {
                                newfuel = fuel + fillAmount;
                            }
                            else
                            {
                                newfuel = 1;
                            }

                            var hydrogenId = MyCharacterOxygenComponent.HydrogenId;

                            oxyComp.UpdateStoredGasLevel(ref hydrogenId, newfuel);
                        }

                    }

                }

            }

        }
    }
}
