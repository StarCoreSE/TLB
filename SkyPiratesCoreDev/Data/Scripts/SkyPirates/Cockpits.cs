using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRageMath;
using System;

namespace SKY_PIRATES_CORE
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Cockpit), false)]
    public class CockpitControllers : MyGameLogicComponent
    {

        private const double MIN_STALL_SPEED = 70;

        IMyCockpit cockpit;
        IMyShipController controller;
        IMyCubeGrid grid;

        Vector3D aabbSize;
        double aabbSizeLength;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            this.NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
            grid = (Entity as IMyTerminalBlock).CubeGrid;
            cockpit = (Entity as IMyCockpit);
            controller = cockpit as IMyShipController;
        }

        public override void UpdateBeforeSimulation()
        {
            if (grid.Physics == null || grid.GridSizeEnum == MyCubeSize.Large || controller.IsUnderControl == false || controller.CanControlShip == false)
                return;

            ApplyStallTorque();
        }

        /// <summary>
        /// Method to stall small grid fighter planes. Could use improvement.
        /// </summary>
        private void ApplyStallTorque()
        {
            Vector3 velocity = grid.Physics.LinearVelocity;
            Vector3 forward = cockpit.WorldMatrix.Forward;
            Vector3 up = cockpit.WorldMatrix.Up;

            float mismatch = -Vector3.Dot(forward, Vector3.Normalize(velocity));

            if (mismatch > -0.96f && velocity.Length() < MIN_STALL_SPEED)
            {
                Vector3D torque = grid.WorldAABB.Size.Length() * grid.Physics.Mass * Vector3D.Cross(velocity, -forward) * Math.Min(velocity.Length(), MIN_STALL_SPEED) * (0.49f + mismatch * mismatch / 10f) / 3000f;
                grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, null, null, Vector3D.Transform(torque, MatrixD.Transpose(grid.WorldMatrix.GetOrientation())));
            }
        }
    }
}