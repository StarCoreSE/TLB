using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Klime.CatchARideGameLogic
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ButtonPanel), false, "save_panel_catcharide", "claim_panel_catcharide")]
    public class CatchARideGameLogic : MyGameLogicComponent
    {
        private IMyButtonPanel panel;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                panel = Entity as IMyButtonPanel;
                NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (panel.CubeGrid.Physics != null)
            {
                panel.CubeGrid.Name = panel.CubeGrid.EntityId.ToString();
                MyEntities.SetEntityName((MyEntity)panel.CubeGrid);
                MyVisualScriptLogicProvider.SetGridDestructible(panel.CubeGrid.Name, false);
                MyVisualScriptLogicProvider.SetGridEditable(panel.CubeGrid.Name, false);

                panel.PropertiesChanged += Panel_PropertiesChanged;
            }
        }

        private void Panel_PropertiesChanged(IMyTerminalBlock obj)
        {
            if (panel.ShowOnHUD)
            {
                MyVisualScriptLogicProvider.SetGridEditable(panel.CubeGrid.Name, true);
            }
            else
            {
                MyVisualScriptLogicProvider.SetGridEditable(panel.CubeGrid.Name, false);
            }
        }

        public override void Close()
        {

        }
    }
}