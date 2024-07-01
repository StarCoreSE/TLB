using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.AI.Pathfinding;
using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using VRage;
using VRage.FileSystem;
using VRage.Game;
using VRage.Game.Definitions;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.Generics;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRageRender;
using Sandbox.ModAPI;
using VRage.Game.Definitions;
using VRage.Game.ObjectBuilders.Definitions;


namespace cleaner
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class BlockRemover : MySessionComponentBase
    {
        public string Model = @"Models\Toedisgay.mwm";
		
		public string[] Icons = {@"Textures\FAKE.dds"};
		
		public float Mass = 0f;

        private bool isInit = false;

        public HashSet<string> weaponhash = new HashSet<string>();
        public HashSet<string> cockpithash = new HashSet<string>();
        public HashSet<string> powerhash = new HashSet<string>();
        public HashSet<string> logistics = new HashSet<string>();
        public HashSet<string> mobility = new HashSet<string>();

        public int cancelled = 0;
        public string strings = "";

        public List<string> cats = new List<string>(new string[] {
            "Large",
            "All",
            "Small",
            "Tools",
            "Weapons",
            "bobpower",
            "bobcockpit",
            "logistics",
            "mobility",
            "onsumable",
            "motes"
            });

        public List<string> blacklist = new List<string>(new string[] {
            "category",
            "Blocks",
            });

        public List<string> bannedids = new List<string>(new string[] {
            "Thrust",
            "Cockpit",
            "Reactor",
            "BatteryBlock",
            "OxygenTank",
            "OxygenGenerator",
            "HydrogenEngine",

            "LargeRailgun",
            "SmallRailgun",
            "SmallBlockMediumCalibreTurret",
            "AutoCannonTurret",
            "LargeMissileLauncher",
            "LargeBlockLargeCalibreGun",

            "LargeJumpDrive",
            "GravityGenerator",
            "GravityGeneratorSphere",
            "VirtualMassLarge",
            "RotatingLightLarge",
            "RotatingLightSmall", 
            "StoreBlock",
            "AtmBlock",
            "SafeZoneBlock",

            "Small_Elevator_Straight",
            
            "Small_Elevator_Straight_Centered",
            "Small_2x1_Control_Centered",
            "Small_2x1_Control_Flat",
            "Small_Rudder_Straight_Flat",
            });

        public List<string> whitelist = new List<string>(new string[] {
            "OpenCockpitSmall",
            "OpenCockpitLarge",
            "ZClassicCockpit",
            "PassengerSeatSmallOffset",
            "PassengerSeatSmall",
            "PassengerSeatSmallNew",
            "PassengerSeatLarge",

            "SmallBlockBatteryBlock",
            "LargeBlockBatteryBlock",

            "ZeppelinPropeller",
            "ZeppelinPropellerSmall",
            "ZeppelinPropellerLarge",
            "Prop2B",
            "Prop3B",
            "Prop4B",

            "SmallDieselEngine",
            "TinyDieselEngine",
            "MediumDieselEngine",
            "LargeHydrogenEngine"
        });

        private void DoWork()
        {
            foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;

                if (blockDef == null) continue;

                //if (def.DLCs != null)
                //{
                //    blockDef.Public = false;
                //    blockDef.GuiVisible = false;
                //    //eviscerate(blockDef);
                //}

                foreach (string subtypeid in bannedids)
                {
                    if (whitelist.Contains(blockDef.Id.SubtypeName))
                        continue;

                    if (blockDef.Id.SubtypeName == subtypeid)
                    {
                        eviscerate(blockDef);
                    } else if (blockDef.Id.TypeId.ToString().Contains(subtypeid))
                        eviscerate(blockDef);

                }
            }
        }

        private void eviscerate(MyCubeBlockDefinition blockDef)
        {
            //blockDef.DisplayName == "DoNotUse";
            blockDef.Model = Model;
            blockDef.HasPhysics = false;
            blockDef.Mass = Mass;
            blockDef.MountPoints = null;
            blockDef.Public = false;
            blockDef.GuiVisible = false;
            blockDef.Icons = Icons;
        }

        public override void BeforeStart()
        {

            DoWork();

            foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var checkdef = def as MyCubeBlockDefinition;
                if (checkdef == null)
                    continue;

                if (def.Id.SubtypeName.Contains("ZeppelinTank"))
                {
                    mobility.Add(def.Id.TypeId.ToString() + "/" + def.Id.SubtypeName);
                    continue;
                }

                /*------------------------------------------WEAPONS---------------------------------------*/

                var turretDef = def as MyLargeTurretBaseDefinition;
                var warheaddef = def as MyWarheadDefinition;
                var fixdef = def as MyWeaponBlockDefinition;
                if (turretDef != null || fixdef != null || warheaddef != null)
                {
                    if (def.Id.SubtypeName.Contains("pellant"))
                    {
                        mobility.Add(def.Id.TypeId.ToString() + "/" + def.Id.SubtypeName);
                        continue;
                    }
                    if (def.Id.SubtypeName.Contains("halfmin") || def.Id.SubtypeName.Contains("insurgent"))
                        continue;
                    weaponhash.Add(def.Id.TypeId.ToString() + "/" + def.Id.SubtypeName);
                    continue;
                }

                /*------------------------------------------COCKPITS---------------------------------------*/

                var cockdef = def as MyCockpitDefinition;
                if (cockdef != null)
                {
                    cockpithash.Add(def.Id.TypeId.ToString() + "/" + def.Id.SubtypeName);
                    continue;
                }

                /*------------------------------------------POWER---------------------------------------*/

                var batterydef = def as MyBatteryBlockDefinition;
                var reactordef = def as MyReactorDefinition;
                var solardef = def as MyWeaponBlockDefinition;
                var winddef = def as MyWindTurbineDefinition;
                var enginedef = def as MyHydrogenEngineDefinition;
                var generatordef = def as MyOxygenGeneratorDefinition;
                if (generatordef != null || winddef != null || solardef != null || reactordef != null || batterydef != null || enginedef != null)
                {
                    powerhash.Add(def.Id.TypeId.ToString() + "/" + def.Id.SubtypeName);
                    continue;
                }


                /*------------------------------------------LOGISTICS---------------------------------------*/

                var cargodef = def as MyCargoContainerDefinition;
                var tankdef = def as MyGasTankDefinition;
                var sorterdef = def as MyConveyorSorterDefinition;
                var connectordef = def as MyShipConnectorDefinition;
                var mergedef = def as MyMergeBlockDefinition;
                if (cargodef != null || tankdef != null || sorterdef != null || connectordef != null || mergedef != null)
                {
                    logistics.Add(def.Id.TypeId.ToString() + "/" + def.Id.SubtypeName);
                    if(connectordef == null)
                        continue;
                }
                if (def.Id.TypeId.ToString().Contains("Conveyor"))
                {
                    logistics.Add(def.Id.TypeId.ToString() + "/" + def.Id.SubtypeName);
                    continue;
                }


                /*------------------------------------------MOBILITY---------------------------------------*/


                var suspensiondef = def as MyMotorSuspensionDefinition;
                var balldef = def as MySpaceBallDefinition;
                var mechdef = def as MyMechanicalConnectionBlockBaseDefinition;
                var thrusterdef = def as MyThrustDefinition;
                var gyrodef = def as MyGyroDefinition;
                if (suspensiondef != null || balldef != null || mechdef != null || thrusterdef != null || gyrodef != null)
                {
                    mobility.Add(def.Id.TypeId.ToString() + "/" + def.Id.SubtypeName);
                    continue;
                }

                if (def.DisplayNameString != null && def.DisplayNameString.Contains("Control Surface"))
                {
                    mobility.Add(def.Id.TypeId.ToString() + "/" + def.Id.SubtypeName);
                    continue;
                }
            }

            foreach (var entry in MyDefinitionManager.Static.GetCategories())
            {
                var categorydef = entry.Value as MyGuiBlockCategoryDefinition;
                if (categorydef != null)
                {
                    bool okie = false;
                    strings = strings + " : " + categorydef.Name;
                    foreach (string name in cats)
                    {
                        if (categorydef.Name.Contains(name))
                            okie = true;
                    }
                    if (okie == false)
                    {
                        categorydef.Public = false;
                        categorydef.ShowInCreative = false;
                        categorydef.ItemIds.Clear();
                    }
                    foreach (string name in blacklist)
                    {
                        if (categorydef.Name.Contains(name))
                        {
                            categorydef.Public = false;
                            categorydef.ShowInCreative = false;
                        }
                    }


                    if (categorydef.Name.Contains("Weapons"))
                    {
                        foreach (string block in weaponhash)
                            categorydef.ItemIds.Add(block);
                    }
                    if (categorydef.Name.Contains("bobcockpit"))
                    {
                        foreach (string block in cockpithash)
                            categorydef.ItemIds.Add(block);
                    }
                    if (categorydef.Name.Contains("logistics"))
                    {
                        foreach (string block in logistics)
                            categorydef.ItemIds.Add(block);
                    }
                    if (categorydef.Name.Contains("bobpower"))
                    {
                        foreach (string block in powerhash)
                            categorydef.ItemIds.Add(block);
                    }
                    if (categorydef.Name.Contains("mobility"))
                    {
                        foreach (string block in mobility)
                            categorydef.ItemIds.Add(block);
                    }
                }
            }
        }
    }
}