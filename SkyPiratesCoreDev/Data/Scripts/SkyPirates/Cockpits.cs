using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using System.Collections.Generic;
using VRage.Game.Entity;
using Sandbox.Definitions;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using VRage.Utils;

namespace SKY_PIRATES_CORE
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Cockpit), false)]
    public class CockpitControllers : MyGameLogicComponent
    {

        IMyCockpit cockpit;
        IMyShipController controller;
        IMyCubeGrid grid;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            this.NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
            grid = (Entity as IMyTerminalBlock).CubeGrid;
            cockpit = (Entity as IMyCockpit);
            controller = cockpit as IMyShipController;
        }

        public override void UpdateBeforeSimulation()
        {
            if (grid.Physics == null || controller.IsUnderControl == false || controller.CanControlShip == false)
                return;

            Vector3 velocity = grid.Physics.LinearVelocity;
            Vector3 forward = cockpit.WorldMatrix.Forward;
            Vector3 up = cockpit.WorldMatrix.Up;

            if (grid.GridSizeEnum == MyCubeSize.Small)
            {
                float mismatch = -Vector3.Dot(forward, Vector3.Normalize(velocity));

                if (mismatch > -0.96f && velocity.Length() < 70)
                {
                    Vector3D torque = grid.WorldAABB.Size.Length() * grid.Physics.Mass * Vector3D.Cross(velocity, -forward) * Math.Min(velocity.Length(), 70) * (0.49f + mismatch * mismatch / 10f) / 3000f;
                    grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, null, null, Vector3D.Transform(torque, MatrixD.Transpose(grid.WorldMatrix.GetOrientation())));
                }
            }
            /*
            else if(!grid.IsStatic) // this is zeppelin controller behaviour
            {
                Vector3 gravity = grid.Physics.Gravity;
                float bank = -Vector3.Dot(up, Vector3.Normalize(gravity));

                Vector3D torque = grid.WorldAABB.Size.Length() * grid.Physics.Mass * Vector3D.Cross(gravity, up) * (bank * bank);
                grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, null, null, Vector3D.Transform(torque, MatrixD.Transpose(grid.WorldMatrix.GetOrientation())));
            }
            */
        }
    }
}