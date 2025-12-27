using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Services.Rigging
{
    /// <summary>
    /// Physics engine for ragdoll simulation using Verlet integration.
    /// Provides realistic gravity, momentum, and constraint-based bone behavior.
    /// </summary>
    public class PhysicsService
    {
        // Physics parameters
        public float Gravity { get; set; } = 500f;  // pixels/sec^2 (downward)
        public float Damping { get; set; } = 0.98f;  // Air resistance (0-1)
        public int ConstraintIterations { get; set; } = 12;  // IMPROVED: More iterations for stability
        public float StiffnessMultiplier { get; set; } = 1.0f;  // NEW: Global stiffness control

        private Dictionary<int, JointModel> _joints;
        private List<DistanceConstraint> _constraints;
        private JointModel _draggedJoint;
        private SKPoint _dragTarget;
        private float _dragStrength = 1.2f;  // IMPROVED: Stronger drag for better control
        private float _dragRadius = 80f;  // NEW: Radius of influence for drag force

        public PhysicsService()
        {
            _joints = new Dictionary<int, JointModel>();
            _constraints = new List<DistanceConstraint>();
        }

        /// <summary>
        /// Initialize physics simulation with current skeleton.
        /// </summary>
        public void Initialize(IEnumerable<JointModel> joints)
        {
            _joints.Clear();
            _constraints.Clear();

            foreach (var joint in joints)
            {
                _joints[joint.Id] = joint;
                
                // Initialize Verlet integration (PreviousPosition = CurrentPosition)
                joint.PreviousPosition = joint.Position;

                // Create distance constraint with parent
                if (joint.Parent != null && joint.BoneLength > 0)
                {
                    _constraints.Add(new DistanceConstraint
                    {
                        JointA = joint.Parent,
                        JointB = joint,
                        RestLength = joint.BoneLength
                    });
                }
            }
        }

        /// <summary>
        /// Main physics update loop (call at 60 FPS).
        /// </summary>
        public void VerletStep(float deltaTime)
        {
            if (_joints.Count == 0) return;

            // Step 1: Apply forces (gravity)
            foreach (var joint in _joints.Values)
            {
                if (joint.IsAnchored) continue;

                // Calculate velocity (Verlet: velocity = current - previous)
                SKPoint velocity = new SKPoint(
                    joint.Position.X - joint.PreviousPosition.X,
                    joint.Position.Y - joint.PreviousPosition.Y
                );

                // Apply damping (air resistance)
                velocity = new SKPoint(
                    velocity.X * Damping,
                    velocity.Y * Damping
                );

                // Apply gravity
                float gravityForce = Gravity * deltaTime * deltaTime / joint.Mass;
                velocity = new SKPoint(
                    velocity.X,
                    velocity.Y + gravityForce
                );

                // Store current position as previous
                joint.PreviousPosition = joint.Position;

                // Update position (Verlet integration)
                joint.Position = new SKPoint(
                    joint.Position.X + velocity.X,
                    joint.Position.Y + velocity.Y
                );
            }

            // Step 2: Apply mouse drag force (IMPROVED with radius of influence)
            if (_draggedJoint != null)
            {
                // Direct drag - pull dragged joint toward target
                SKPoint delta = new SKPoint(
                    _dragTarget.X - _draggedJoint.Position.X,
                    _dragTarget.Y - _draggedJoint.Position.Y
                );

                // Strong pull for dragged joint
                _draggedJoint.Position = new SKPoint(
                    _draggedJoint.Position.X + delta.X * _dragStrength,
                    _draggedJoint.Position.Y + delta.Y * _dragStrength
                );

                // NEW: Apply influence to nearby joints (creates natural body follow)
                foreach (var joint in _joints.Values)
                {
                    if (joint == _draggedJoint || joint.IsAnchored) continue;

                    float distance = Distance(_draggedJoint.Position, joint.Position);
                    if (distance < _dragRadius)
                    {
                        // Proximity-based influence (inverse square falloff)
                        float influence = 1.0f - (distance / _dragRadius);
                        influence = influence * influence; // Square for smoother falloff
                        influence *= 0.3f; // Reduce strength

                        SKPoint deltaToJoint = new SKPoint(
                            delta.X * influence,
                            delta.Y * influence
                        );

                        joint.Position = new SKPoint(
                            joint.Position.X + deltaToJoint.X,
                            joint.Position.Y + deltaToJoint.Y
                        );
                    }
                }
            }

            // Step 3: Solve constraints (distance, angle, anchors)
            for (int iteration = 0; iteration < ConstraintIterations; iteration++)
            {
                SolveDistanceConstraints();
                SolveAngleConstraints();
                SolveAnchorConstraints();
            }
        }

        /// <summary>
        /// Start dragging a joint toward mouse position.
        /// </summary>
        public void StartDragging(JointModel joint, SKPoint mousePosition)
        {
            _draggedJoint = joint;
            _dragTarget = mousePosition;
        }

        /// <summary>
        /// Update drag target position (call on mouse move).
        /// </summary>
        public void UpdateDragTarget(SKPoint mousePosition)
        {
            _dragTarget = mousePosition;
        }

        /// <summary>
        /// Stop dragging (call on mouse release).
        /// </summary>
        public void StopDragging()
        {
            _draggedJoint = null;
        }

        /// <summary>
        /// Maintain bone lengths (distance constraints) with stiffness support.
        /// </summary>
        private void SolveDistanceConstraints()
        {
            foreach (var constraint in _constraints)
            {
                var jointA = constraint.JointA;
                var jointB = constraint.JointB;

                // Skip if either joint is anchored
                bool aAnchored = jointA.IsAnchored;
                bool bAnchored = jointB.IsAnchored;

                if (aAnchored && bAnchored) continue;

                // Calculate current distance
                SKPoint delta = new SKPoint(
                    jointB.Position.X - jointA.Position.X,
                    jointB.Position.Y - jointA.Position.Y
                );
                float currentLength = MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y);

                if (currentLength < 1e-6f) continue;

                // IMPROVED: Apply joint stiffness (0.0 = floppy, 1.0 = rigid)
                float stiffness = jointB.Stiffness * StiffnessMultiplier;
                stiffness = Math.Clamp(stiffness, 0.0f, 1.0f);

                // Calculate correction with stiffness
                float diff = (currentLength - constraint.RestLength) / currentLength;
                diff *= stiffness; // Apply stiffness
                SKPoint correction = new SKPoint(delta.X * diff * 0.5f, delta.Y * diff * 0.5f);

                // Apply correction (push/pull joints to maintain distance)
                if (!aAnchored)
                {
                    jointA.Position = new SKPoint(
                        jointA.Position.X + correction.X,
                        jointA.Position.Y + correction.Y
                    );
                }

                if (!bAnchored)
                {
                    jointB.Position = new SKPoint(
                        jointB.Position.X - correction.X,
                        jointB.Position.Y - correction.Y
                    );
                }
            }
        }

        /// <summary>
        /// Enforce angle limits (prevent unrealistic joint bending).
        /// </summary>
        private void SolveAngleConstraints()
        {
            foreach (var joint in _joints.Values)
            {
                if (joint.Parent == null) continue;
                if (joint.MinAngle <= -180f && joint.MaxAngle >= 180f) continue;  // No limits

                // Calculate current angle
                SKPoint delta = new SKPoint(
                    joint.Position.X - joint.Parent.Position.X,
                    joint.Position.Y - joint.Parent.Position.Y
                );
                float currentAngle = MathF.Atan2(delta.Y, delta.X) * 180f / MathF.PI;

                // Clamp angle
                float clampedAngle = Math.Clamp(currentAngle, joint.MinAngle, joint.MaxAngle);

                if (MathF.Abs(clampedAngle - currentAngle) > 0.1f)
                {
                    // Reposition joint to satisfy angle constraint
                    float angleRad = clampedAngle * MathF.PI / 180f;
                    float length = MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y);

                    joint.Position = new SKPoint(
                        joint.Parent.Position.X + MathF.Cos(angleRad) * length,
                        joint.Parent.Position.Y + MathF.Sin(angleRad) * length
                    );
                }
            }
        }

        /// <summary>
        /// Keep anchored joints in place.
        /// </summary>
        private void SolveAnchorConstraints()
        {
            foreach (var joint in _joints.Values)
            {
                if (joint.IsAnchored)
                {
                    // Reset to bind position (anchored joints don't move)
                    // Note: We need to store bind positions separately or in joint
                    // For now, just prevent velocity accumulation
                    joint.PreviousPosition = joint.Position;
                }
            }
        }

        /// <summary>
        /// Helper: Calculate distance between two points.
        /// </summary>
        private float Distance(SKPoint a, SKPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Distance constraint between two joints.
        /// </summary>
        private class DistanceConstraint
        {
            public JointModel JointA { get; set; }
            public JointModel JointB { get; set; }
            public float RestLength { get; set; }
        }
    }
}
