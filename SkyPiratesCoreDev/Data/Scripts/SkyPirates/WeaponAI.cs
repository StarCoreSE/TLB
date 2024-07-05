using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.Components;

namespace cleaner
{
    /// <summary>
    /// Handles removal of AI behaviour from all blocks of <see cref="MyLargeTurretBaseDefinition"/>
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Core : MySessionComponentBase
    {
        private static float AiRange = 0f;

        private const bool AiEnabled = false;

        /// <summary>
        /// Removes Ai from turret blocks.
        /// </summary>
        private void RemoveAI()
        {
            foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                MyCubeBlockDefinition blockDef = def as MyLargeTurretBaseDefinition;
                var turretDef = def as MyLargeTurretBaseDefinition;

                if (blockDef == null)
                {
                    continue;
                }

                if (turretDef != null)
                {
                    turretDef.AiEnabled = Core.AiEnabled;
                    turretDef.MaxRangeMeters = Core.AiRange;
                }
            }
        }

        public override void BeforeStart()
        {
            this.RemoveAI();
        }
    }
}
