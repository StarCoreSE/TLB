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
        bool shot = false;
        bool init = false;
        int tick = 0;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
            IMyThrust tb = Entity as IMyThrust;
            IMyFunctionalBlock funk = tb as IMyFunctionalBlock;
            funk.Enabled = false;
        }

        public override void UpdateBeforeSimulation()
        {
            IMyThrust tb = Entity as IMyThrust;
            IMyFunctionalBlock funk = tb as IMyFunctionalBlock;
            var grid = tb.CubeGrid;

            if (tb == null || grid == null || grid.Physics == null)
                return;

            if (!init)
            {
                init = true;
                funk.Enabled = false;
            }

            if (funk.Enabled == true)
            {
                shot = true;
            }

            if (!shot)
            {
                shot = false;
                funk.Enabled = false;
                return;
            }

            funk.Enabled = true;
            Launch(Entity);
        }

        public void Launch(IMyEntity Entity)
        {
            IMyThrust tb = Entity as IMyThrust;
            var grid = tb.CubeGrid;

            tb.SlimBlock.DoDamage(100f, MyStringHash.GetOrCompute("Bullet"), true);
            tb.SetValue<float>("Override", tb.MaxThrust);
            var thrustMatrix = tb.WorldMatrix;
            var force = thrustMatrix.Backward * tb.MaxThrust;
            grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force * 1000, grid.Physics.CenterOfMassWorld, null);
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), true, "Propellantx5")]
    public class x5 : MyGameLogicComponent
    {
        bool shot = false;
        bool init = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
            IMyThrust tb = Entity as IMyThrust;
            IMyFunctionalBlock funk = tb as IMyFunctionalBlock;
        }

        public override void UpdateBeforeSimulation()
        {
            IMyThrust tb = Entity as IMyThrust;
            IMyFunctionalBlock funk = tb as IMyFunctionalBlock;
            var grid = tb.CubeGrid;

            if (tb == null || grid == null || grid.Physics == null)
                return;

            if (!init)
            {
                init = true;
                funk.Enabled = false;
            }

            if (funk.Enabled == true)
            {
                shot = true;
            }

            if (!shot || !tb.IsFunctional)
            {
                shot = false;
                funk.Enabled = false;
                return;
            }

            funk.Enabled = true;
            tb.SlimBlock.DoDamage(0.1f, MyStringHash.GetOrCompute("Bullet"), true);
            tb.SetValue<float>("Override", tb.MaxThrust);
            var thrustMatrix = tb.WorldMatrix;
            var force = thrustMatrix.Forward * tb.MaxThrust;
            grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -force * 2, grid.Physics.CenterOfMassWorld, null); // apply the thruster's force at center of mass
        }
    }
}