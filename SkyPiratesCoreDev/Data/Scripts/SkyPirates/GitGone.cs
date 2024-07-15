using Sandbox.Definitions;
using System.Collections.Generic;

using VRage.Game.Components;
using VRage.Game;
using VRage.Utils;
using VRage.Game.ModAPI;

using Sandbox.ModAPI;
using System.Runtime.CompilerServices;

namespace cleaner
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class BlockRemover : MySessionComponentBase
    {
        private readonly string DUMMY_MODEL = @"Models\Toedisgay.mwm";
        private readonly string[] DUMMY_ICON = {@"Textures\FAKE.dds"};
        private readonly float DUMMY_MASS = 0f;
        private readonly string ADMIN_TAG = "ADMIN";
        private readonly bool DEBUG = false;

        public HashSet<string> weapon_def_hashset = new HashSet<string>();
        public HashSet<string> cockpit_def_hashset = new HashSet<string>();
        public HashSet<string> power_def_hashset = new HashSet<string>();
        public HashSet<string> logistics_def_hashset = new HashSet<string>();
        public HashSet<string> mobility_def_hashset = new HashSet<string>();

        public List<string> categories = new List<string>(new string[] {
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

        public List<string> category_blacklist = new List<string>(new string[] {
            "category",
            "Blocks",
        });

        public List<string> block_type_id_blacklist = new List<string>(new string[] {
            "MyObjectBuilder_Thrust",
            "MyObjectBuilder_Cockpit",
            "MyObjectBuilder_Reactor",
            "MyObjectBuilder_BatteryBlock",
            "MyObjectBuilder_OxygenTank",
            "MyObjectBuilder_OxygenGenerator",
            "MyObjectBuilder_HydrogenEngine",
        });

        public List<string> block_subtype_id_blacklist = new List<string>(new string[] {
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

            "LargeCameraBlock",
            "SmallCameraBlock",
            "LargeCameraTopMounted",
            "SmallCameraTopMounted",
        });

        public List<string> block_subtype_id_whitelist = new List<string>(new string[] {
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

        private void ApplyGitGoneFilters()
        {
            foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                MyCubeBlockDefinition block_def = def as MyCubeBlockDefinition;

                if (block_def == null) continue;

                string subtype_id = block_def.Id.SubtypeName;
                string type_id = block_def.Id.TypeId.ToString();

                bool is_banned = block_type_id_blacklist.Contains(type_id) && !block_subtype_id_whitelist.Contains(subtype_id) || block_subtype_id_blacklist.Contains(subtype_id);
                bool is_admin = subtype_id.Contains(ADMIN_TAG);

                if (DEBUG)
                {
                    MyLog.Default.WriteLine($"[TLB-GG] DEBUG: {type_id}/{subtype_id}, bn: {is_banned}, adm: {is_admin}");
                }

                // deprecated code to kill dlcs

                //if (def.DLCs != null)
                //    EviscerateBlockDefinition(blockDef);

                if (is_admin)
                    GatekeepBlock(block_def);

                if (is_banned)
                    EviscerateBlockDefinition(block_def);

            }
        }

        private void SortBlockCategories()
        {
            foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var block_def = def as MyCubeBlockDefinition;
                if (block_def == null)
                    continue;

                string subtype_id = block_def.Id.SubtypeName;
                string type_id = block_def.Id.TypeId.ToString();

                if (subtype_id.Contains("ZeppelinTank"))
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
                    if (turret_def != null && !subtype_id.Contains(ADMIN_TAG))
                    {
                        turret_def.AiEnabled = false;
                        turret_def.MaxRangeMeters = 0f;
                    }

                    weapon_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }

                /*------------------------------------------COCKPITS---------------------------------------*/

                var cockdef = def as MyCockpitDefinition;
                if (cockdef != null)
                {
                    cockpit_def_hashset.Add(type_id + "/" + subtype_id);
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
                    power_def_hashset.Add(type_id + "/" + subtype_id);
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
                    logistics_def_hashset.Add(type_id + "/" + subtype_id);
                    if (connectordef == null)
                        continue;
                }
                if (type_id.Contains("Conveyor"))
                {
                    logistics_def_hashset.Add(type_id + "/" + subtype_id);
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
                    mobility_def_hashset.Add(type_id + "/" + subtype_id);
                    continue;
                }

                if (def.DisplayNameString != null && def.DisplayNameString.Contains("Control Surface"))
                {
                    mobility_def_hashset.Add(type_id + "/" + subtype_id);
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
        }
    }
}
