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
    public class ButtonExample : MySessionComponentBase
    {
        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            if (MyAPIGateway.Input.IsKeyPress(MyKeys.LeftShift) && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.F2) && ValidInput()) //hey dumbass, use this before the url. fucking keen https://steamcommunity.com/linkfilter/?url={url}
            {
               
                MyVisualScriptLogicProvider.OpenSteamOverlay("https://steamcommunity.com/linkfilter/?url=https://docs.google.com/document/d/1XN2-FSX3viataUZwPXZUuensG1_KXrcwnvhzIw9Y8RM");
						
            }	
			
			if (MyAPIGateway.Input.IsKeyPress(MyKeys.LeftControl) && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.F2) && ValidInput()) //hey dumbass, use this before the url. fucking keen https://steamcommunity.com/linkfilter/?url={url}
            {
               
                MyVisualScriptLogicProvider.OpenSteamOverlay("https://steamcommunity.com/linkfilter/?url=https://docs.google.com/forms/d/e/1FAIpQLSfz5mPj72pDoPIJP49s8v2GPz453IEfjhsQjzj8FpZzZdwqbg/viewform");
						
            }	
				
        }

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{

            //MyAPIGateway.Utilities.ShowMessage("README", "Press Shift + F2 to open the StarCore Infodoc" );
            MyAPIGateway.Utilities.ShowMessage("README", "Press Ctrl + F2 to open an issue submission form.");
            MyAPIGateway.Utilities.ShowMessage("README", "Press Shift + F2 to open athe build rules.");
            
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
