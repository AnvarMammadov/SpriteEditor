using System;
using System.Collections.Generic;
using SkiaSharp;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Services.Rigging
{
    /// <summary>
    /// Inverse Kinematics (IK) solver service for intuitive character posing.
    /// Uses CCD (Cyclic Coordinate Descent) algorithm for general chains.
    /// Includes 2-bone analytical solver for arms/legs (faster and more stable).
    /// </summary>
    public class KinematicService
    {
        /// <summary>
        /// Solve IK chain using CCD (Cyclic Coordinate Descent).
        /// Works from end effector to root, rotating each joint to point toward target.
        /// </summary>
        /// <param name="chain">List of joints from END to ROOT (reversed order)</param>
        /// <param name="targetPos">Desired position for end effector</param>
        /// <param name="maxIterations">Maximum CCD iterations</param>
        /// <param name="tolerance">Stop when end effector is within this distance</param>
        public void SolveCCD(List<JointModel> chain, SKPoint targetPos, int maxIterations = 10, float tolerance = 1f)
        {
            if (chain == null || chain.Count < 2)
                return;

            var endEffector = chain[0]; // First in list is the end (e.g., hand)

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                // Check if we're close enough
                float distToTarget = Distance(endEffector.Position, targetPos);
                if (distToTarget < tolerance)
                    break;

                // Iterate through chain from end to root (skip end effector itself)
                for (int i = 1; i < chain.Count; i++)
                {
                    var joint = chain[i];
                    
                    // Skip root rotation (Root should stay fixed)
                    if (joint.Parent == null)
                    {
                        // But allow root translation if needed for extreme reaches
                        // (optional - currently disabled)
                        continue;
                    }

                    // Vector from current joint to end effector
                    SKPoint toEnd = new SKPoint(
                        endEffector.Position.X - joint.Position.X,
                        endEffector.Position.Y - joint.Position.Y
                    );

                    // Vector from current joint to target
                    SKPoint toTarget = new SKPoint(
                        targetPos.X - joint.Position.X,
                        targetPos.Y - joint.Position.Y
                    );

                    // Calculate rotation needed
                    float angleToEnd = MathF.Atan2(toEnd.Y, toEnd.X);
                    float angleToTarget = MathF.Atan2(toTarget.Y, toTarget.X);
                    float deltaAngle = angleToTarget - angleToEnd;

                    // Normalize angle to [-PI, PI]
                    deltaAngle = NormalizeAngle(deltaAngle);

                    // Small rotation only (stability)
                    deltaAngle = Math.Clamp(deltaAngle, -0.5f, 0.5f);

                    // Update joint rotation
                    if (!float.IsNaN(deltaAngle) && !float.IsInfinity(deltaAngle))
                    {
                        joint.Rotation += deltaAngle;
                    }

                    // Apply angle constraints BEFORE propagating
                    ApplyAngleConstraints(joint);

                    // CRITICAL FIX: Update THIS joint's position based on its parent and new rotation
                    if (joint.Parent != null)
                    {
                        // USER FIX: Disable dynamic bone length calculation to prevent stretching
                        // Only use pre-defined BoneLength
                        /*
                        if (joint.BoneLength < 0.1f)
                        {
                            float dx = joint.Position.X - joint.Parent.Position.X;
                            float dy = joint.Position.Y - joint.Parent.Position.Y;
                            joint.BoneLength = MathF.Sqrt(dx * dx + dy * dy);
                        }
                        */

                        // Check for Chain Root locking
                        // Since we use GetIKChain() which excludes the fixed base (e.g. Shoulder),
                        // the chain root is the moving joint (e.g. Elbow). We MUST update its position.
                        // So we do NOT skip position update for the chain root.
                        // bool isChainRoot = (i == chain.Count - 1);

                        // Update Position strictly based on Parent + Rotation + Length
                        // This prevents bone stretching and drift
                        if (joint.BoneLength >= 0.001f)
                        {
                            joint.Position = new SKPoint(
                                joint.Parent.Position.X + joint.BoneLength * MathF.Cos(joint.Rotation),
                                joint.Parent.Position.Y + joint.BoneLength * MathF.Sin(joint.Rotation)
                            );
                        }
                    }

                    // CRITICAL FIX: After rotating 'joint', update positions of all joints
                    // FORWARD in the chain (toward end effector), not just children in hierarchy!
                    // This prevents skeleton breakage when rotating proximal joints.
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var childJoint = chain[j];
                        if (childJoint.Parent != null && childJoint.BoneLength >= 0.001f)
                        {
                            // Update position based on parent's new rotation
                            childJoint.Position = new SKPoint(
                                childJoint.Parent.Position.X + childJoint.BoneLength * MathF.Cos(childJoint.Rotation),
                                childJoint.Parent.Position.Y + childJoint.BoneLength * MathF.Sin(childJoint.Rotation)
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Analytical 2-bone IK solver with proper bend direction (FIXED).
        /// User fix: Added bendPositive parameter to prevent reverse knee bending.
        /// </summary>
        public void Solve2BoneIK(JointModel root, JointModel mid, JointModel end, SKPoint targetPos, bool bendPositive = true)
        {
            if (root == null || mid == null || end == null) return;

            // 1. Bone lengths
            float a = Distance(root.Position, mid.Position); // Thigh/Upper arm
            float b = Distance(mid.Position, end.Position);  // Shin/Forearm
            
            // 2. Distance and vector from root to target
            float dx = targetPos.X - root.Position.X;
            float dy = targetPos.Y - root.Position.Y;
            float c = MathF.Sqrt(dx * dx + dy * dy);

            // 3. Maximum reach limit (prevent singularity)
            float maxReach = (a + b) * 0.999f; 
            if (c > maxReach) 
            {
                c = maxReach;
                // Scale target to max distance
                float scale = maxReach / MathF.Sqrt(dx*dx + dy*dy);
                dx *= scale;
                dy *= scale;
            }
            
            // Minimum distance (prevent bones overlapping)
            if (c < 0.1f) c = 0.1f;

            // 4. Law of cosines to find angles
            // Alpha: angle at root (thigh)
            // Beta: angle at mid (knee)
            
            float alpha = MathF.Acos(Math.Clamp((a * a + c * c - b * b) / (2 * a * c), -1f, 1f));
            float beta = MathF.Acos(Math.Clamp((a * a + b * b - c * c) / (2 * a * b), -1f, 1f));

            // Target vector's global angle
            float targetAngle = MathF.Atan2(dy, dx);

            // 5. CRITICAL: Bend Direction
            // bendPositive = true: knee bends forward (right leg)
            // bendPositive = false: knee bends forward (left leg)
            
            float rootAngle = bendPositive ? (targetAngle - alpha) : (targetAngle + alpha);
            float midAngle = bendPositive ? (MathF.PI - beta) : (beta - MathF.PI);

            // 6. Apply rotations  
            root.Rotation = NormalizeAngle(rootAngle);
            mid.Rotation = NormalizeAngle(root.Rotation + midAngle);

            // 7. Update positions (Forward Kinematics)
            mid.Position = new SKPoint(
                root.Position.X + a * MathF.Cos(root.Rotation),
                root.Position.Y + a * MathF.Sin(root.Rotation)
            );
            
            end.Position = new SKPoint(
                mid.Position.X + b * MathF.Cos(mid.Rotation),
                mid.Position.Y + b * MathF.Sin(mid.Rotation)
            );

            // NOTE: ApplyAngleConstraints removed from here!
            // User fix: Constraints after IK cause jitter. Apply to target instead.
        }

        /// <summary>
        /// Update all child joint positions AND rotations based on parent's rotation (Forward Kinematics).
        /// CRITICAL: This must maintain the relative angle between parent and child (preserve bone angles).
        /// </summary>
        private void UpdateChildPositions(JointModel parent)
        {
            foreach (var child in parent.Children)
            {
                if (child.BoneLength < 0.001f)
                {
                    // Calculate bone length if not set
                    float dx = child.Position.X - parent.Position.X;
                    float dy = child.Position.Y - parent.Position.Y;
                    child.BoneLength = MathF.Sqrt(dx * dx + dy * dy);
                    
                    if (child.BoneLength < 0.001f)
                        continue; // Skip invalid bones
                }
                
                // CRITICAL: Use child's EXISTING rotation (set by IK or parent)
                // DO NOT recalculate from position - that creates circular dependency!
                // The child's rotation was already set correctly by the IK solver.
                child.Position = new SKPoint(
                    parent.Position.X + child.BoneLength * MathF.Cos(child.Rotation),
                    parent.Position.Y + child.BoneLength * MathF.Sin(child.Rotation)
                );

                // Recursively update grandchildren
                UpdateChildPositions(child);
            }
        }

        /// <summary>
        /// Apply angle constraints to prevent unrealistic poses.
        /// Clamps joint rotation between MinAngle and MaxAngle relative to bind pose.
        /// </summary>
        private void ApplyAngleConstraints(JointModel joint)
        {
            if (joint == null || joint.Parent == null)
                return;

            // Calculate current angle relative to bind pose
            float currentAngle = joint.Rotation - joint.BindRotation;
            currentAngle = NormalizeAngle(currentAngle);

            // Convert degrees to radians
            float minRad = joint.MinAngle * MathF.PI / 180f;
            float maxRad = joint.MaxAngle * MathF.PI / 180f;

            // Clamp
            float clampedAngle = Math.Clamp(currentAngle, minRad, maxRad);

            // Apply clamped rotation
            joint.Rotation = joint.BindRotation + clampedAngle;
        }

        /// <summary>
        /// Root pulling: move the root joint toward target if end effector can't reach.
        /// Creates natural "reaching" motion.
        /// </summary>
        public void ApplyRootPulling(JointModel root, SKPoint targetPos, float pullStrength = 0.3f)
        {
            if (root == null) return;

            // Vector from root to target
            float dx = targetPos.X - root.Position.X;
            float dy = targetPos.Y - root.Position.Y;

            // Pull root slightly toward target
            root.Position = new SKPoint(
                root.Position.X + dx * pullStrength,
                root.Position.Y + dy * pullStrength
            );
        }

        #region Helper Methods

        private float Distance(SKPoint a, SKPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private SKPoint Normalize(SKPoint vec)
        {
            float length = MathF.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
            if (length < 0.001f) return new SKPoint(1, 0); // Avoid division by zero
            return new SKPoint(vec.X / length, vec.Y / length);
        }

        private float NormalizeAngle(float angle)
        {
            while (angle > MathF.PI) angle -= 2f * MathF.PI;
            while (angle < -MathF.PI) angle += 2f * MathF.PI;
            return angle;
        }

        #endregion
    }
}
