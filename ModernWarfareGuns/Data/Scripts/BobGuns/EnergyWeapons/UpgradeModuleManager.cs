using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Lights;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using ProtoBuf;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;
using VRageMath;

/*
This script file is used to force upgrade modules to stay in an On status.
This is important for upgrades that change power draw values, since players
could potentially disable an upgrade block until charging completes, and then
re-enable to gain the other benefits of the upgrade.
*/

namespace MWI2_CustomEnergyWeapons{
	
	//Add your upgrade block subtypeids to the line below at the indicated positions
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "YourUpgradeBlockSubtypeId", "AnotherUpgradeBlockId", "LargeProductivityModuleTest")]
	 
	public class MWI2_UpgradeModuleManager : MyGameLogicComponent{
		
		public IMyUpgradeModule UpgradeBlock;
		public bool Setup = false;
		
		public override void Init(MyObjectBuilder_EntityBase objectBuilder){
			
			base.Init(objectBuilder);
			
			try{
				
				NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
				NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME; //uncomment if you want method that runs every 100 ticks as well
				
			}catch(Exception exc){
				
				
				
			}
			
		}
		
		public override void UpdateBeforeSimulation(){
			
			if(Setup == false){
				
				Setup = true;
				UpgradeBlock = Entity as IMyUpgradeModule;
				UpgradeBlock.IsWorkingChanged += WorkingChanged;
				NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
			
			}
			
		}
		
		public override void UpdateBeforeSimulation100(){
			
			
			
		}
		
		public void WorkingChanged(IMyCubeBlock cubeBlock){
			
			if(UpgradeBlock.Enabled == false){
				
				UpgradeBlock.Enabled = true;
				
			}
			
		}
		
		public override void OnRemovedFromScene(){
			
			base.OnRemovedFromScene();
			
			var Block = Entity as IMyUpgradeModule;
			
			if(Block == null){
				
				return;
				
			}
			
			//Unregister any handlers here
			Block.IsWorkingChanged -= WorkingChanged;
			
		}
		
		public override void OnBeforeRemovedFromContainer(){
			
			base.OnBeforeRemovedFromContainer();
			
			if(Entity.InScene == true){
				
				OnRemovedFromScene();
				
			}
			
		}
		
	}
	
}