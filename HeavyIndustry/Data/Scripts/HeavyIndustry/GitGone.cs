using Sandbox.Definitions;
using System.Collections.Generic;

using VRage.Game.Components;
using VRage.Game;
using VRage.Utils;
using VRage.Game.ModAPI;

using Sandbox.ModAPI;
using System.Runtime.CompilerServices;
using Sandbox.Game.Weapons;
using Sandbox.Common.ObjectBuilders;

namespace cleaner
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class BlockRemover : MySessionComponentBase
    {
        private readonly string DUMMY_MODEL = @"Models\Toedisgay.mwm";
        private readonly string[] DUMMY_ICON = { @"Textures\FAKE.dds" };
        private readonly float DUMMY_MASS = 0f;
        private readonly string ADMIN_TAG = "ADMIN";
        private readonly string AI_ENABLED_TAG = "AIEnabled";
        private readonly bool DEBUG = true;

        public HashSet<string> weapon_def_hashset = new HashSet<string>();
        public HashSet<string> cockpit_def_hashset = new HashSet<string>();
        public HashSet<string> power_def_hashset = new HashSet<string>();
        public HashSet<string> logistics_def_hashset = new HashSet<string>();
        public HashSet<string> mobility_def_hashset = new HashSet<string>();
        public HashSet<string> prop_def_hashset = new HashSet<string>();
        public HashSet<string> att_def_hashset = new HashSet<string>();
        public HashSet<string> reentry_def_hashset = new HashSet<string>();

        public List<string> categories = new List<string>(new string[]
        {
            "All",
            "Character",
            "bobpower",
            "bobcockpit",
            "logistics",
            "mobility",
            "onsumable",
			"prop",
			"attitude",
			"reentry",
            "motes"
        });

        public List<string> category_blacklist = new List<string>(new string[]
        {
            "category",
            //"Blocks",
        });

        public List<string> block_type_id_blacklist = new List<string>(new string[]
        {
        });

        public List<string> block_subtype_id_blacklist = new List<string>(new string[]
        {
        });

        public List<string> block_subtype_banned_keywords = new List<string>(new string[]
        {
        });

        public List<string> block_subtype_id_whitelist = new List<string>(new string[]
        {
            //"OpenCockpitSmall",
            //"OpenCockpitLarge",
            //"PassengerSeatSmallOffset",
            //"PassengerSeatSmall",
            //"PassengerSeatSmallNew",
            //"PassengerSeatLarge",
            //"PassengerBench",

            //"SmallBlockBatteryBlock",
            //"SmallBlockSmallBatteryBlock",
            //"LargeBlockBatteryBlock",

            //"JetConveyorSorterer",
            //"Prop2B",
            //"MainHelicopterRotor",
            //"TailHelicopterRotor",
            //"ElectricFan",
            //"ElectricFanDShape",

            //"JetEngineIntake",
            //"SmallDieselEngine",
            //"TinyDieselEngine",
            //"MediumDieselEngine",
            //"LargeHydrogenEngine",

            //"LargeAlternator",
            //"SuspensionConverter",
            //"SmallAlternator",

            //"Propellantx4",
            //"Propellantx5",

            //"aero-wing-plane-air_brake_double_1x1x1_Small",
            //"aero-wing-plane-air_brake_single_1x1x1_Small",
            //"aero-wing-plane-air_brake_single_1x1x1_Large",
            //"aero-wing-plane-air_brake_double_1x1x1_Large",

            //"SmallCameraBlock",

            //"ScorpionGunBlock",
            //"PolybolosGunBlock",
            //"DaggerLaunncher",
            //"MaceLauncher",
            //"ShieldTurret_AIEnabled",
            //"SpearLauncher",
            //"SwordLauncher",
            //"APSTurret",
        });

        /*
        private void ConfigureXLBlocks(MyCubeBlockDefinition def)
        {
            var comp_list = new MyCubeBlockDefinition.Component[1];

            MyComponentDefinition steel_plate = MyDefinitionManager.Static.TryGetDefinition(MyDefinitionId.TryParse("MyObjectBuilder_Component/SteelPlate"), out steel_plate);

            MyCubeBlockDefinition.Component component = new MyCubeBlockDefinition.Component
            {
                Definition = steel_plate,
                Count = 1000,
                DeconstructItem = steel_plate
            };

            comp_list[0] = (component);

            def.Components = comp_list;
        }
        */

        private void ApplyGitGoneFilters()
        {
            foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                MyCubeBlockDefinition block_def = def as MyCubeBlockDefinition;

                if (block_def == null) continue;

                string subtype_id = block_def.Id.SubtypeName;
                string type_id = block_def.Id.TypeId.ToString();

                bool is_banned = block_type_id_blacklist.Contains(type_id) || block_subtype_id_blacklist.Contains(subtype_id);
                bool is_ConveyorSorterer = type_id == "MyObjectBuilder_ConveyorSorter";
                bool is_tank = type_id == "MyObjectBuilder_GasTank";
                bool is_admin = subtype_id.Contains(ADMIN_TAG);
                //bool is_XL_block = subtype_id.Contains("XL");

                foreach (string banned_keyword in block_subtype_banned_keywords)
                {
                    if (subtype_id.Contains(banned_keyword))
                    {
                        is_banned = true;
                    }
                }

                if (block_subtype_id_whitelist.Contains(subtype_id))
                    is_banned = false;

                if(is_ConveyorSorterer)
                {
                    var tdef = block_def as MyConveyorSorterDefinition;

                    if(subtype_id.Contains("RCS") || subtype_id.Contains("Rcs")) // humanoid ConveyorSorterers
                    {
                        is_banned = false;

                    }
                    else if (subtype_id.Contains("Atm"))
                    {
                        is_banned = false;
                    }
                    else if (subtype_id.Contains("ConveyorSorterer") && !subtype_id.Contains("Modular") && !subtype_id.Contains("Prototech"))
                    {
                        is_banned = false;
                    }
                    else
                    {
                        //woopsie
                    }
                }

                if (DEBUG)
                {
                    MyLog.Default.WriteLine($"[TLB-GG] DEBUG: {type_id}/{subtype_id}, bn: {is_banned}, adm: {is_admin}");
                }

                // deprecated code to kill dlcs

                //if (def.DLCs != null)
                //    EviscerateBlockDefinition(blockDef);

                //if (is_XL_block)
                //    ConfigureXLBlocks(block_def);

                if (is_admin)
                {
                    GatekeepBlock(block_def);
                    continue;
                }

                if (is_banned)
                    EviscerateBlockDefinition(block_def);
            }
        }

        private void ChangeDisplayNamesForF10Menu()
        {
            foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var ammo_mag_def = def as MyAmmoMagazineDefinition;
                if (ammo_mag_def != null)
                {
                    ammo_mag_def.DisplayNameString = "USE AMMO RACKS";
                    ammo_mag_def.DisplayNameEnum = MyStringId.GetOrCompute(ammo_mag_def.DisplayNameString);
                    continue;
                }

                var phys_item_def = def as MyPhysicalItemDefinition;
                if (phys_item_def != null && phys_item_def.Id.SubtypeName == "Ice")
                {
                    phys_item_def.DisplayNameString = $"1: {phys_item_def.DisplayNameString}";
                    phys_item_def.DisplayNameEnum = MyStringId.GetOrCompute(phys_item_def.DisplayNameString);
                    continue;
                }

                var hand_wep_def = def as MyWeaponItemDefinition;
                if (hand_wep_def != null)
                {
                    hand_wep_def.DisplayNameString = $"2: {hand_wep_def.DisplayNameString}";
                    hand_wep_def.DisplayNameEnum = MyStringId.GetOrCompute(hand_wep_def.DisplayNameString);
                    continue;
                }

                var hand_item_def = def as MyToolItemDefinition;
                if (hand_item_def != null)
                {
                    hand_item_def.DisplayNameString = $"3: {hand_item_def.DisplayNameString}";
                    hand_item_def.DisplayNameEnum = MyStringId.GetOrCompute(hand_item_def.DisplayNameString);
                    continue;
                }
            }
        }

        private void SortBlockCategories()
        {
            foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var block_def = def as MyCubeBlockDefinition;
                if (block_def == null || block_def.DLCs != null)
                    continue;

                string subtype_id = block_def.Id.SubtypeName;
                string type_id = block_def.Id.TypeId.ToString();

                /*----------------------------------------SKY PIRATES--------------------------------------*/

                if (subtype_id.Contains("ZeppelinTank"))
                {
                    mobility_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }

                if (type_id == "MyObjectBuilder_AdvancedDoor" && subtype_id.Contains("air_brake"))
                {
                    mobility_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }

                /*------------------------------------------WEAPONS---------------------------------------*/

                var turret_def = def as MyLargeTurretBaseDefinition;
                var warhead_def = def as MyWarheadDefinition;
                var fixed_weapon_def = def as MyWeaponBlockDefinition;
                if (turret_def != null || fixed_weapon_def != null || warhead_def != null)
                {
                    if (subtype_id.Contains("pellant"))
                    {
                        mobility_def_hashset.Add(type_id + "/" + subtype_id);
                        continue;
                    }

                    // from weapon ai remover - thanks fire and jp
                    if (turret_def != null && !subtype_id.Contains(ADMIN_TAG) && !subtype_id.Contains(AI_ENABLED_TAG))
                    {
                        turret_def.AiEnabled = false;
                        turret_def.MaxRangeMeters = 0f;
                    }

                    weapon_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }

                /*------------------------------------------COCKPITS---------------------------------------*/

                var cock_def = def as MyCockpitDefinition;
                if (cock_def != null)
                {
                    cockpit_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }

                /*------------------------------------------POWER---------------------------------------*/

                var battery_def = def as MyBatteryBlockDefinition;
                var reactor_def = def as MyReactorDefinition;
                var solar_def = def as MySolarPanelDefinition;
                var wind_def = def as MyWindTurbineDefinition;
                var engine_def = def as MyHydrogenEngineDefinition;
                var generator_def = def as MyOxygenGeneratorDefinition;
                if (generator_def != null || wind_def != null || solar_def != null || reactor_def != null || battery_def != null || engine_def != null)
                {
                    power_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }


                /*------------------------------------------LOGISTICS---------------------------------------*/

                var cargo_def = def as MyCargoContainerDefinition;
                var tank_def = def as MyGasTankDefinition;
                var sorter_def = def as MyConveyorSorterDefinition;
                var connector_def = def as MyShipConnectorDefinition;
                if (cargo_def != null || tank_def != null || sorter_def != null || connector_def != null)
                {
                    logistics_def_hashset.Add(type_id + "/" + subtype_id);
                    if (connector_def == null)
                        continue;
                }
                if (type_id.Contains("Conveyor"))
                {
                    logistics_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }


                /*------------------------------------------MOBILITY---------------------------------------*/


                var ball_def = def as MySpaceBallDefinition;
				var door_def = def as MyAdvancedDoorDefinition;
                var mech_def = def as MyMechanicalConnectionBlockBaseDefinition;
				var merge_def = def as MyMergeBlockDefinition;
                var susp_def = def as MyMotorSuspensionDefinition;
                if (susp_def != null || ball_def != null || mech_def != null || door_def != null || merge_def != null)
                {
                    mobility_def_hashset.Add(type_id + "/" + subtype_id);
                }
				
				/*------------------------------------------ATT---------------------------------------*/
                var gyro_def = def as MyGyroDefinition;
				
				if (def.DisplayNameString != null && def.DisplayNameString.Contains("Control Surface"))
                {
                    att_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }

                if(type_id == "MyObjectBuilder_TerminalBlock" && subtype_id.Contains("aero-wing_") && (subtype_id.Contains("pointed_edge") || subtype_id.Contains("rounded_edge")))
                {
                    att_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }
				
				if (gyro_def != null)
                {
                    att_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }
				
				if (subtype_id.Contains("RCS") || subtype_id.Contains("Rcs") || subtype_id.Contains("Navball")) // humanoid ConveyorSorterers
				{
					att_def_hashset.Add(type_id + "/" + subtype_id);
				}

				/*------------------------------------------REENTRY---------------------------------------*/

				var chute_def = def as MyParachuteDefinition;
				
				if (chute_def != null)
                {
                    reentry_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }
				
				if (subtype_id.Contains("Ceramic") || subtype_id.Contains("BobSpring")) // humanoid ConveyorSorterers
				{
					reentry_def_hashset.Add(type_id + "/" + subtype_id);
				}

				
				/*------------------------------------------PROP---------------------------------------*/
				var ConveyorSorter_def = def as MyConveyorSorterDefinition;
				if(ConveyorSorter_def != null)
				{
                    prop_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
				}
				
            }

            foreach (var entry in MyDefinitionManager.Static.GetCategories())
            {
                var category_def = entry.Value as MyGuiBlockCategoryDefinition;
                if (category_def != null)
                {
                    bool is_valid_category = false;
                    foreach (string name in categories)
                    {
                        if (category_def.Name.Contains(name))
                            is_valid_category = true;
                    }

                    if (!is_valid_category)
                    {
                        EviscerateBlockCategory(category_def);
                    }

                    foreach (string blacklisted_cat in category_blacklist)
                    {
                        if (category_def.Name.Contains(blacklisted_cat))
                        {
                            EviscerateBlockCategory(category_def);
                        }
                    }

                    if (category_def.Name.Contains("Weapons"))
                    {
                        foreach (string block in weapon_def_hashset)
                            category_def.ItemIds.Add(block);
                    }
                    if (category_def.Name.Contains("bobcockpit"))
                    {
                        foreach (string block in cockpit_def_hashset)
                            category_def.ItemIds.Add(block);
                    }
                    if (category_def.Name.Contains("logistics"))
                    {
                        foreach (string block in logistics_def_hashset)
                            category_def.ItemIds.Add(block);
                    }
                    if (category_def.Name.Contains("bobpower"))
                    {
                        foreach (string block in power_def_hashset)
                            category_def.ItemIds.Add(block);
                    }
                    if (category_def.Name.Contains("mobility"))
                    {
                        foreach (string block in mobility_def_hashset)
                            category_def.ItemIds.Add(block);
                    }
					if (category_def.Name.Contains("prop"))
                    {
                        foreach (string block in prop_def_hashset)
                            category_def.ItemIds.Add(block);
                    }
					if (category_def.Name.Contains("attitude"))
                    {
                        foreach (string block in att_def_hashset)
                            category_def.ItemIds.Add(block);
                    }
					if (category_def.Name.Contains("reentry"))
                    {
                        foreach (string block in reentry_def_hashset)
                            category_def.ItemIds.Add(block);
                    }
                }
            }
        }

        /// <summary>
        /// Removes access to admin blocks for subhumans. Reload game if u still dont have access. thx
        /// </summary>
        /// <param name="def"></param>
        private void GatekeepBlock(MyCubeBlockDefinition def)
        {
            bool is_promoted = MyAPIGateway.Session.PromoteLevel >= MyPromoteLevel.SpaceMaster;
            bool is_host = MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer || MyAPIGateway.Utilities.IsDedicated;

            if (!is_promoted && !is_host)
            {
                def.Public = false;
                def.GuiVisible = false;
                def.Icons = DUMMY_ICON;
            }
        }

        private void EviscerateBlockDefinition(MyCubeBlockDefinition def)
        {
            //blockDef.DisplayName == "DoNotUse";
            def.Model = DUMMY_MODEL;
            def.HasPhysics = false;
            def.Mass = DUMMY_MASS;
            def.MountPoints = null;
            def.Public = false;
            def.GuiVisible = false;
            def.Icons = DUMMY_ICON;
        }

        private void EviscerateBlockCategory(MyGuiBlockCategoryDefinition def)
        {
            def.Public = false;
            def.ShowInCreative = false;
            def.ItemIds?.Clear();
        }

        public override void BeforeStart()
        {
            ApplyGitGoneFilters();
            SortBlockCategories();
            // ChangeDisplayNamesForF10Menu();
        }
    }
}
