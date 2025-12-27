using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Services.Rigging
{
    /// <summary>
    /// Professional Physics Engine for Rigging.
    /// Uses Sub-Stepping Verlet Integration for industrial-grade stability.
    /// Features: Rigid constraints, Active Ragdoll (Pose Matching), Ground Collision.
    /// Includes Safety Clamps (MaxVelocity, NaN Check) to prevent explosion.
    /// </summary>
    public class PhysicsService
    {
        // Physics parameters
        public float Gravity { get; set; } = 800f;   
        public float Damping { get; set; } = 0.88f;  // Reduced for faster energy absorption  
        public int SubSteps { get; set; } = 8;       
        public float PoseStiffness { get; set; } = 0.6f; 
        public float GroundY { get; set; } = 600f;   
        public float MaxVelocity { get; set; } = 1500f; 
        
        private Dictionary<int, JointModel> _joints;
        private List<DistanceConstraint> _constraints;
        private JointModel _draggedJoint;
        private SKPoint _dragTarget;
        private float _dragStrength = 0.2f;

        public PhysicsService()
        {
            _joints = new Dictionary<int, JointModel>();
            _constraints = new List<DistanceConstraint>();
        }

        public void Initialize(IEnumerable<JointModel> joints)
        {
            _joints.Clear();
            _constraints.Clear();

            foreach (var joint in joints)
            {
                _joints[joint.Id] = joint;
                joint.PreviousPosition = joint.Position; // Reset velocity

                // NOTE: BindRotation is now calculated in TemplateRiggingViewModel.BindTemplate()
                // No need to recalculate here - it's already correct!

                // Add Distance Constraint
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

        public void VerletStep(float deltaTime)
        {
            if (_joints.Count == 0) return;

            float dt = Math.Clamp(deltaTime, 0.001f, 0.03f); 
            float subStepDt = dt / SubSteps;

            for (int i = 0; i < SubSteps; i++)
            {
                ApplyForces(subStepDt);
                ApplyConstraints(); 
            }
        }

        private void ApplyForces(float dt)
        {
            foreach (var joint in _joints.Values)
            {
                if (joint.IsAnchored) continue;

                SKPoint tempPos = joint.Position;
                
                float vx = (joint.Position.X - joint.PreviousPosition.X);
                float vy = (joint.Position.Y - joint.PreviousPosition.Y);

                // --- SAFETY: CLAMP VELOCITY ---
                float speedSq = vx*vx + vy*vy;
                if (speedSq > (MaxVelocity * dt) * (MaxVelocity * dt))
                {
                    float speed = MathF.Sqrt(speedSq);
                    float scale = (MaxVelocity * dt) / speed;
                    vx *= scale;
                    vy *= scale;
                }

                vx *= Damping;
                vy *= Damping;
                vy += Gravity * dt * dt; 

                float newX = joint.Position.X + vx;
                float newY = joint.Position.Y + vy;

                // --- SAFETY: NaN CHECK ---
                if (float.IsNaN(newX) || float.IsInfinity(newX)) newX = joint.PreviousPosition.X;
                if (float.IsNaN(newY) || float.IsInfinity(newY)) newY = joint.PreviousPosition.Y;

                joint.Position = new SKPoint(newX, newY);
                joint.PreviousPosition = tempPos;
            }

            if (_draggedJoint != null)
            {
                var deltaX = _dragTarget.X - _draggedJoint.Position.X;
                var deltaY = _dragTarget.Y - _draggedJoint.Position.Y;
                _draggedJoint.Position = new SKPoint(
                    _draggedJoint.Position.X + deltaX * _dragStrength,
                    _draggedJoint.Position.Y + deltaY * _dragStrength
                );
            }
        }

        private void ApplyConstraints()
        {
            // CRITICAL: Higher iterations = Less jitter (Industry Standard: 5-10)
            // Distance constraints need multiple passes to converge
            for (int i = 0; i < 8; i++) 
            {
                foreach (var c in _constraints)
                    SolveDistanceConstraint(c);
            }

            // Angle constraints also need refinement
            for (int i = 0; i < 3; i++)
            {
                SolveAngleConstraints(); 
            }
            
            // CRITICAL: Active Ragdoll - Pull toward bind pose (Standing!)
            ApplyPoseForces();
            
            ApplyGroundCollision();
        }

        private void SolveDistanceConstraint(DistanceConstraint c)
        {
            if (c.JointA.IsAnchored && c.JointB.IsAnchored) return;

            float dx = c.JointB.Position.X - c.JointA.Position.X;
            float dy = c.JointB.Position.Y - c.JointA.Position.Y;
            float currentDist = MathF.Sqrt(dx * dx + dy * dy);
            
            if (currentDist < 0.001f || float.IsNaN(currentDist)) return;

            float diff = (currentDist - c.RestLength) / currentDist;
            if (MathF.Abs(diff) > 0.5f) diff = 0.5f * MathF.Sign(diff);

            float offsetX = dx * diff * 0.5f;
            float offsetY = dy * diff * 0.5f;

            float totalMass = c.JointA.Mass + c.JointB.Mass;
            float ratioA = c.JointB.Mass / totalMass;
            float ratioB = c.JointA.Mass / totalMass;

            if (c.JointA.IsAnchored)
            {
                c.JointB.Position = new SKPoint(c.JointB.Position.X - offsetX * 2.0f, c.JointB.Position.Y - offsetY * 2.0f);
            }
            else if (c.JointB.IsAnchored)
            {
                c.JointA.Position = new SKPoint(c.JointA.Position.X + offsetX * 2.0f, c.JointA.Position.Y + offsetY * 2.0f);
            }
            else
            {
                c.JointA.Position = new SKPoint(c.JointA.Position.X + offsetX * 2.0f * ratioA, c.JointA.Position.Y + offsetY * 2.0f * ratioA);
                c.JointB.Position = new SKPoint(c.JointB.Position.X - offsetX * 2.0f * ratioB, c.JointB.Position.Y - offsetY * 2.0f * ratioB);
            }
        }

        /// <summary>
        /// Solves Angle Constraints relative to BIND POSE (Rest Pose).
        /// Updated to rely on correct BindRotation calculated in Initialize.
        /// </summary>
        private void SolveAngleConstraints()
        {
            foreach (var joint in _joints.Values)
            {
                if (joint.Parent == null) continue;
                if (float.IsNaN(joint.Position.X)) continue;

                // 1. Calculate Parent Angle (World Space)
                float parentAngle = 0f;
                if (joint.Parent.Parent != null)
                {
                    float pdx = joint.Parent.Position.X - joint.Parent.Parent.Position.X;
                    float pdy = joint.Parent.Position.Y - joint.Parent.Parent.Position.Y;
                    parentAngle = MathF.Atan2(pdy, pdx);
                }
                
                // 2. Calculate Current Angle (World Space)
                float dx = joint.Position.X - joint.Parent.Position.X;
                float dy = joint.Position.Y - joint.Parent.Position.Y;
                float currentAngle = MathF.Atan2(dy, dx);

                // 3. Current Local Angle (Relative to Parent)
                float localAngle = currentAngle - parentAngle;
                localAngle = NormalizeAngle(localAngle);

                // 4. Calculate Rest (Bind) Local Angle
                // Local Bind Angle = Joint.BindRotation - Parent.BindRotation
                // Note: We use the stored BindRotations because Physics works in global space but limits are local logic
                float parentBindRot = (joint.Parent != null) ? joint.Parent.BindRotation : 0f;
                float jointBindRot = joint.BindRotation;
                float targetLocalAngle = jointBindRot - parentBindRot;
                targetLocalAngle = NormalizeAngle(targetLocalAngle);
                
                // 5. Calculate Deviation from Bind Pose
                float deviation = localAngle - targetLocalAngle;
                deviation = NormalizeAngle(deviation);
                float deviationDeg = deviation * 180f / MathF.PI;

                // --- ACTIVE RAGDOLL (Pose Matching) ---
                if (PoseStiffness > 0)
                {
                     if (MathF.Abs(deviation) > 0.01f)
                     {
                         float correction = deviation * PoseStiffness;
                         float newAngle = currentAngle - correction;
                         float len = MathF.Sqrt(dx*dx + dy*dy);
                         
                         joint.Position = new SKPoint(
                            joint.Parent.Position.X + MathF.Cos(newAngle) * len,
                            joint.Parent.Position.Y + MathF.Sin(newAngle) * len
                         );
                         
                         // Recalculate
                         dx = joint.Position.X - joint.Parent.Position.X;
                         dy = joint.Position.Y - joint.Parent.Position.Y;
                         currentAngle = MathF.Atan2(dy, dx);
                         localAngle = NormalizeAngle(currentAngle - parentAngle);
                         deviation = NormalizeAngle(localAngle - targetLocalAngle);
                         deviationDeg = deviation * 180f / MathF.PI;
                     }
                }

                // --- ANGULAR LIMITS (Relative to Bind Pose) ---
                if (joint.MinAngle > -180 && joint.MaxAngle < 180)
                {
                    float clampedDev = Math.Clamp(deviationDeg, joint.MinAngle, joint.MaxAngle);
                    
                    if (MathF.Abs(clampedDev - deviationDeg) > 0.1f)
                    {
                        // Convert clamped deviation back to World Angle
                        float targetDevRad = clampedDev * MathF.PI / 180f;
                        float targetLocal = targetLocalAngle + targetDevRad;
                        float targetWorld = parentAngle + targetLocal;
                        
                        float len = MathF.Sqrt(dx*dx + dy*dy);
                        joint.Position = new SKPoint(
                            joint.Parent.Position.X + MathF.Cos(targetWorld) * len,
                            joint.Parent.Position.Y + MathF.Sin(targetWorld) * len
                        );
                    }
                }
            }
        }

        /// <summary>
        /// ACTIVE RAGDOLL: Pulls joints toward their bind pose (standing position)
        /// This creates the "WOW" effect - character wants to stand!
        /// </summary>
        private void ApplyPoseForces()
        {
            if (PoseStiffness <= 0) return;

            foreach (var joint in _joints.Values)
            {
                if (joint.IsAnchored) continue;

                // Calculate offset from bind position
                float dx = joint.BindPosition.X - joint.Position.X;
                float dy = joint.BindPosition.Y - joint.Position.Y;

                // Apply force by manipulating previous position (Verlet style)
                // Higher PoseStiffness = stronger pull toward bind pose
                float forceFactor = PoseStiffness * 0.1f; // Scale down for stability
                
                joint.PreviousPosition = new SKPoint(
                    joint.PreviousPosition.X - dx * forceFactor,
                    joint.PreviousPosition.Y - dy * forceFactor
                );
            }
        }

        private float NormalizeAngle(float angle)
        {
            while (angle <= -MathF.PI) angle += 2 * MathF.PI;
            while (angle > MathF.PI) angle -= 2 * MathF.PI;
            return angle;
        }

        /// <summary>
        /// Professional Ground Collision: SOLID FLOOR with bounce damping
        /// Character stands naturally on ground, cannot penetrate.
        /// </summary>
        private void ApplyGroundCollision()
        {
            foreach (var joint in _joints.Values)
            {
                if (joint.Position.Y > GroundY)
                {
                    // HARD CONSTRAINT: Cannot penetrate ground!
                    joint.Position = new SKPoint(joint.Position.X, GroundY);
                    
                    //Calculate vertical velocity
                    float vy = joint.Position.Y - joint.PreviousPosition.Y;
                    
                    // Bounce with strong damping (realistic foot-ground contact)
                    float bounceDamping = 0.1f; // Almost no bounce (realistic)
                    float newVy = -vy * bounceDamping;
                    
                    // Strong horizontal friction for standing stability
                    float vx = joint.Position.X - joint.PreviousPosition.X;
                    float friction = 0.4f; // Strong friction (character doesn't slide)
                    float newVx = vx * friction;
                    
                    // Update previous position to create new velocity
                    joint.PreviousPosition = new SKPoint(
                        joint.Position.X - newVx,
                        GroundY - newVy
                    );
                }
            }
        }

        public void StartDragging(JointModel joint, SKPoint mousePosition)
        {
            _draggedJoint = joint;
            _dragTarget = mousePosition;
        }

        public void UpdateDragTarget(SKPoint mousePosition) => _dragTarget = mousePosition;
        public void StopDragging() => _draggedJoint = null;

        private class DistanceConstraint
        {
            public JointModel JointA { get; set; }
            public JointModel JointB { get; set; }
            public float RestLength { get; set; }
        }
    }
}
