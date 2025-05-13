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
using VRage.Game.Components;
using Sandbox.Game.ParticleEffects;
using static VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GameDefinition;
using Sandbox.Common.ObjectBuilders;
using static HeavyIndustry.DefinitionRedefiner;
using SpaceEngineers.Game.Entities.Blocks;
using System.Collections;
using ResourceNodes;


namespace HeavyIndustry
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class DefinitionRedefiner : MySessionComponentBase
    {

        public bool isInit = false;

        public HashSet<MyCubeBlockDefinition> cubeDefs = new HashSet<MyCubeBlockDefinition>();

        public Dictionary<string, MyComponentDefinition> compDefs = new Dictionary<string, MyComponentDefinition>();

        public Dictionary<string, MyPhysicalItemDefinition> oreDefs = new Dictionary<string, MyPhysicalItemDefinition>();

        public Dictionary<string, MyResearchBlockDefinition> rsblockDefs = new Dictionary<string, MyResearchBlockDefinition>();

        public Dictionary<string, MyResearchDefinition> rsDefs = new Dictionary<string, MyResearchDefinition>();

        public Dictionary<string, MyResearchGroupDefinition> rsGroupDefs = new Dictionary<string, MyResearchGroupDefinition>();


        public List<string> validParts = new List<string>();

        public List<string> t1Parts = new List<string>()
        {
            //T1
            "StructuralParts",
            "ConcreteParts",
            "WheelParts",
            "MechanicalParts",
            "Computer", // CircuitParts
            "GlassParts",
            "ProjectileParts",
        };

        public List<string> t2Parts = new List<string>()
        {
            //T2
            "ComputerParts",
            "ArmorParts",
            "AeroParts",
            "CommunicationParts",
            "ElectricalParts",
            "ExplosiveParts",
            "MissileParts",
        };

        public List<string> t3Parts = new List<string>()
        {
            //T3
            "SolarParts",
            "LaserParts",
            "MagneticParts",
            "AutomationParts",
            "AstroParts",

        };

        public List<string> t4Parts = new List<string>()
        {
            //T4
            "ReactorParts",
            "IonThrusterParts",
            "TemporalisationParts",
            "GravityParts",
        };

        public Dictionary<string, string> compReplacements = new Dictionary<string, string>()
        {
            { "SteelPlate","StructuralParts" },
            { "LargeTube","StructuralParts"},
            { "SmallTube","StructuralParts" },
            { "Construction","StructuralParts" },
            { "InteriorPlate","StructuralParts" },
            { "Girder","StructuralParts" },

            { "Display","Computer" },
            { "Detector","Computer" },
            { "Medical","Computer" },
            { "Motor", "MechanicalParts" },
            { "MetalGrid", "ArmorParts" },

            { "BulletproofGlass","GlassParts" },
            { "RadioCommunication","CommunicationParts" },
            { "PowerCell","ElectricalParts" },
            { "Explosives","ExplosiveParts" },

            { "SolarCell", "SolarParts" },
            { "Reactor", "ReactorParts" }

        };

        int theseExist = 0;

        /// <summary>
        /// Loop through all definition manager defs and manage 'em.
        /// </summary>
        private void UpdateAllDefinitions()
        {
            UpdateAllPhysicalItems();
            UpdateAllComponents();
            UpdateAllCubes();
            UpdateResearchBlocks();
            UpdateOreMap();
        }

        private void UpdateToolBlueprint(MyPhysicalItemDefinition toolDef)
        {
            MyBlueprintDefinitionBase bpdef = FindBlueprint(toolDef.Id);

            if (bpdef == null)
                return;

            var bpclass = MyDefinitionManager.Static.GetBlueprintClass("Tools");

            if (!bpclass.ContainsBlueprint(bpdef))
                bpclass.AddBlueprint(bpdef);

            bool enhanced = toolDef.Id.SubtypeName.Contains("2Item");
            bool proficient = toolDef.Id.SubtypeName.Contains("3Item");
            bool elite = toolDef.Id.SubtypeName.Contains("4Item");
            bool basic = !enhanced && !proficient && !elite;

            List<MyBlueprintDefinition.Item> items = new List<MyBlueprintDefinition.Item>();
            MyBlueprintDefinition.Item item;

            if (basic)
            {
                return;
            }
            else if(enhanced)
            {
                item = new MyBlueprintDefinition.Item();
                item.Amount = 3;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/Computer");
                items.Add(item);

                item = new MyBlueprintDefinition.Item();
                item.Amount = 5;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/MechanicalParts");
                items.Add(item);
            }
            else if(proficient)
            {
                item = new MyBlueprintDefinition.Item();
                item.Amount = 3;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/ElectricalParts");
                items.Add(item);

                item = new MyBlueprintDefinition.Item();
                item.Amount = 5;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/Computer");
                items.Add(item);

                item = new MyBlueprintDefinition.Item();
                item.Amount = 7;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/MechanicalParts");
                items.Add(item);
            }
            else if(elite)
            {
                item = new MyBlueprintDefinition.Item();
                item.Amount = 3;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/MagneticParts");
                items.Add(item);

                item = new MyBlueprintDefinition.Item();
                item.Amount = 5;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/ElectricalParts");
                items.Add(item);

                item = new MyBlueprintDefinition.Item();
                item.Amount = 7;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/Computer");
                items.Add(item);

                item = new MyBlueprintDefinition.Item();
                item.Amount = 9;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/MechanicalParts");
                items.Add(item);
            }

            bpdef.Prerequisites = items.ToArray();
        }


        private void UpdateWeaponBlueprint(MyWeaponItemDefinition wepDef)
        {
            MyBlueprintDefinitionBase bpdef = FindBlueprint(wepDef.Id);

            if (bpdef == null)
                return;

            var bpclass = MyDefinitionManager.Static.GetBlueprintClass("BasicWeapons");

            if (!bpclass.ContainsBlueprint(bpdef))
                bpclass.AddBlueprint(bpdef);

            MyWeaponDefinition wep = MyDefinitionManager.Static.GetWeaponDefinition(wepDef.WeaponDefinitionId);
            MyAmmoMagazineDefinition mag;
            MyAmmoDefinition ammo;

            if (wep != null)
            {
                mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wep.AmmoMagazinesId[0]);
                if (mag != null)
                {
                    ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                    if (ammo == null)
                    {
                        return;

                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }

            int rof = 0;

            if(wep.HasMissileAmmoDefined)
            {
                rof = wep.WeaponAmmoDatas[1].RateOfFire;
            }
            else if(wep.HasProjectileAmmoDefined)
            {
                rof = wep.WeaponAmmoDatas[0].RateOfFire;
            }

            var ammoInts = new AmmoInts(mag, ammo);

            List<MyBlueprintDefinition.Item> items = new List<MyBlueprintDefinition.Item>();
            MyBlueprintDefinition.Item item;

            item = new MyBlueprintDefinition.Item();
            item.Amount = 5;
            item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/StructuralParts");
            items.Add(item);

            item = new MyBlueprintDefinition.Item();
            item.Amount = (int)(10 *  rof / 200);
            item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/MechanicalParts");
            items.Add(item);

            if (ammoInts.ap > 0)
            {
                item = new MyBlueprintDefinition.Item();
                item.Amount = ammoInts.ap;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/MechanicalParts");
                items.Add(item);
            }

            if (ammoInts.he > 0)
            {
                item = new MyBlueprintDefinition.Item();
                item.Amount = ammoInts.he;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/ComputerParts");
                items.Add(item);
            }

            if (ammoInts.prop > 0)
            {
                item = new MyBlueprintDefinition.Item();
                item.Amount = ammoInts.prop;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/AeroParts");
                items.Add(item);
            }

            if (ammoInts.guid > 0)
            {
                item = new MyBlueprintDefinition.Item();
                item.Amount = ammoInts.guid;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/CommunicationParts");
                items.Add(item);
            }

            if (ammoInts.rail > 0)
            {
                item = new MyBlueprintDefinition.Item();
                item.Amount = ammoInts.rail;
                item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/MagneticParts");
                items.Add(item);
            }

            bpdef.Prerequisites = items.ToArray();
        }

        MyBlueprintDefinitionBase FindBlueprint(MyDefinitionId id)
        {

            foreach (MyBlueprintDefinitionBase def in MyDefinitionManager.Static.GetBlueprintDefinitions())
            {
                foreach (MyBlueprintDefinitionBase.Item item in def.Results)
                {

                    if (item.Id.SubtypeName == id.SubtypeName)
                        return def;
                }
            }

            return null;
        }

        public struct AmmoInts
        {
            public int ap;
            public int he;
            public int prop;
            public int guid;
            public int rail;

            public AmmoInts(MyAmmoMagazineDefinition mag, MyAmmoDefinition ammo)
            {
                ap = 0;
                he = 0;
                prop = 0;
                guid = 0;
                rail = 0;
                
                guid = (int)Math.Round(Math.Min(ammo.MaxTrajectory - 1000, 0) / 100);

                if (ammo is MyMissileAmmoDefinition)
                {
                    var missile = ammo as MyMissileAmmoDefinition;
                    ap = (int)Math.Round(missile.MissileHealthPool / 100);
                    he = (int)Math.Round(missile.MissileExplosionDamage * Math.PI * Math.Pow(missile.MissileExplosionRadius, 2) * missile.ExplosiveDamageMultiplier / 100);
                    prop = (int)Math.Round(2 * (ap + he) * (missile.DesiredSpeed + missile.MissileInitialSpeed) / 1000);

                    if (ammo.Id.SubtypeName.Contains("ailgun"))
                    {
                        ap *= 2;
                        rail = prop / 3;
                        prop = 0;
                    }

                }
                else if (ammo is MyProjectileAmmoDefinition)
                {
                    var proj = ammo as MyProjectileAmmoDefinition;
                    ap = (int)Math.Round(proj.ProjectileMassDamage / 100);
                    prop = (int)Math.Round(2 * (ap + he) * (proj.DesiredSpeed) / 1000);
                }

                ap *= mag.Capacity;
                he *= mag.Capacity;
                prop *= mag.Capacity;
                guid *= mag.Capacity;
                rail *= mag.Capacity;
            }
        }

        private void UpdateAmmoBluePrints()
        {
            foreach (MyAmmoMagazineDefinition ammodef in MyDefinitionManager.Static.GetDefinitionsOfType<MyAmmoMagazineDefinition>())
            {

                MyBlueprintDefinitionBase bpdef = FindBlueprint(ammodef.Id);


                if (bpdef == null)
                {
                    continue;
                }

                var bpclass = MyDefinitionManager.Static.GetBlueprintClass("Ammo");

                if(!bpclass.ContainsBlueprint(bpdef))
                    bpclass.AddBlueprint(bpdef);


                MyAmmoDefinition ammo = MyDefinitionManager.Static.GetAmmoDefinition(ammodef.AmmoDefinitionId);

                var ammoInts = new AmmoInts(ammodef, ammo);

                List<MyBlueprintDefinition.Item> items = new List<MyBlueprintDefinition.Item>();

                if(ammoInts.ap > 0)
                {
                    var item = new MyBlueprintDefinition.Item();
                    item.Amount = ammoInts.ap;
                    item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/StructuralParts");
                    items.Add(item);
                }

                if (ammoInts.he > 0)
                {
                    var item = new MyBlueprintDefinition.Item();
                    item.Amount = ammoInts.he;
                    item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/ExplosiveParts");
                    items.Add(item);
                }

                if (ammoInts.prop > 0)
                {
                    var item = new MyBlueprintDefinition.Item();
                    item.Amount = ammoInts.prop;
                    item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/MissileParts");
                    items.Add(item);
                }

                if (ammoInts.guid > 0)
                {
                    var item = new MyBlueprintDefinition.Item();
                    item.Amount = ammoInts.guid;
                    item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/CommunicationParts");
                    items.Add(item);
                }

                if (ammoInts.rail > 0)
                {
                    var item = new MyBlueprintDefinition.Item();
                    item.Amount = ammoInts.rail;
                    item.Id = MyDefinitionId.Parse("MyObjectBuilder_Component/MagneticParts");
                    items.Add(item);
                }

                bpdef.Prerequisites = items.ToArray();
            }
        }

        private void UpdateBlueprints()
        {

            foreach (MyBlueprintDefinitionBase def in MyDefinitionManager.Static.GetBlueprintDefinitions())
            {
                //theseExist++;

                List<MyBlueprintDefinitionBase.Item> items = new List<MyBlueprintDefinitionBase.Item>();

                items = def.Prerequisites.ToList();

                //var item = items[0];
                //item.Amount = 10000;

                //items.Add(item);

                def.Prerequisites = items.ToArray();
            }
        }
        
        private void UpdateResearchGroups()
        {
            foreach (MyResearchGroupDefinition def in MyDefinitionManager.Static.GetResearchGroupDefinitions())
            {
                //if (def.Members.Count() == 0) continue;
                //SerializableDefinitionId[] myArray = new SerializableDefinitionId[] { };
                def.Members[0] = new SerializableDefinitionId(MyObjectBuilderType.Parse("MyObjectBuilder_Assembler"), "BasicAssembler");
            }
        }
        
        private void UpdateResearchBlocks()
        {

            foreach (MyResearchBlockDefinition def in MyDefinitionManager.Static.GetResearchBlockDefinitions())
            {
                //MyDefinitionManager.Static.Definitions.RemoveDefinition(ref def.Id);

                def.UnlockedByGroups = null;
                def.AvailableInSurvival = false;
                def.UnlockedByGroups = new string[0];
            }
        }


        private string[] CheckTechRequirements()
        {
            return null;
        }

        private void UpdateAllComponents()
        {
            foreach (MyComponentDefinition def in MyDefinitionManager.Static.GetDefinitionsOfType<MyComponentDefinition>())
            {



                def.Volume *= 3;
                def.Mass *= 3;
            }
        }

        private void UpdateAllVoxels()
        {
            foreach (MyVoxelMaterialDefinition def in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
            {
                def.MinedOreRatio /= 10;
            }
        }

        private void UpdateAllPhysicalItems()
        {
            foreach (MyPhysicalItemDefinition def in MyDefinitionManager.Static.GetDefinitionsOfType<MyPhysicalItemDefinition>())
            {


                if (def.Id.TypeId.ToString().Contains("Ore"))
                {
                    def.DisplayNameEnum = MyStringId.GetOrCompute("Ore: " + def.DisplayNameText.Replace(" Ore", ""));
                    def.Volume *= 5;
                    def.Mass *= 5;
                }
                else if (def.Id.TypeId.ToString().Contains("Ingot"))
                {
                    def.DisplayNameEnum = MyStringId.GetOrCompute("Ingot: " + def.DisplayNameText);
                    def.Volume *= 2;
                    def.Mass *= 2;
                }
                else if (def is MyComponentDefinition)
                {

                    if (def.DisplayNameText.Contains("Prototech"))
                    {
                        def.DisplayNameEnum = MyStringId.GetOrCompute(def.DisplayNameText.Replace("Prototech", "Prototech:"));
                    }
                    else if(def.DisplayNameText.Contains("Tech"))
                    {
                        //donothing
                    }
                    else if (def.DisplayNameText.Contains("Science"))
                    {
                        def.DisplayNameEnum = MyStringId.GetOrCompute("Science: " + def.DisplayNameText.Replace("Science", ""));
                    }
                    else if (!def.DisplayNameText.Contains("Parts"))
                    {
                        def.DisplayNameEnum = MyStringId.GetOrCompute("Comp: " + def.DisplayNameText);
                    }
                    else
                    {
                        def.DisplayNameEnum = MyStringId.GetOrCompute("Part: " + def.DisplayNameText.Replace("Parts", ""));
                    }

                }
                else if (def.Id.SubtypeName.Contains("elder") || def.Id.SubtypeName.Contains("rill") || def.Id.SubtypeName.Contains("rinder"))
                {
                    def.DisplayNameEnum = MyStringId.GetOrCompute("Tool: " + def.DisplayNameText);
                    UpdateToolBlueprint(def);
                }
                else if (def is MyWeaponItemDefinition)
                {
                    def.DisplayNameEnum = MyStringId.GetOrCompute("Weapon: " + def.DisplayNameText);
                    UpdateWeaponBlueprint(def as MyWeaponItemDefinition);
                }
                else if (def is MyAmmoMagazineDefinition)
                {
                    def.DisplayNameEnum = MyStringId.GetOrCompute("Ammo: " + def.DisplayNameText);
                }
                else
                {
                    def.DisplayNameEnum = MyStringId.GetOrCompute("Item: " + def.DisplayNameText);
                }
            }
        }

        private void UpdateOreMap()
        {
            var allPlanets = MyDefinitionManager.Static.GetPlanetsGeneratorsDefinitions();

            foreach (var def in allPlanets)
            {

                var planet = def as MyPlanetGeneratorDefinition;
                var oreList = new List<MyPlanetOreMapping>(planet.OreMappings.ToList());

                for (int i = 0; i < oreList.Count; i++)
                {
                    var oreMap = planet.OreMappings[i];

                    if (oreMap.Type.Contains("Ice_01") == true) { oreMap.Start = 0; oreMap.Depth = 10; }

                    if (oreMap.Type.Contains("Iron_02") == true) { oreMap.Start = 0; oreMap.Depth = 10; }
                    if (oreMap.Type.Contains("Nickel_01") == true) { oreMap.Start = 0; oreMap.Depth = 10; }
                    if (oreMap.Type.Contains("Silicon_01") == true) { oreMap.Start = 0; oreMap.Depth = 10; }

                    if (oreMap.Type.Contains("Cobalt_01") == true) { oreMap.Start = 0; oreMap.Depth = 10; }
                    if (oreMap.Type.Contains("Magnesium_01") == true) { oreMap.Start = 0; oreMap.Depth = 10; }

                    if (oreMap.Type.Contains("Silver_01") == true) { oreMap.Start = 0; oreMap.Depth = 10; }
                    if (oreMap.Type.Contains("Gold_01") == true) { oreMap.Start = 0; oreMap.Depth = 10; }

                    if (oreMap.Type.Contains("Platinum_01") == true) { oreMap.Start = 0; oreMap.Depth = 10; }
                    if (oreMap.Type.Contains("Uraninite_01") == true) { oreMap.Start = 0; oreMap.Depth = 10; }
                }

                planet.OreMappings = oreList.ToArray();

            }
        }

        /// <summary>
        /// Loop through ever cube definition and spank it.
        /// </summary>
        private void UpdateAllCubes()
        {
            foreach (MyCubeBlockDefinition def in MyDefinitionManager.Static.GetDefinitionsOfType<MyCubeBlockDefinition>())
            {
                float buildTime = def.MaxIntegrity / def.IntegrityPointsPerSec;
                string subtype_id = def.Id.SubtypeName;
                string type_id = def.Id.TypeId.ToString();

                var thrust_def = def as MyThrustDefinition;
                var battery_def = def as MyBatteryBlockDefinition;
                var turret_def = def as MyLargeTurretBaseDefinition;
                var warhead_def = def as MyWarheadDefinition;
                var weapon_def = def as MyWeaponBlockDefinition;
                var cock_def = def as MyCockpitDefinition;
                var reactor_def = def as MyReactorDefinition;
                var solar_def = def as MySolarPanelDefinition;
                var wind_def = def as MyWindTurbineDefinition;
                var engine_def = def as MyHydrogenEngineDefinition;
                var generator_def = def as MyOxygenGeneratorDefinition;
                var cargo_def = def as MyCargoContainerDefinition;
                var tank_def = def as MyGasTankDefinition;
                var sorter_def = def as MyConveyorSorterDefinition;
                var connector_def = def as MyShipConnectorDefinition;
                var merge_def = def as MyMergeBlockDefinition;
                var susp_def = def as MyMotorSuspensionDefinition;
                var ball_def = def as MySpaceBallDefinition;
                var mech_def = def as MyMechanicalConnectionBlockBaseDefinition;
                var ConveyorSorter_def = def as MyConveyorSorterDefinition;
                var gyro_def = def as MyGyroDefinition;
                var ctc_def = def as MyTurretControlBlockDefinition;
                var pb_def = def as MyProgrammableBlockDefinition;
                var cam_def = def as MyCameraBlockDefinition;
                var drill_def = def as MyShipDrillDefinition;

                bool is_simple_logic = (def is MyTimerBlockDefinition || def is MyEventControllerBlockDefinition);
                bool is_XL_block = subtype_id.Contains("XL");
                bool is_wheel = subtype_id.Contains("heel") && !subtype_id.Contains("uspension");

                bool is_atmo = thrust_def != null && thrust_def.EffectivenessAtMinInfluence < 0.0001 && !subtype_id.Contains("Aquatic");
                bool is_hydro = thrust_def != null && thrust_def.EffectivenessAtMinInfluence > 0.4 && thrust_def.EffectivenessAtMinInfluence > 0.4 && thrust_def.FuelConverter?.FuelId.SubtypeName == "Hydrogen";
                bool is_ion = thrust_def != null && thrust_def.EffectivenessAtMaxInfluence < 0.4;

                bool is_control_surface = type_id == "MyObjectBuilder_TerminalBlock" && subtype_id.Contains("aero-wing_") && (subtype_id.Contains("pointed_edge") || subtype_id.Contains("rounded_edge"));

                bool is_ai_block = (def is MyBasicMissionBlockDefinition || def is MyFlightMovementBlockDefinition || def is MyDefensiveCombatBlockDefinition || def is MyOffensiveCombatBlockDefinition);
                bool is_logic_block = (def is MyProgrammableBlockDefinition || def is MyTimerBlockDefinition || def is MyEventControllerBlockDefinition);

                bool is_projectile = weapon_def != null && (type_id.Contains("Interior") || type_id.Contains("Gatling"));
                bool is_missile = weapon_def != null && type_id.Contains("Missile");
                bool is_railgun = weapon_def != null && type_id.Contains("Missile") && subtype_id.Contains("ailgun");

                var compList = new List<MyCubeBlockDefinition.Component>(def.Components);

                //var is_t1 = subtype_id.Contains("T1") || is_atmo

                ExchangeAndFix(ref compList, def);

                if(cam_def != null)
                {
                    InsertComponents(ref compList, 1, 1, "GlassParts");
                }

                if(is_XL_block)
                {
                    for(int i = 0; i < compList.Count; i++)
                    {
                        MyCubeBlockDefinition.Component comp = compList[i];
                        ExchangeComponents(ref compList, comp.Definition.Id.SubtypeName, "ConcreteParts");
                    }
                }
                
                #region thrust
                
                if(is_wheel)
                {
                    ExchangeComponents(ref compList, "StructuralParts", "WheelParts", 0.5f, null, true);
                }

                if (is_atmo || is_control_surface)
                {
                    ExchangeComponents(ref compList, "MechanicalParts", "AeroParts", 0.5f);
                }

                if(is_hydro)
                {
                    ExchangeComponents(ref compList, "ArmorParts", "AstroParts", 0.1f);
                }

                if (is_ion)
                {
                    //ExchangeComponents(ref compList, "Thrust", "GravityGenerator", 0.5f);
                }

                #endregion

                #region comm

                if (ctc_def != null)
                {
                    int radio_comps = (int)Math.Round(ctc_def.MaxRangeMeters / 250);
                    InsertComponents(ref compList, radio_comps, 1, "CommunicationParts");
                }

                if(weapon_def != null)
                {
                    if(is_projectile)
                    {
                        ExchangeComponents(ref compList, "ArmorParts", "StructuralParts");
                    }
                    if (is_missile)
                    {
                        ExchangeComponents(ref compList, "Computer", "ComputerParts", 0.5f);
                    }
                    if(is_railgun)
                    {
                        ExchangeComponents(ref compList, "Computer", "ComputerParts", 0.5f);
                        ExchangeComponents(ref compList, "ElectricalParts", "MagneticParts", 0.1f);
                    }
                }

                if (turret_def != null)
                {
                    int radio_comps = (int)Math.Round(turret_def.MaxRangeMeters / 250);
                    InsertComponents(ref compList, radio_comps, 1, "CommunicationParts");
                }

                if (is_ai_block)
                {
                    if(def is MyDefensiveCombatBlockDefinition || def is MyOffensiveCombatBlockDefinition)
                    {
                        InsertComponents(ref compList, 3, 1, "CommunicationParts");
                    }
                    InsertComponents(ref compList, 3, 1, "AutomationParts");
                }

                if (is_logic_block)
                {
                    InsertComponents(ref compList, 3, 1, "ComputerParts");
                }

                #endregion

                if (tank_def != null)
                {
                    tank_def.Capacity /= 75;
                }

                #region power

                if (battery_def != null)
                {
                    battery_def.MaxPowerOutput /= 10;
                    battery_def.MaxStoredPower /= 5;
                }

                if (engine_def != null)
                {
                    engine_def.MaxPowerOutput /= 1;
                    engine_def.FuelProductionToCapacityMultiplier *= 5;
                }

                if (reactor_def != null)
                {
                    reactor_def.MaxPowerOutput /= 3;
                }
				
				if(wind_def != null)
				{
					wind_def.MaxPowerOutput /= 1;
                    ExchangeComponents(ref compList, "MechanicalParts", "AeroParts");
				}

                if(solar_def != null)
                {
                    solar_def.MaxPowerOutput /= 1;
                }

                #endregion

                ExchangeAndFix(ref compList, def);

                def.Components = compList.ToArray();
                string criticalCompSubtype = def.Components[def.CriticalGroup].Definition.Id.SubtypeName;
                ReInit(def, def.GetObjectBuilder(), criticalCompSubtype, buildTime);

                //PrintCubeDefinitionDebug(cubeDef);
            }
        }

        /// <summary>
        /// Readjust thresholds for new integrity changes.
        /// </summary>
        /// <param name="cubeDef"></param>
        /// <param name="b"></param>
        /// <param name="criticalCompSubtype"></param>
		private void ReInit(MyCubeBlockDefinition def, MyObjectBuilder_DefinitionBase objBuilder, string criticalCompSubtype, float buildTime)
        {
            MyObjectBuilder_CubeBlockDefinition builder = objBuilder as MyObjectBuilder_CubeBlockDefinition;
            //MyLog.Default.Info($"[SEBR] integrityps:{def.IntegrityPointsPerSec}, build time:  {builder.BuildTimeSeconds}, calculated build time: {def.MaxIntegrity/def.IntegrityPointsPerSec}");
            def.DamageThreshold = builder.DamageThreshold;
            float sumMass = 0f;
            float sumIntegrityFunctional = 0f;
            float sumIntegrityHacked = 0f;
            def.MaxIntegrityRatio = 1f;
            if (def.Components != null && def.Components.Length != 0)
            {
                float sumIntegrity = 0f;
                int num5 = 0;
                int critical = int.MaxValue;
                for (int n = 0; n < def.Components.Length; n++)
                {
                    var component = def.Components[n];
                    if (component.Definition.Id.SubtypeId.String == "Computer" && sumIntegrityHacked == 0f)
                    {
                        sumIntegrityHacked = sumIntegrity + (float)component.Definition.MaxIntegrity;
                    }
                    sumIntegrity += (float)(component.Count * component.Definition.MaxIntegrity);
                    if (component.Definition.Id.SubtypeId.String == criticalCompSubtype)
                    {
                        if (num5 == builder.CriticalComponent.Index)
                        {
                            def.CriticalGroup = (ushort)n;
                            sumIntegrityFunctional = sumIntegrity - (float)component.Definition.MaxIntegrity;
                        }
                        num5++;
                        critical = n;
                    }

                    sumMass += (float)component.Count * component.Definition.Mass;
                }

                //for (int n = 0; n < def.Components.Length; n++)
                //{
                //    var component = def.Components[n];
                //    if (n <= critical)
                //    {
                //        //MyPhysicalItemDefinition Scrap;
                //        //oreDefs.TryGetValue("Scrap", out Scrap);

                //        component.DeconstructItem = component.Definition;
                //    }
                //}

                def.MaxIntegrity = sumIntegrity;
                // DECREASE BUILD TIME ACROSS BOARD
                //def.IntegrityPointsPerSec = def.MaxIntegrity / buildTime * 2f;
                def.IntegrityPointsPerSec = def.MaxIntegrity / buildTime;
                if (builder.MaxIntegrity != 0)
                {
                    def.MaxIntegrityRatio = (float)builder.MaxIntegrity / def.MaxIntegrity;
                    def.DeformationRatio /= def.MaxIntegrityRatio;
                }
            }
            def.CriticalIntegrityRatio = sumIntegrityFunctional / def.MaxIntegrity;
            def.OwnershipIntegrityRatio = sumIntegrityHacked / def.MaxIntegrity;
            if (builder.BuildProgressModels != null)
            {
                builder.BuildProgressModels.Sort((MyObjectBuilder_CubeBlockDefinition.BuildProgressModel a, MyObjectBuilder_CubeBlockDefinition.BuildProgressModel b) => a.BuildPercentUpperBound.CompareTo(b.BuildPercentUpperBound));
                def.BuildProgressModels = new MyCubeBlockDefinition.BuildProgressModel[builder.BuildProgressModels.Count];
                for (int num6 = 0; num6 < def.BuildProgressModels.Length; num6++)
                {
                    MyObjectBuilder_CubeBlockDefinition.BuildProgressModel buildProgressModel = builder.BuildProgressModels[num6];
                    if (!string.IsNullOrEmpty(buildProgressModel.File))
                    {
                        def.BuildProgressModels[num6] = new MyCubeBlockDefinition.BuildProgressModel
                        {
                            BuildRatioUpperBound = ((def.CriticalIntegrityRatio > 0f) ? (buildProgressModel.BuildPercentUpperBound * def.CriticalIntegrityRatio) : buildProgressModel.BuildPercentUpperBound),
                            File = buildProgressModel.File,
                            RandomOrientation = buildProgressModel.RandomOrientation
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Preserves total integrity when swapping two components.
        /// </summary>
        /// <param name="quantity"></param>
        /// <param name="fromComp"></param>
        /// <param name="toComp"></param>
        /// <returns></returns>
        private int GenerateComponentExchange(int quantity, MyComponentDefinition fromComp, MyComponentDefinition toComp)
        {
            double ratio = (double)fromComp.MaxIntegrity / (double)toComp.MaxIntegrity;
            return (int)(Math.Max((double)quantity * ratio, 1.0));
        }

        private void EnsureComponentsHave(ref List<MyCubeBlockDefinition.Component> components, int quantity, string toCompSubtype, int index = -1, string deconstructItemSubtype = null)
        {
            MyComponentDefinition toComp;
            MyPhysicalItemDefinition deconstructItem = null;

            MyDefinitionManager.Static.TryGetDefinition(
                MyDefinitionId.Parse("MyObjectBuilder_Component/" + toCompSubtype),
                out toComp);

            if (!string.IsNullOrEmpty(deconstructItemSubtype))
            {
                MyDefinitionManager.Static.TryGetDefinition(
                    MyDefinitionId.Parse("MyObjectBuilder_Component/" + deconstructItemSubtype),
                    out deconstructItem);
            }

            bool block_has_comps = components.Any(x => x.Definition == toComp);

            if(!block_has_comps)
            {
                InsertComponents(ref components, quantity, index, toCompSubtype, deconstructItemSubtype);
            }
            else
            {
                var currentIndex = FindComponentIndex(components, toCompSubtype);

                var stack = components[currentIndex];

                stack.Count = quantity;

                if(index != -1)
                    components.Swap(currentIndex, index);
            }
        }

        int FindComponentIndex(List<MyCubeBlockDefinition.Component> components, string toCompSubtype)
        {
            MyComponentDefinition toComp;

            MyDefinitionManager.Static.TryGetDefinition(
                MyDefinitionId.Parse("MyObjectBuilder_Component/" + toCompSubtype),
                out toComp);

            for (int i = 0; i < components.Count; i++)
            {
                MyCubeBlockDefinition.Component fromStack = components[i];
                if (fromStack.Definition == toComp)
                    return i;
            }

            return -1;
        }

        public void SafeRemoveIndex(ref List<MyCubeBlockDefinition.Component> compList, ref MyCubeBlockDefinition cubeDef, ref int i)
        {
            if(i != cubeDef.CriticalGroup)
            {
                compList[i - 1].Count += compList[i].Count;
                compList.RemoveAt(i);
                i--;
                if (i <= cubeDef.CriticalGroup && cubeDef.CriticalGroup > 0)
                    cubeDef.CriticalGroup--;
            }
        }

        bool IsValidPart(string subtypeName)
        {
            return validParts.Contains(subtypeName) || subtypeName.Contains("Prototech");
        }

        private void ExchangeAndFix(ref List<MyCubeBlockDefinition.Component> compList, MyCubeBlockDefinition cubeDef)
        {
            for (int i = 0; i < compList.Count; i++)
            {
                MyCubeBlockDefinition.Component comp = compList[i];
                if (compReplacements.Keys.Contains(comp.Definition.Id.SubtypeName))
                {
                    ExchangeComponents(ref compList, comp.Definition.Id.SubtypeName, compReplacements[comp.Definition.Id.SubtypeName]);
                }

                if (!IsValidPart(compList[i].Definition.Id.SubtypeName) && compList.Count > 1)
                {
                    //MyLog.Default.WriteLineAndConsole($"UGH DEBUG : {cubeDef.Id.SubtypeName} {compList[i].Definition.Id.SubtypeName}");
                    //SafeRemoveIndex(ref compList, ref cubeDef, ref i);
                }
            }

            for (int i = 0; i < compList.Count; i++)
            {
                // Fix duplicate component stacks.
                if (i > 0 && compList[i].Definition == compList[i - 1].Definition)
                {
                    SafeRemoveIndex(ref compList, ref cubeDef, ref i);
                }
            }
        }

        private void ExchangeComponents(ref List<MyCubeBlockDefinition.Component> components, string fromCompSubtype, string toCompSubtype, float ratio = 1f, string deconstructItemSubtype = null, bool justOnce = false)
        {

            MyComponentDefinition fromComp;
            MyComponentDefinition toComp;
            MyPhysicalItemDefinition deconstructItem = null;

            MyDefinitionManager.Static.TryGetDefinition(
                MyDefinitionId.Parse("MyObjectBuilder_Component/" + fromCompSubtype),
                out fromComp);

            MyDefinitionManager.Static.TryGetDefinition(
                MyDefinitionId.Parse("MyObjectBuilder_Component/" + toCompSubtype),
                out toComp);

            if (!string.IsNullOrEmpty(deconstructItemSubtype))
            {
                MyDefinitionManager.Static.TryGetDefinition(
                    MyDefinitionId.Parse("MyObjectBuilder_Component/" + deconstructItemSubtype),
                    out deconstructItem);
            }

            for (int i = 0; i < components.Count; i++)
            {
                MyCubeBlockDefinition.Component fromStack = components[i];
                if (fromStack.Definition == fromComp)
                {
                    var toStack = new MyCubeBlockDefinition.Component();
                    toStack.Definition = toComp;
                    if (!string.IsNullOrEmpty(deconstructItemSubtype))
                        toStack.DeconstructItem = deconstructItem;
                    else
                        toStack.DeconstructItem = toStack.Definition;

                    if (ratio == 1f)
                    {
                        int quantity = GenerateComponentExchange(fromStack.Count, fromComp, toComp);
                        toStack.Count = quantity;
                        components[i] = toStack;
                    }
                    else
                    {
                        int fromStackQuantity = (int)Math.Round(fromStack.Count * (1f - ratio));
                        int toStackQuantity = (int)Math.Round(fromStack.Count * ratio);

                        toStackQuantity = GenerateComponentExchange(toStackQuantity, fromComp, toComp);

                        fromStack.Count = fromStackQuantity;
                        toStack.Count = toStackQuantity;

                        components[i] = fromStack;
                        components.Insert(i, toStack);
                        i++;
                    }
                    if (justOnce)
                        return;
                }
            }
        }

        private void InsertComponents(ref List<MyCubeBlockDefinition.Component> components, int quantity, int index, string toCompSubtype, string deconstructItemSubtype = null)
        {
            MyComponentDefinition toComp;
            MyPhysicalItemDefinition deconstructItem = null;

            MyDefinitionManager.Static.TryGetDefinition(
                MyDefinitionId.Parse("MyObjectBuilder_Component/" + toCompSubtype),
                out toComp);

            if (!string.IsNullOrEmpty(deconstructItemSubtype))
            {
                MyDefinitionManager.Static.TryGetDefinition(
                    MyDefinitionId.Parse("MyObjectBuilder_Component/" + deconstructItemSubtype),
                    out deconstructItem);
            }

            var stack = new MyCubeBlockDefinition.Component();
            stack.Definition = toComp;
            stack.Count = quantity;
            if (!string.IsNullOrEmpty(deconstructItemSubtype))
                stack.DeconstructItem = deconstructItem;
            else
                stack.DeconstructItem = stack.Definition;

            components.Insert(index, stack);
        }

        private void PrintCubeDefinitionDebug(MyCubeBlockDefinition cubeDef)
        {
            MyLog.Default.WriteLineAndConsole($"DEBUGGING {cubeDef.Id.SubtypeId.String}.\n" +
                $"CriticalIntegrityRatio = {cubeDef.CriticalIntegrityRatio}\n" +
                $"MaxIntegrityRatio = {cubeDef.MaxIntegrityRatio}\n" +
                $"IntegrityPointsPerSec = {cubeDef.IntegrityPointsPerSec}\n" +
                $"OwnershipIntegrityRatio = {cubeDef.OwnershipIntegrityRatio}\n" +
                $"MaxIntegrity = {cubeDef.MaxIntegrity}"
            );
        }

        /// <summary>
        /// Runs prior to Init.
        /// </summary>
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            //UpdateResearchGroups();
            UpdateAllVoxels();
            UpdateAmmoBluePrints();
            UpdateBlueprints();
        }

        public override void LoadData()
        {
            validParts.AddRange(t1Parts);
            validParts.AddRange(t2Parts);
            validParts.AddRange(t3Parts);
            validParts.AddRange(t4Parts);

            UpdateAllDefinitions();

        }

        bool init = false;

        public override void UpdateBeforeSimulation()
        {
            if(!init)
            {
                //MyAPIGateway.Utilities.ShowNotification($"mm {theseExist}", 10000);
            }
        }
    }
}
