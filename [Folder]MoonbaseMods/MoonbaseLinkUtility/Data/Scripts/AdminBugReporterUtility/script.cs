using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;


namespace invalid.BugReporter
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class RoSSLinkUtility : MySessionComponentBase
    {
        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            if (MyAPIGateway.Input.IsKeyPress(MyKeys.LeftShift) && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.F2) && ValidInput()) //hey dumbass, use this before the url. fucking keen https://steamcommunity.com/linkfilter/?url={url}
            {
               
                MyVisualScriptLogicProvider.OpenSteamOverlay("https://steamcommunity.com/linkfilter/?url=https://docs.google.com/document/d/1yoYMmdiJyq1cGgzp_p1_wY0rccdv4dPZmjhj5eS_53U/");
						
            }	
            
				
        }

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{

            MyAPIGateway.Utilities.ShowMessage("Server", "Press Shift + F2 to open the Moonbase Infodoc" );
			                    
		}      



        private bool ValidInput()
        {
            if (MyAPIGateway.Session.CameraController != null && !MyAPIGateway.Gui.ChatEntryVisible && !MyAPIGateway.Gui.IsCursorVisible
                && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.None)
            {
                return true;
            }
            return false;
        }

        protected override void UnloadData()
        {

        }
    }
}
