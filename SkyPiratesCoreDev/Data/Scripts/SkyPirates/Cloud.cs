using System;
using System.Collections.Generic;

using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;

using VRage.Input;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace SKY_PIRATES_CORE
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenTank), false, "ZeppelinCloud")]
    public class Cloud : MyGameLogicComponent
    {
        IMyGasTank cloud;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
            cloud = Entity as IMyGasTank;
        }

        public override void UpdateBeforeSimulation()
        {
            if (cloud == null)
                return;

            IMyCubeGrid grid = (cloud as IMyCubeBlock).CubeGrid;
            Vector3 gravity = grid.Physics.Gravity;
            Vector3 up = cloud.WorldMatrix.Up;
            float bank = -Vector3.Dot(up, Vector3.Normalize(gravity));
            Vector3D torque = grid.WorldAABB.Size.Length() * 100f * Vector3D.Cross(gravity, up) * bank * bank;
            grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, null, null, Vector3D.Transform(torque, MatrixD.Transpose(grid.WorldMatrix.GetOrientation())));
            grid.Physics.LinearVelocity = Vector3D.Normalize(grid.Physics.LinearVelocity) * 30;
        }
    }
}