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


namespace launchers
{

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), true, "Propellantx4")]
    public class x4 : MyGameLogicComponent
    {
        bool init = false;
        bool shot = false;

        IMyThrust thrust;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
            thrust = Entity as IMyThrust;
        }

        public override void UpdateBeforeSimulation()
        {

            if (thrust == null || thrust.CubeGrid == null || thrust.CubeGrid.Physics == null || !thrust.IsFunctional)
                return;

            if (!init)
            {
                init = true;
                thrust.Enabled = false;
                return;
            }
            else if (thrust.Enabled == true)
            {
                shot = true;
            }

            if(shot)
                Launch(Entity);

        }

        public void Launch(IMyEntity Entity)
        {
            thrust.SlimBlock.DoDamage(thrust.SlimBlock.Integrity, MyStringHash.GetOrCompute("Bullet"), true);
            var force = thrust.WorldMatrix.Backward * 1e7;
            thrust.CubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, thrust.CubeGrid.Physics.CenterOfMassWorld, null);
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), true, "Propellantx5")]
    public class x5 : MyGameLogicComponent
    {
        bool shot = false;
        bool init = false;

        IMyThrust thrust;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
            thrust = Entity as IMyThrust;
        }

        public override void UpdateBeforeSimulation()
        {
            if (thrust == null || thrust.CubeGrid == null || thrust.CubeGrid.Physics == null || !thrust.IsFunctional)
                return;

            if (!init)
            {
                // wrow
                /*
                MyResourceSourceComponent sourceComp = new MyResourceSourceComponent();
                thrust.Components.Add(sourceComp);
                sourceComp.Init(MyStringHash.GetOrCompute("SolarPanels"), new MyResourceSourceInfo()
                {
                    DefinedOutput = 1f,
                    IsInfiniteCapacity = true,
                    ProductionToCapacityMultiplier = 1,
                    ResourceTypeId = MyResourceDistributorComponent.ElectricityId,
                });
                sourceComp.Enabled = true;
                */
                init = true;
                thrust.Enabled = false;
                return;
            }
            else if (thrust.Enabled == true)
            {
                shot = true; 
            }

            if(shot)
            {
                thrust.Enabled = true;
                if (thrust.ThrustOverridePercentage == 0)
                    thrust.ThrustOverridePercentage = 1f;

                thrust.SlimBlock.DoDamage(0.1f, MyStringHash.GetOrCompute("Bullet"), true);

                if (thrust.CurrentThrust == 0) //annoying but whatever
                {
                    var force = thrust.WorldMatrix.Backward * thrust.ThrustOverridePercentage * thrust.MaxEffectiveThrust;
                    thrust.CubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, thrust.CubeGrid.Physics.CenterOfMassWorld, null);
                }
            }
        }
    }
}