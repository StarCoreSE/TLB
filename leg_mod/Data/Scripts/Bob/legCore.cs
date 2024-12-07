using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRage.ObjectBuilders;
using VRageMath;
using System.Linq;
using SpaceEngineers.Game.Entities.Blocks;


namespace MyMod
{

    public class FABRIKSolver
    {
        public static List<Vector3D> SolveFABRIK(
            List<Vector3D> joints,
            List<double> segmentLengths,
            Vector3D target,
            double tolerance = 0.01,
            int maxIterations = 10)
        {
            // Ensure the target is reachable
            double totalLength = 0;
            foreach (double length in segmentLengths)
                totalLength += length;

            if (Vector3D.Distance(joints[0], target) > totalLength)
            {
                // Target unreachable: extend toward target
                Vector3D direction = Vector3D.Normalize(target - joints[0]);
                for (int i = 1; i < joints.Count; i++)
                {
                    joints[i] = joints[i - 1] + direction * segmentLengths[i - 1];
                }
                return joints;
            }

            // FABRIK Iterative Process
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                Vector3D originalBase = joints[0]; // Save the initial base position

                // Forward Reaching: Start from the end-effector
                joints[joints.Count - 1] = target;
                for (int i = joints.Count - 2; i >= 0; i--)
                {
                    Vector3D direction = Vector3D.Normalize(joints[i] - joints[i + 1]);
                    joints[i] = joints[i + 1] + direction * segmentLengths[i];
                }

                // Backward Reaching: Start from the base
                joints[0] = originalBase; // Keep the base fixed
                for (int i = 1; i < joints.Count; i++)
                {
                    Vector3D direction = Vector3D.Normalize(joints[i] - joints[i - 1]);
                    joints[i] = joints[i - 1] + direction * segmentLengths[i - 1];
                }

                // Check for convergence
                if (Vector3D.Distance(joints[joints.Count - 1], target) < tolerance)
                    break;
            }

            return joints;
        }

    }

    public class Mecha
    {
        IMyShipController controller;
        IMyCubeGrid grid;

        bool isCrouched = false;
        bool isBraking = false;
        bool needToPlantFoot = true;

        public List<Leg> legs = new List<Leg>();

        double phase_leg_to_move = 0;
        const double PHASE_TOL = 0.1;

        public Mecha(IMyCubeGrid grid)
        {
            this.grid = grid;
        }

        public void Update()
        {
            if (grid == null)
                return;

            if (controller == null || controller.Closed || !controller.IsFunctional)
            {
                FindController(); // Recheck cockpit status
            }

            double movementForce = 0;

            needToPlantFoot = true;

            foreach (Leg leg in legs.ToList())
            {
                if (leg.suspension == null || leg.suspension.MarkedForClose)
                {
                    legs.Remove(leg);
                    continue;
                }

                // Handle phase offset and foot planting
                if (Math.Abs(leg.GetPhaseOffset() - phase_leg_to_move) < PHASE_TOL)
                {
                    if (leg.isRooted)
                    {
                        phase_leg_to_move += 0.01;
                    }

                    leg.Update(false, true); // Plant foot only if necessary
                }
                else
                {
                    leg.Update(false, false); // Move without planting
                }

                MyAPIGateway.Utilities.ShowNotification($"phase {phase_leg_to_move}, leg {leg.GetPhaseOffset()}", 16);

                if (leg.isRooted)
                    movementForce += leg.GetWalkForce();
            }

            // TODO: fix workaround
            if (grid.Physics != null && controller != null)
                ApplyMovementForce(-grid.Physics.Gravity, movementForce);

            if (phase_leg_to_move > 1)
                phase_leg_to_move = 0;
        }

        // TODO: Fix method
        private void FindController()
        {
            var cockpits = new List<IMySlimBlock>();
            grid.GetBlocks(cockpits, block => block.FatBlock is IMyShipController);

            if (cockpits.Count > 0)
            {
                controller = cockpits[0].FatBlock as IMyCockpit;
            }
        }

        public void ApplyMovementForce(Vector3D surfaceNormal, double legPower)
        {
            if (controller == null || controller.CubeGrid.Physics == null)
                return;

            surfaceNormal = Vector3D.Normalize(surfaceNormal);

            // Get the move indicator (normalized movement input)
            Vector3 moveIndicator = controller.MoveIndicator;

            if (controller.MoveIndicator.Y > 0)
            {
                isBraking = true;
                return;
            }
            else if (controller.MoveIndicator.Y < 0)
            {
                // toggle
                isCrouched = !isCrouched;
            }
            else
                isBraking = false;

            // Handle movement in the X and Z directions (strafe movement)
            Vector3D movementInput = Vector3D.TransformNormal(new Vector3D(moveIndicator.X, 0, moveIndicator.Z), controller.WorldMatrix);

            if (movementInput.LengthSquared() > 0)
            {
                // Create a vector perpendicular to the surface normal
                Vector3D adjustedMovement = movementInput - Vector3D.Dot(movementInput, surfaceNormal) * surfaceNormal;
                if (adjustedMovement.LengthSquared() > 0)
                {
                    adjustedMovement = Vector3D.Normalize(adjustedMovement);

                    // Calculate the force to apply
                    Vector3D forceToApply = adjustedMovement * legPower;

                    // Apply the force
                    controller.CubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceToApply, null, null);
                }
            }
        }

        public void Draw()
        {

        }

    }

    public class Segment
    {
        IMyCubeBlock segment;
        IMyCubeGrid segment_grid;

        public Segment()
        {

        }

        public void ConnectSegment()
        {

        }

        public void ApplyConnectingForces()
        {

        }
    }

    public static class Utilities
    {
        public static Vector3D GetPointAlongArc(Vector3D pointA, Vector3D pointB, Vector3D upVector, double arcHeight, double percentage)
        {
            // Clamp the percentage to ensure it's between 0 and 1
            percentage = MathHelper.Clamp(percentage, 0.0, 1.0);

            // Calculate the midpoint between pointA and pointB
            Vector3D midpoint = (pointA + pointB) / 2.0;

            // Offset the midpoint upwards by the arcHeight along the upVector
            Vector3D arcPeak = midpoint + Vector3D.Normalize(upVector) * arcHeight;

            // Use a quadratic Bézier curve formula to calculate the point along the arc
            // Quadratic Bézier curve: B(t) = (1-t)^2 * A + 2(1-t)t * Peak + t^2 * B
            double t = percentage;
            Vector3D pointOnArc =
                (1 - t) * (1 - t) * pointA +
                2 * (1 - t) * t * arcPeak +
                t * t * pointB;

            return pointOnArc;
        }

        public static Vector3D Lerp(Vector3D start, Vector3D end, double t)
        {
            t = MathHelper.Clamp(t, 0.0, 1.0); // Ensure 't' is clamped between 0 and 1
            return start + (end - start) * t;
        }

        // Function to draw a line between two vectors
        public static void DrawLineBetweenVectors(Vector3D start, Vector3D end, Color color)
        {
            // This is where you use MySimpleObjectDraw to visualize a line between start and end

            Vector4 v4 = color.ToVector4();

            // Draw a simple line in the 3D space between start and end using MySimpleObjectDraw
            MySimpleObjectDraw.DrawLine(start, end, MyStringId.GetOrCompute("Square"), ref v4, 0.1f);  // Red line, with a thickness of 0.1f

            // Optionally, you can add more objects to draw
            // MySimpleObjectDraw.DrawSphere(start, 0.2f, Color.Green); // Draw a sphere at the start point
            // MySimpleObjectDraw.DrawSphere(end, 0.2f, Color.Blue);   // Draw a sphere at the end point
        }

        public static bool BobRayCast(Vector3D start, Vector3D end, IMyCubeGrid ignoredGrid, out IHitInfo hit)
        {
            hit = null;

            // Perform the raycast and get all hits
            List<IHitInfo> hitResults = new List<IHitInfo>();
            MyAPIGateway.Physics.CastRay(start, end, hitResults);

            // Filter hits: Ignore hits on the same grid or grids logically connected to the current grid
            foreach (var hitInfo in hitResults)
            {
                // Skip if the hit is on the same grid or a grid logically connected to the current grid
                if (hitInfo.HitEntity is IMyCubeGrid)
                {
                    IMyCubeGrid hitGrid = hitInfo.HitEntity as IMyCubeGrid;
                    if (hitGrid == ignoredGrid || MyAPIGateway.GridGroups.HasConnection(hitGrid, ignoredGrid, GridLinkTypeEnum.Logical))
                    {
                        continue; // Skip hits on the same grid or connected grids
                    }
                }

                hit = hitInfo;
                return true;
            }

            return false;
        }
    }

    public class Leg
    {
        public IMyMotorSuspension suspension;
        public IMyCubeGrid grid;

        public bool isRagdolling;
        public bool isInStandingRange;
        public bool inContact;
        public bool isRooted;
        public bool isStepping;
        public bool isMovingToSurface;

        IHitInfo surface;

        Vector3D upDirection;
        Vector3D newFootLocation;
        Vector3D curFootLocation;
        Vector3D oldFootLocation;

        double heightAboveSurface = 0;

        public double height => 10 * suspension.Height;
        public double StepLength => height / 2;
        public double StepHeight => height / 3;

        double MaxSquatStrength = 1e6;
        double MaxWalkStrength = 5000;

        List<Segment> segments = new List<Segment>();

        List<Vector3D> joints = new List<Vector3D>
    {
        Vector3D.Zero,
        Vector3D.Zero,
        Vector3D.Zero,
    };

        List<double> segmentLengths = new List<double> { 7.5, 7.5 };

        public Leg(IMyMotorSuspension suspension, IMyCubeGrid grid)
        {
            this.suspension = suspension;
            this.grid = grid;
        }

        public void Update(bool forceMoveFoot, bool canMoveFoot)
        {
            if (suspension == null || grid == null || grid.Physics == null)
                return;

            if (suspension.IsAttached && suspension.Top != null)
            {
                suspension.Top.Close();
            }

            upDirection = Vector3D.Normalize(-grid.Physics.Gravity);

            UpdateState(forceMoveFoot, canMoveFoot);
            UpdateJoints();
            DebugDrawLeg();

            ApplyLegForces(1, Vector3D.Zero); // Assuming 4 legs
        }

        private void UpdateState(bool forceMoveFoot, bool canMoveFoot)
        {

            double dist = Vector3D.Distance(curFootLocation, -upDirection * height);

            if (!suspension.IsFunctional || !suspension.Enabled)
            {
                TransitionToRagdoll();
            }
            else if (!isStepping && !isMovingToSurface && (forceMoveFoot || (canMoveFoot && dist > height * 0.5) || dist > height))
            {
                MyAPIGateway.Utilities.ShowNotification($"is{!isStepping} && istm {!isMovingToSurface} && cmf {canMoveFoot} && dist {Vector3D.Distance(curFootLocation, -upDirection * height) > height * 0.5}", 32 );
                TransitionToStepping();
            }
            else if (isStepping)
            {
                CheckIfSteppingComplete();
            }
            else if (isMovingToSurface)
            {
                CheckIfReachedSurface();
            }
            else if (isRooted)
            {
                TransitionToRooted();
            }
        }

        private void TransitionToMovingToSurface()
        {
            isStepping = false;
            isMovingToSurface = true;

            DebugState("transition to moving to surf", Color.Gray);
        }

        private void TransitionToRagdoll()
        {
            isRagdolling = true;
            isRooted = false;
            isStepping = false;
            isMovingToSurface = false;
            isInStandingRange = false;

            newFootLocation = -upDirection * height;
            curFootLocation = (3 * curFootLocation + newFootLocation) / 4;

            DebugState("transition to ragdoll", Color.Gray);
        }

        private void TransitionToStanding()
        {
            isRagdolling = false;
            isInStandingRange = true;

            DebugState("transition to standing", Color.Gray);
        }

        private void TransitionToStepping()
        {
            isRooted = false;
            isMovingToSurface = false;
            isStepping = true;
            oldFootLocation = curFootLocation;

            DebugState("transition to stepping", Color.Yellow);
        }

        private void CheckIfSteppingComplete()
        {
            newFootLocation = (newFootLocation + Vector3D.Normalize(grid.Physics.LinearVelocity) * StepLength - upDirection * (height - StepHeight)) / 2;

            Utilities.DrawLineBetweenVectors(suspension.GetPosition(), newFootLocation + suspension.GetPosition(), Color.Blue);

            curFootLocation = (3 * curFootLocation + newFootLocation) / 4;

            MyAPIGateway.Utilities.ShowNotification($"dist {Vector3D.Distance(newFootLocation, curFootLocation)}, height {StepHeight * 2}", 16);

            if (Vector3D.Distance(newFootLocation, curFootLocation) < StepHeight * 2)
            {
                FindSurfaceLocation(Vector3D.Zero);
            }

            DebugState("transition to checking", Color.Yellow);
        }

        private void CheckIfReachedSurface()
        {
            curFootLocation = (9 * curFootLocation + newFootLocation) / 10;

            Utilities.DrawLineBetweenVectors(suspension.GetPosition(), newFootLocation + suspension.GetPosition(), Color.Blue);

            if (Vector3D.Distance(newFootLocation, curFootLocation) < StepHeight)
            {
                TransitionToRooted();      
            }

            DebugState("transition to moving to surface", Color.Cyan);
        }

        private void TransitionToRooted()
        {
            curFootLocation = surface.Position - suspension.GetPosition();
            isRooted = true;
            isMovingToSurface = false;

            DebugState("transition to rooted", Color.Green);
        }

        public void UpdateJoints()
        {
            joints[0] = suspension.GetPosition();
            FABRIKSolver.SolveFABRIK(joints, segmentLengths, curFootLocation + suspension.GetPosition());
        }

        public void DebugDrawLeg()
        {
            for (int i = 0; i < joints.Count - 1; i++)
            {
                Utilities.DrawLineBetweenVectors(joints[i], joints[i + 1], Color.Red);
            }
        }

        public void FindSurfaceLocation(Vector3D surfaceVelocity)
        {
            IHitInfo hit;
            Vector3D start = curFootLocation + upDirection * height + suspension.GetPosition();
            Vector3D end = start - upDirection * 4 * height;

            if (Utilities.BobRayCast(start, end, grid, out hit))
            {
                TransitionToMovingToSurface();
                newFootLocation = hit.Position;
                surface = hit;
                DebugState("fd srf", Color.Green);
            }

            Utilities.DrawLineBetweenVectors(start, end, Color.Black);
            DebugState($"checking for surf {hit == null}", Color.Green);
        }

        public void ApplyLegForces(int legs, Vector3D surfaceVelocity)
        {
            IHitInfo validHit;
            if (!Utilities.BobRayCast(suspension.GetPosition(), suspension.GetPosition() - upDirection * 2 * height, grid, out validHit))
            {
                TransitionToRagdoll();
                return;
            }

            Vector3D netForce = Vector3D.Zero;
            double distanceFromGround = Vector3D.Distance(validHit.Position, suspension.GetPosition());

            if (distanceFromGround < 1.5 * height && distanceFromGround > height)
            {
                TransitionToStanding();
            }
            else
            {
                netForce += upDirection * GetSquatForce(upDirection, legs, distanceFromGround);
                TransitionToStanding();
            }

            netForce += CalculateFrictionForceVector(validHit.Normal, Vector3D.Zero, grid.Physics.Mass / legs * grid.Physics.Gravity, 1.0, 1.0);
            grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, netForce, null, null);
        }

        /// <summary>
        /// Calculates the surface-relative velocity of the grid.
        /// </summary>
        /// <param name="surfaceNormal">The normal vector of the surface.</param>
        /// <param name="surfaceVelocity">The velocity of the surface.</param>
        /// <returns>A velocity vector relative to the surface.</returns>
        public Vector3D CalculateSurfaceVelocity(Vector3D surfaceNormal, Vector3D surfaceVelocity)
        {
            Vector3D relativeVelocity = grid.LinearVelocity - surfaceVelocity;
            return relativeVelocity - Vector3D.Dot(relativeVelocity, surfaceNormal) * surfaceNormal;
        }

        /// <summary>
        /// Calculates the maximum extension of the leg based on segment lengths and a fudge factor.
        /// </summary>
        /// <param name="fudge">A multiplier to slightly increase the maximum extension.</param>
        /// <returns>The maximum extension length of the leg.</returns>
        public double GetMaxExtension(double fudge)
        {
            double maxExtension = 0;
            foreach (var segmentLength in segmentLengths)
            {
                maxExtension += segmentLength * fudge;
            }
            return maxExtension;
        }

        /// <summary>
        /// Gets the phase offset of the leg, used for determining gait timing.
        /// </summary>
        /// <returns>A normalized value between 0 and 1 representing the phase offset.</returns>
        public double GetPhaseOffset()
        {
            return suspension.MaxSteerAngle / Math.PI;
        }

        /// <summary>
        /// Calculates the squat force applied by the leg based on height difference and surface normal.
        /// </summary>
        /// <param name="surfaceNormal">The normal vector of the surface.</param>
        /// <param name="legs">The total number of legs contributing to the force.</param>
        /// <param name="heightDifference">The height difference from the target position.</param>
        /// <returns>The magnitude of the squat force.</returns>
        public double GetSquatForce(Vector3D surfaceNormal, int legs, double distanceFromGround)
        {
            double speedTowardsSurface = -Vector3D.Dot(grid.Physics.LinearVelocity, Vector3D.Normalize(surfaceNormal));

            double squatForce = 0;

            if (distanceFromGround < 0.75 * height) // airshock region
            {
                if(speedTowardsSurface > 0) // falling
                    squatForce = MaxSquatStrength;
                else
                    squatForce = MaxSquatStrength * suspension.Strength / 100;
            }
            else
            {
                squatForce = 1.0 * (grid.Physics.Mass * grid.Physics.Gravity / legs).Length();
                if (speedTowardsSurface < 1)
                {
                    grid.Physics.LinearVelocity += Vector3D.Normalize(surfaceNormal) * (speedTowardsSurface - 1) * 0.5;
                }
            }

            //MyAPIGateway.Utilities.ShowNotification($"sf {(int)squatForce}", 16);
            //MyAPIGateway.Utilities.ShowNotification($"sts {speedTowardsSurface:0.00}, dfg {distanceFromGround:0.00}", 16);

            // Ensure the squat force doesn't exceed MaxSquatStrength
            return Math.Min(squatForce, MaxSquatStrength);
        }

        /// <summary>
        /// Gets the walking force based on suspension friction.
        /// </summary>
        /// <returns>The calculated walking force.</returns>
        public double GetWalkForce()
        {
            return MaxWalkStrength * suspension.Friction;
        }

        /// <summary>
        /// Calculates the normal force vector from a given surface normal and force vector.
        /// </summary>
        /// <param name="surfaceNormal">The normal vector of the surface.</param>
        /// <param name="force">The total force applied on the surface.</param>
        /// <returns>The normal force vector.</returns>
        public Vector3D CalculateNormalForceVector(Vector3D surfaceNormal, Vector3D force)
        {
            surfaceNormal = Vector3D.Normalize(surfaceNormal);
            double dotProduct = Vector3D.Dot(surfaceNormal, force);
            return Math.Abs(dotProduct) * surfaceNormal;
        }

        /// <summary>
        /// Calculates the friction force vector based on surface conditions and the force applied.
        /// </summary>
        /// <param name="surfaceNormal">The normal vector of the surface.</param>
        /// <param name="surfaceVelocity">The velocity of the surface relative to the leg.</param>
        /// <param name="normalForce">The normal force acting on the surface.</param>
        /// <param name="staticFrictionCoefficient">The coefficient of static friction.</param>
        /// <param name="dynamicFrictionCoefficient">The coefficient of dynamic friction.</param>
        /// <returns>The calculated friction force vector.</returns>
        public Vector3D CalculateFrictionForceVector(Vector3D surfaceNormal, Vector3D surfaceVelocity, Vector3D normalForce, double staticFrictionCoefficient, double dynamicFrictionCoefficient)
        {
            if (!isRooted)
                return Vector3D.Zero;

            Vector3D velocityPerpendicular = CalculateSurfaceVelocity(surfaceNormal, surfaceVelocity);

            if (velocityPerpendicular.LengthSquared() > 1)
            {
                return dynamicFrictionCoefficient * -Vector3D.Normalize(velocityPerpendicular) *
                    (normalForce.Length() + 100 * velocityPerpendicular.LengthSquared()) * suspension.Friction / 100;
            }
            else
            {
                return staticFrictionCoefficient * -Vector3D.Normalize(velocityPerpendicular) *
                    normalForce.Length() * suspension.Friction / 100;
            }
        }

        private void DebugState(string message, Color color)
        {
            MyAPIGateway.Utilities.ShowNotification(message, 16);
            //Utilities.DrawLineBetweenVectors(suspension.GetPosition(), curFootLocation + suspension.GetPosition(), color);
        }

        // Other helper methods remain unchanged (GetSquatForce, CalculateFrictionForceVector, etc.)
    }
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MotorSuspension), true)]
    public class LegSuspensionGameLogic : MyGameLogicComponent
    {
        IMyMotorSuspension suspension;
        IMyCubeGrid grid;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            suspension = Entity as IMyMotorSuspension;
            grid = suspension.CubeGrid;

            if (suspension == null || grid == null)
                return;

            Mecha mecha;
            Leg leg = new Leg(suspension, grid);

            if (LegSessionComponent.instance.mechas.TryGetValue(grid.EntityId, out mecha))
            {
                mecha.legs.Add(leg);
            }
            else
            {
                mecha = new Mecha(grid);
                mecha.legs.Add(leg);
                LegSessionComponent.instance.mechas.Add(grid.EntityId, mecha);
            }
        }
    }


    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)] // No continuous updates needed for this component
    public class LegSessionComponent : MySessionComponentBase
    {

        public static LegSessionComponent instance;

        public Dictionary<long, Mecha> mechas = new Dictionary<long, Mecha>();
        public override void LoadData()
        {
            base.LoadData();

            instance = this;

            // Hook into the CustomControlGetter event to dynamically add controls
            MyAPIGateway.TerminalControls.CustomControlGetter += FixTerminalControls;
        }

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            MyAPIGateway.Parallel.ForEach(mechas.Values.ToList(), mecha =>
            {
                mecha.Update();
            });
        }

        // TODO: Implement so players dont see fucky terminal actions for wheels
        private void FixTerminalActions(IMyTerminalBlock blocks, List<IMyTerminalAction> actions)
        {

        }

        private void FixTerminalControls(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (!block.BlockDefinition.SubtypeName.Contains("Suspension"))
            {
                // Only add controls for "Connector" blocks
                return;
            }

            List<IMyTerminalControl> controls_to_remove = new List<IMyTerminalControl>();

            // Create a "Detach" button for the connector
            foreach(var control in controls)
            {
                if (control is IMyTerminalControlButton)
                {
                    IMyTerminalControlButton button = control as IMyTerminalControlButton;
                    //MyAPIGateway.Utilities.ShowNotification(button.Title.ToString(), 10000);
                    if (button.Title.ToString() == "BlockActionTitle_AddWheel")
                    {
                        button.Title = MyStringId.GetOrCompute("Add Leg");
                        button.Tooltip = MyStringId.GetOrCompute("Try to add a leg if one is not present");
                    }
                }

                if(control is IMyTerminalControlSlider)
                {
                    IMyTerminalControlSlider slider = control as IMyTerminalControlSlider;
                    //MyAPIGateway.Utilities.ShowNotification(slider.Title.ToString(), 10000);

                    control.Enabled = (b) => true;

                    switch (slider.Title.ToString())
                    {
                        case ("BlockPropertyTitle_Motor_MaxSteerAngle"):
                            slider.Title = MyStringId.GetOrCompute("Phase Offset");
                            slider.Tooltip = MyStringId.GetOrCompute("Phase offset in gait");
                            break;

                        case ("BlockPropertyTitle_Motor_PropulsionOverride"):
                            slider.Title = MyStringId.GetOrCompute("Bounciness");
                            slider.Tooltip = MyStringId.GetOrCompute("Restitution on surface contact");
                            break;

                        case ("BlockPropertyTitle_Motor_SteerOverride"):
                            controls_to_remove.Add(control);
                            break;

                        case ("BlockPropertyTitle_SafetyDetach"):
                            controls_to_remove.Add(control);
                            break;

                        case ("BlockPropertyTitle_Motor_Height"):
                            slider.Title = MyStringId.GetOrCompute("Height");
                            slider.Tooltip = MyStringId.GetOrCompute("Total Height Off Ground");
                            break;
                    }
                }

                if(control is IMyTerminalControlCheckbox)
                {
                    IMyTerminalControlCheckbox checkbox = control as IMyTerminalControlCheckbox;
                    //MyAPIGateway.Utilities.ShowNotification(checkbox.Title.ToString(), 10000);

                    control.Enabled = (b) => true;

                    switch (checkbox.Title.ToString())
                    {
                        case ("BlockPropertyTitle_Motor_Propulsion"):
                            controls_to_remove.Add(control);
                            break;

                        case ("BlockPropertyTitle_Motor_Steering"):
                            controls_to_remove.Add(control);
                            break;

                        case ("BlockPropertyTitle_Motor_InvertSteer"):
                            controls_to_remove.Add(control);
                            break;

                        case ("BlockPropertyTitle_Motor_InvertPropulsion"):
                            controls_to_remove.Add(control);
                            break;
                    }
                }
            }
            foreach(var control in controls_to_remove)
            {
                controls.Remove(control);
            }
        }

        // Unsubscribe from the event to avoid memory leaks
        protected override void UnloadData()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= FixTerminalControls;
            mechas.Clear();
        }
    }
}