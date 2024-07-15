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
        public override void BeforeStart()
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
                    turretDef.AiEnabled = false;
                    turretDef.MaxRangeMeters = 0;
                }
            }
        }
    }
}
