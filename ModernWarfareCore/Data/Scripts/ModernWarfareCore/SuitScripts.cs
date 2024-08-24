using Sandbox.Game;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Components.Session;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

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

            foreach (IMyPlayer player in players)
            {
                SpeedLimit(player);
            }

            if (ticks % 900 == 0)
            {
                foreach (IMyPlayer player in players)
                {
                    RefillHydro(player);
                }

            }

        }

        public void RefillHydro(IMyPlayer player)
        {
            if (player.Character == null)
                return;

            var jetpack = player.Character.Components.Get<MyCharacterJetpackComponent>();

            if (jetpack == null)
                return;

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

        public void SpeedLimit(IMyPlayer player)
        {
            IMyCharacter character = player.Character;

            if (character == null || character.MarkedForClose || character.IsDead || player.PromoteLevel >= MyPromoteLevel.SpaceMaster)
                return;

            if (character.RelativeDampeningTarget == null && character.EnabledThrusts)
            {
                Vector3D vel = character.Physics.LinearVelocity;
                Vector3D gravityDirection = Vector3D.Normalize(character.Physics.Gravity); // Get the normalized gravity direction
                Vector3D verticalVelocity = gravityDirection * Vector3D.Dot(vel, gravityDirection); // Vertical component of velocity
                Vector3D horizontalVelocity = vel - verticalVelocity; // Horizontal component of velocity

                if (horizontalVelocity.LengthSquared() > 25)
                {
                    horizontalVelocity = Vector3D.Normalize(horizontalVelocity) * 5;
                    character.Physics.LinearVelocity = horizontalVelocity + verticalVelocity; // Combine modified horizontal with original vertical
                }
            } else if(character.RelativeDampeningTarget != null && character.EnabledThrusts)
            {
                Vector3D vel = character.Physics.LinearVelocity;
                Vector3D targ_vel = character.RelativeDampeningTarget.Physics.LinearVelocity;
                Vector3D rel_vel = vel - targ_vel;

                if (rel_vel.LengthSquared() > 25)
                    character.Physics.LinearVelocity = targ_vel + Vector3D.Normalize(rel_vel) * 5;
            }
        }
    }
}
