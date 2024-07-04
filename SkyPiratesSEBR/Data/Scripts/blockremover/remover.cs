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


namespace SEBR_GITGAT
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class SEBR_BLOCK_REDEFINER : MySessionComponentBase
    {

        public bool isInit = false;

        public HashSet<MyCubeBlockDefinition> cubeDefs = new HashSet<MyCubeBlockDefinition>();

        public Dictionary<string, MyComponentDefinition> compDefs = new Dictionary<string, MyComponentDefinition>();

        /// <summary>
        /// Replaces the Key with the Value.
        /// </summary>
        public Dictionary<string, string> compReplacements = new Dictionary<string, string>()
        {
            { "SteelPlate","Girder" },
            { "LargeTube","Girder"},
            { "SmallTube","Girder" },
            { "Construction","Girder" },
            { "InteriorPlate","Girder" },
            { "Display","Computer" },
            { "Reactor","PowerCell" },
            { "GravityGenerator","PowerCell" },
            { "RadioCommunication","PowerCell" },
            { "Detector","Computer" },
            { "Superconductor","PowerCell" },
            { "SolarCell","PowerCell" },
            { "Medical","PowerCell" },
        };

        /// <summary>
        /// Loop through all definition manager defs and manage 'em.
        /// </summary>
        private void UpdateAllDefinitions()
        {

            foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                MyCubeBlockDefinition cubeDef = def as MyCubeBlockDefinition;
                MyComponentDefinition compDef = def as MyComponentDefinition;

                if (cubeDef != null)
                    cubeDefs.Add(cubeDef);

                if (compDef != null && !compDefs.ContainsKey(compDef.Id.SubtypeName))
                    compDefs.Add(compDef.Id.SubtypeName, compDef);
            }

            UpdateAllCubes();
        }

        /// <summary>
        /// Loop through ever cube definition and spank it.
        /// </summary>
        private void UpdateAllCubes()
        {
            foreach(MyCubeBlockDefinition cubeDef in cubeDefs)
            {
                //PrintCubeDefinitionDebug(cubeDef);
                float buildTime = cubeDef.MaxIntegrity / cubeDef.IntegrityPointsPerSec;
                //MyLog.Default.Info($"[SEBR] original integrityps:  {cubeDef.IntegrityPointsPerSec}, original build time: {buildTime}");

                var compList = new List<MyCubeBlockDefinition.Component>(cubeDef.Components);

                for (int i = 0; i < compList.Count; i++)
                {
                    MyComponentDefinition fromComp = compList[i].Definition;
                    int quantity = compList[i].Count;

                    // Replace Components in our dictionary.
                    string toCompSubtype;
                    if(compReplacements.TryGetValue(fromComp.Id.SubtypeName, out toCompSubtype))
                    {
                        MyComponentDefinition toComp;
                        if (compDefs.TryGetValue(toCompSubtype, out toComp))
                        {
                            compList[i].Definition = toComp;
                            compList[i].DeconstructItem = toComp as MyPhysicalItemDefinition;
                            compList[i].Count = GenerateComponentExchange(quantity, fromComp, toComp);
                        }
                    }


                    // make cargo and connectors ungovernable
                    //if((cubeDef.Id.SubtypeId.String.Contains("Container") || cubeDef.Id.SubtypeId.String.Contains("Connector")) && compList[i].Definition.Id.SubtypeId.String == "Computer")
                    //{
                    //    //MyLog.Default.WriteLineAndConsole($"Removed computers from {cubeDef.Id.SubtypeId.String} at component index {i}.");
                    //    compList.RemoveAt(i);
                    //    i--;
                    //    if (i <= cubeDef.CriticalGroup && cubeDef.CriticalGroup > 0)
                    //        cubeDef.CriticalGroup--;
                    //    continue;
                    //}

                    // Fix duplicate component stacks.
                    if (i > 0 && compList[i].Definition == compList[i - 1].Definition && (i != (int)cubeDef.CriticalGroup))
                    {
                        compList[i - 1].Count += compList[i].Count;
                        compList.RemoveAt(i);
                        i--;
                        if (i <= cubeDef.CriticalGroup && cubeDef.CriticalGroup > 0)
                            cubeDef.CriticalGroup--;
                    }
                }

                // add armor to large light armor blocks
                //if (cubeDef.Id.SubtypeId.String.Contains("Armor") && cubeDef.CubeSize == MyCubeSize.Large)
                //{
                //    //MyLog.Default.WriteLineAndConsole($"Added armor to {cubeDef.Id.SubtypeId.String}.");
                //    MyComponentDefinition armorComp;
                //    compDefs.TryGetValue("MetalGrid", out armorComp);

                //    MyCubeBlockDefinition.Component component = new MyCubeBlockDefinition.Component
                //    {
                //        Definition = armorComp,
                //        Count = 10,
                //        DeconstructItem = armorComp
                //    };

                //    compList.Add(component);
                //}

                if (cubeDef.CubeSize == MyCubeSize.Small)
                {
                    cubeDef.GeneralDamageMultiplier *= 1.5f;
                }

                // add armor to large light armor blocks
                if (cubeDef.Id.SubtypeId.String.Contains("Decoy") && cubeDef.CubeSize == MyCubeSize.Large)
                {
                    //MyLog.Default.WriteLineAndConsole($"Added armor to {cubeDef.Id.SubtypeId.String}.");
                    MyComponentDefinition armorComp;
                    compDefs.TryGetValue("MetalGrid", out armorComp);

                    MyCubeBlockDefinition.Component component = new MyCubeBlockDefinition.Component
                    {
                        Definition = armorComp,
                        Count = 10,
                        DeconstructItem = armorComp
                    };

                    cubeDef.DamageMultiplierExplosion = 0.33f;

                    compList.Add(component);
                }

                // Remove target locking, improve damage resistance, add comps
                MyShipControllerDefinition cockdef = cubeDef as MyShipControllerDefinition;
                if (cockdef != null)
                {
                    cockdef.TargetLockingEnabled = false;
                    cubeDef.GeneralDamageMultiplier = 0.5f;

                    MyComponentDefinition Girder;
                    compDefs.TryGetValue("Girder", out Girder);

                    MyCubeBlockDefinition.Component component = new MyCubeBlockDefinition.Component
                    {
                        Definition = Girder,
                        Count = 10,
                        DeconstructItem = Girder
                    };

                    compList.Add(component);
                }

                cubeDef.Components = compList.ToArray();
                string criticalCompSubtype = cubeDef.Components[cubeDef.CriticalGroup].Definition.Id.SubtypeName;
                ReInit(cubeDef, cubeDef.GetObjectBuilder(), criticalCompSubtype, buildTime);

                //PrintCubeDefinitionDebug(cubeDef);
            }
        }

        /* Deprecated.
        private void HideBlackListedCubes()
        {
            foreach (MyCubeBlockDefinition cubeDef in cubeDefs)
            {
                foreach (string name in blacklist)
                {
                    if (cubeDef.DisplayNameText.Contains(name))
                    {
                        if(!cubeDef.DisplayNameText.Contains("Proprietary"))
                            cubeDef.DisplayNameString = "Proprietary " + cubeDef.DisplayNameText;

                        cubeDef.GuiVisible = false;
                        cubeDef.Public = false;
                    }
                }
            }
        }
        */

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
					}
                    sumMass += (float)component.Count * component.Definition.Mass;
				}
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
            return (int)(Math.Max((double)quantity * ratio,1.0));
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
        public override void LoadData()
        {
            UpdateAllDefinitions();
        }
    }
}
