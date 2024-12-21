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
using VRage.ModAPI;
using VRage;


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

            for (int i = 0; i < legs.Count; i++)
            {
                Leg leg = legs[i];

                // Check if the leg's suspension is invalid or marked for removal
                if (leg.suspension == null || leg.suspension.MarkedForClose)
                {
                    legs.RemoveAt(i); // Remove invalid leg
                    i--; // Adjust index to account for the removed element
                    continue;
                }

                // Handle phase offset and foot planting
                if (Math.Abs(leg.GetPhaseOffset() - phase_leg_to_move) < PHASE_TOL)
                {
                    if (leg.surface != null)
                    {
                        phase_leg_to_move = legs[(i + 1) % legs.Count].GetPhaseOffset();
                    }

                    leg.Update(false, true); // Plant foot only if necessary
                }
                else
                {
                    leg.Update(false, false); // Move without planting
                }

                // Display current phase and leg offset in a notification
                MyAPIGateway.Utilities.ShowNotification($"phase {phase_leg_to_move}, leg {leg.GetPhaseOffset()}", 16);

                // If the leg is on a surface, calculate and accumulate movement force
                if (leg.surface != null)
                {
                    movementForce += leg.GetWalkForce();
                }
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
        bool connected = false;

        const double spring_constant = 1000000;

        IMyCubeGrid segment_grid;
        IMyCubeGrid leg_grid;
        Vector3D start_joint;
        Vector3D end_joint;

        public Segment(string definition_string, Vector3D start_joint, Vector3D end_joint, Vector3D up, IMyCubeGrid leg_grid)
        {
            SpawnSegmentBlock(definition_string, start_joint, end_joint, up);

            this.start_joint = start_joint;
            this.end_joint = end_joint;
            this.leg_grid = leg_grid;
        }

        public void Update(Vector3D start_joint, Vector3D end_joint)
        {
            var center = (start_joint - this.start_joint) / 2;

            this.start_joint = start_joint;
            this.end_joint = end_joint;

            /*
            if (leg_grid as MyCubeGrid != null && segment_grid as MyCubeGrid != null && !connected)
            {
                try
                {
                    Utilities.ConnectGrids(leg_grid as MyCubeGrid, segment_grid as MyCubeGrid);
                    if (MyAPIGateway.GridGroups.HasConnection(leg_grid, segment_grid, GridLinkTypeEnum.Logical))
                        connected = true;
                }
                catch(Exception ex)
                {
                    connected = false;
                }
            }
            */

            ApplyConnectingForces();
        }

        private void SpawnSegmentBlock(string definition_string, Vector3D start_joint, Vector3D end_joint, Vector3D up)
        {
            // Get the definition for the block type of the main connector
            MyDefinitionId id;
            MyDefinitionId.TryParse(definition_string, out id);

            var blockDef = MyDefinitionManager.Static.GetCubeBlockDefinition(id);
            if (blockDef == null)
                return;

            // Get the position and orientation of the current connector
            var _pos = (start_joint + end_joint) / 2;
            var _fwd = Vector3D.Normalize(end_joint - start_joint);
            var _up = Vector3D.Cross(_fwd, up);

            MatrixD worldMatrix = MatrixD.CreateWorld(_pos, _fwd, _up);

            // Create the object builder for the new connector
            MyObjectBuilder_CubeBlock ob = (MyObjectBuilder_CubeBlock)MyObjectBuilderSerializer.CreateNewObject(blockDef.Id);
            ob.EntityId = 0;  // Allow the game to assign a new ID
            ob.Min = Vector3I.Zero;

            // Create a new grid object builder for the detached grid
            MyObjectBuilder_CubeGrid gridOB = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_CubeGrid>();
            gridOB.EntityId = 0;  // Allow the game to assign a new ID
            gridOB.DisplayName = "";
            gridOB.CreatePhysics = true;
            gridOB.GridSizeEnum = blockDef.CubeSize;
            gridOB.PositionAndOrientation = new MyPositionAndOrientation(_pos, worldMatrix.Forward, worldMatrix.Up);
            gridOB.PersistentFlags = MyPersistentEntityFlags2.InScene;
            gridOB.IsStatic = false;  // This is a dynamic grid for the detached connector
            gridOB.Editable = true;
            gridOB.DestructibleBlocks = true;  // Allow destruction of the grid
            gridOB.IsRespawnGrid = false;
            gridOB.CubeBlocks.Add(ob);

            IMyCubeGrid grid = null;

            // Spawn the new grid asynchronously
            /*
            MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridOB, true, gridEntity =>
            {
                var spawnedGrid = gridEntity as MyCubeGrid;
                MyAPIGateway.Utilities.ShowNotification($"grid : {spawnedGrid != null}", 10000);
                if (spawnedGrid == null)
                    return;

                // Set up the new grid (e.g., marking it as not a preview grid)
                spawnedGrid.IsPreview = false;
                spawnedGrid.Save = true;  // Allow saving the new grid

                segment_grid = spawnedGrid as IMyCubeGrid;
            });*/

            IMyEntity ent = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridOB);
            ent.Save = true;
            segment_grid = ent as IMyCubeGrid;
        }

        void MaintainOffsetVelocity(IMyCubeGrid targetGrid, IMyCubeGrid referenceGrid, Vector3D desiredOffset)
        {
            // Ensure both grids have physics
            var targetPhysics = targetGrid.Physics;
            var referencePhysics = referenceGrid.Physics;

            if (targetPhysics == null || referencePhysics == null)
                return;

            // Get current positions
            Vector3D targetPosition = targetGrid.WorldMatrix.Translation;
            Vector3D referencePosition = referenceGrid.WorldMatrix.Translation;

            // Calculate the current offset and the error
            Vector3D currentOffset = targetPosition - referencePosition;
            Vector3D offsetError = currentOffset - desiredOffset;

            // Calculate position correction velocity (Proportional Control)
            const double correctionFactor = 2.0; // Adjust for responsiveness
            Vector3D correctionVelocity = -offsetError * correctionFactor;

            // Get reference grid velocities
            Vector3D referenceLinearVelocity = referencePhysics.LinearVelocity;
            Vector3D referenceAngularVelocity = referencePhysics.AngularVelocity;

            // Apply velocity adjustments
            targetPhysics.LinearVelocity = referenceLinearVelocity + correctionVelocity;
            targetPhysics.AngularVelocity = referenceAngularVelocity;

            // Optional: Log for debugging
            MyAPIGateway.Utilities.ShowMessage("MaintainOffset", $"Offset Error: {offsetError}, Correction Velocity: {correctionVelocity}");
        }

        public void ApplyConnectingForces()
        {
            if (segment_grid?.Physics == null) // || !connected)
                return;
            /*
            Vector3D block_start = segment_grid.WorldMatrix.Translation + segment_grid.WorldMatrix.Forward;
            Vector3D block_end = segment_grid.WorldMatrix.Translation + segment_grid.WorldMatrix.Backward;

            segment_grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, spring_constant * -(block_start - start_joint), block_start, null);
            segment_grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, spring_constant * -(block_end - end_joint), block_end, null);

            if(segment_grid.Physics.Gravity != null)
            {
                segment_grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, (segment_grid as MyCubeGrid).GetCurrentMass() * -segment_grid.Physics.Gravity, null, null);
            }

            MaintainOffsetVelocity(segment_grid, leg_grid, (start_joint - this.start_joint) / 2 - leg_grid.WorldMatrix.Translation);
            */
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

        public static void ConnectGrids(MyCubeGrid a, MyCubeGrid b)
        {
            if (!a.IsInSameLogicalGroupAs(b))
            {
                MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Logical, a.EntityId, a, b);
                MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Physical, a.EntityId, a, b);
                MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Electrical, a.EntityId, a, b);

                MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Logical, b.EntityId, a, b);
                MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Physical, b.EntityId, a, b);
                MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Electrical, b.EntityId, a, b);
            }
                // MyLog.Default.Info($"[Tether] ConnectGrids: grids {a.EntityId} and {b.EntityId} are now connected");

        }

        public static void DisconnectGrids(MyCubeGrid a, MyCubeGrid b)
        {

            if (a.IsInSameLogicalGroupAs(b))
            {
                MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Logical, a.EntityId, a, b);
                MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Physical, a.EntityId, a, b);
                MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, a.EntityId, a, b);

                MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Logical, b.EntityId, a, b);
                MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Physical, b.EntityId, a, b);
                MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, b.EntityId, a, b);
            }
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
        private bool init = false;

        public IMyMotorSuspension suspension;
        public IMyCubeGrid grid;

        public bool isRagdolling;
        public bool isStepping;
        public bool isRooted;

        Vector3D footLocation;
        Vector3D targetLocation;

        Vector3D up = Vector3D.Zero;
        Vector3D direction = Vector3D.Zero;
        Vector3D relativeSurfaceVelocity = Vector3D.Zero;
        double suspension_height = double.MaxValue;

        public double height => 15 * suspension.Height;
        public double StepLength => height / 2;
        public double StepHeight => height / 5;

        const double MaxSquatStrength = 1e6;
        const double MaxWalkStrength = 5000;
        const double update = 0.01667;

        public Surface surface;

        List<Segment> segments = new List<Segment>();

        List<Vector3D> joints = new List<Vector3D>
        {
            Vector3D.Zero,
            Vector3D.Zero,
            Vector3D.Zero,
        };

        List<double> segmentLengths = new List<double> { 8, 8 };

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

            if (!init)
                init_leg();

            UpdateFootLocation(forceMoveFoot, canMoveFoot);
            UpdateJoints();
            DebugDrawLeg();
                
        }


        public void UpdateFootLocation(bool forceMoveFoot, bool canMoveFoot)
        {
            suspension_height = GetSuspensionHeight();
            targetLocation = GetTargetLocation();
            relativeSurfaceVelocity = GetRelativeSurfaceVelocity();

            if (suspension_height > 2 * height)
                return;

            ApplyVelocityLimit();

            if (surface == null)
            {
                if (!isOverextended(canMoveFoot))
                    GetSurface();
                footLocation += Vector3D.Normalize(targetLocation - footLocation + up * (Vector3D.Distance(targetLocation, footLocation) - StepHeight)) * update * StepLength * (1 + relativeSurfaceVelocity.Length() / 5);
                //DebugState($"dist {Vector3D.Distance(MyAPIGateway.Session.Camera.WorldMatrix.Translation, footLocation + suspension.GetPosition())}", Color.Black);
            }

            if (surface != null)
            {
                footLocation = surface.Position - suspension.GetPosition();
                ApplyLegForces(1);

            }

            if(isOverextended(canMoveFoot) || forceMoveFoot)
            {
                surface = null;
            }
        }

        public void init_leg()
        {
            up = GetUpDirection();

            footLocation = -up * height;

            joints[0] = suspension.GetPosition();
            FABRIKSolver.SolveFABRIK(joints, segmentLengths, footLocation + suspension.GetPosition());

            for (int i = 0; i < joints.Count - 1; i++)
            {
                segments.Add(new Segment("MyObjectBuilder_UpgradeModule/LargeProductivityModule", joints[i], joints[i+1], up, grid));
            }

            init = true;
        }

        public bool isOverextended(bool canMoveFoot)
        {
            Utilities.DrawLineBetweenVectors(footLocation + suspension.GetPosition(), targetLocation + suspension.GetPosition(), Color.Green);

            return (canMoveFoot && Vector3D.Distance(footLocation, targetLocation) > StepLength)  || footLocation.Length() > height;
        }

        public void UpdateJoints()
        {
            joints[0] = suspension.GetPosition();
            FABRIKSolver.SolveFABRIK(joints, segmentLengths, footLocation + suspension.GetPosition());

            for (int i = 0; i < joints.Count - 1; i++)
            {
                segments[i].Update(joints[i], joints[i + 1]);
            }
        }

        public void DebugDrawLeg()
        {
            for (int i = 0; i < joints.Count - 1; i++)
            {
                Utilities.DrawLineBetweenVectors(joints[i], joints[i + 1], Color.Red);
            }
        }

        public void ApplyLegForces(int legs)
        {
            if (grid.Physics == null)
                return;


            Vector3D netForce = Vector3D.Zero;

            if (suspension_height < 1.5 * height && suspension_height > height)
            {
                // TransitionToStanding();
            }
            else
            {
                netForce += up * GetSquatForce(up, legs, suspension_height);

                // TransitionToStanding();
            }

            netForce += CalculateFrictionForceVector(grid.Physics.Mass / legs * grid.Physics.Gravity, 1.0, 1.0);

            DebugState($"netforce {netForce.Length()}", Color.Black);

            grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, netForce, null, null);
        }

        public Vector3D GetTravelDirection()
        {
            if(relativeSurfaceVelocity.Length() > 5)
                return Vector3D.Normalize(relativeSurfaceVelocity);
            else
                return Vector3D.Zero;
        }

        public Vector3D GetRelativeSurfaceVelocity()
        {
            if(grid.Physics == null)
                return Vector3D.Zero;

            if (surface == null)
                return grid.LinearVelocity;

            Vector3D relativeVelocity = grid.LinearVelocity - surface.Velocity;
            return relativeVelocity - Vector3D.Dot(relativeVelocity, surface.Normal) * surface.Normal;
        }

        public double GetSuspensionHeight()
        {
            IHitInfo validHit;
            if (!Utilities.BobRayCast(suspension.GetPosition(), suspension.GetPosition() - up * 2 * height, grid, out validHit))
            {
                return double.MaxValue;
            }

            return Vector3D.Distance(validHit.Position, suspension.GetPosition());
        }

        public Vector3D GetTargetLocation()
        {
            IHitInfo hit;
            Vector3D start = GetTravelDirection() * StepLength + suspension.GetPosition();
            Vector3D end = start - up * 2 * height;

            Utilities.DrawLineBetweenVectors(start, end, Color.Blue);

            if (Utilities.BobRayCast(start, end, grid, out hit))
            {
                return hit.Position - suspension.GetPosition();
            }

            return start - up * height - suspension.GetPosition();
        }

        public Surface GetSurface()
        {
            IHitInfo hit;
            Vector3D start = footLocation + 0.1 * up + suspension.GetPosition();
            Vector3D end = suspension.GetPosition();

            Utilities.DrawLineBetweenVectors(start, end, Color.Black);

            if (Utilities.BobRayCast(start, end, grid, out hit))
            {
                surface = new Surface(hit);

                return surface;
            }

            DebugState($"checking for surf {hit == null}", Color.Green);

            return null;
        }

        public Vector3D GetUpDirection()
        {
            if (grid.Physics?.Gravity != null)
                return -Vector3D.Normalize(grid.Physics.Gravity);
            else if (surface != null)
                return surface.Normal;
            else
                return suspension.WorldMatrix.Up;
        }

        public double GetMaxExtension(double fudge)
        {
            double maxExtension = 0;
            foreach (var segmentLength in segmentLengths)
            {
                maxExtension += segmentLength * fudge;
            }
            return maxExtension;
        }

        public double GetPhaseOffset()
        {
            return suspension.MaxSteerAngle / Math.PI;
        }

        public void ApplyVelocityLimit()
        {
            if (grid.Physics == null)
                return;

            double speedTowardsSurface = -Vector3D.Dot(grid.Physics.LinearVelocity - relativeSurfaceVelocity, up);

            if (suspension_height >= 0.75 * height) // airshock region
            {
                if (speedTowardsSurface < 1)
                {
                    grid.Physics.LinearVelocity += up * (speedTowardsSurface - 1) * 0.5;
                }
            }
        }

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
            }

            //MyAPIGateway.Utilities.ShowNotification($"sf {(int)squatForce}", 16);
            //MyAPIGateway.Utilities.ShowNotification($"sts {speedTowardsSurface:0.00}, dfg {distanceFromGround:0.00}", 16);

            // Ensure the squat force doesn't exceed MaxSquatStrength
            return Math.Min(squatForce, MaxSquatStrength);
        }

        public double GetWalkForce()
        {
            return MaxWalkStrength * suspension.Friction;
        }

        public Vector3D CalculateNormalForceVector(Vector3D surfaceNormal, Vector3D force)
        {
            surfaceNormal = Vector3D.Normalize(surfaceNormal);
            double dotProduct = Vector3D.Dot(surfaceNormal, force);
            return Math.Abs(dotProduct) * surfaceNormal;
        }

        public Vector3D CalculateFrictionForceVector(Vector3D normalForce, double staticFrictionCoefficient, double dynamicFrictionCoefficient)
        {
            if (surface == null)
                return Vector3D.Zero;

            if (relativeSurfaceVelocity.LengthSquared() > 1)
            {
                return dynamicFrictionCoefficient * -Vector3D.Normalize(relativeSurfaceVelocity) *
                    (normalForce.Length() + 100 * relativeSurfaceVelocity.LengthSquared()) * suspension.Friction / 100;
            }
            else
            {
                return staticFrictionCoefficient * -Vector3D.Normalize(relativeSurfaceVelocity) *
                    normalForce.Length() * suspension.Friction / 100;
            }
        }

        private void DebugState(string message, Color color)
        {
            MyAPIGateway.Utilities.ShowNotification(message, 16);
        }

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

    public class Surface
    {
        public IMyEntity Entity { get; private set; }
        private Vector3D initialNormal;
        private Vector3D initialPosition;
        public float Friction { get; private set; }

        public Surface(IHitInfo hit, float fric = 1f)
        {
            initialNormal = hit.Normal;
            Entity = hit.HitEntity;
            initialPosition = hit.Position;
            Friction = fric;
        }

        // Getter for position in world space
        public Vector3D Position
        {
            get
            {
                if (Entity != null)
                {
                    // Update position based on the entity's current transformation
                    return Vector3D.Transform(initialPosition - Entity.GetPosition(), Entity.WorldMatrix);
                }
                return initialPosition;
            }
        }

        // Getter for velocity in world space
        public Vector3D Velocity
        {
            get
            {
                if (Entity != null && Entity.Physics != null)
                {
                    // Include both linear and rotational contributions
                    var angularVelocity = Entity.Physics.AngularVelocity;
                    var relativePosition = Position - Entity.GetPosition();
                    var rotationalVelocity = Vector3D.Cross(angularVelocity, relativePosition);

                    return Entity.Physics.LinearVelocity + rotationalVelocity;
                }
                return Vector3D.Zero;
            }
        }

        // Getter for normal relative to the entity's orientation
        public Vector3D Normal
        {
            get
            {
                if (Entity != null)
                {
                    // Transform the initial normal vector into the entity's local space
                    return Vector3D.TransformNormal(initialNormal, MatrixD.Transpose(Entity.WorldMatrix));
                }
                return initialNormal;
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