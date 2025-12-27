using System.Collections.Generic;
using SkiaSharp;

namespace SpriteEditor.Data
{
    /// <summary>
    /// Represents a single keyframe in an animation timeline.
    /// Stores the pose (positions and rotations) of all joints at a specific time.
    /// </summary>
    public class Keyframe
    {
        /// <summary>
        /// Time in seconds from the start of the animation.
        /// </summary>
        public float Time { get; set; }

        /// <summary>
        /// Dictionary of joint poses, keyed by joint ID.
        /// Key: Joint ID, Value: Joint pose (position + rotation)
        /// </summary>
        public Dictionary<int, JointPose> JointPoses { get; set; } = new Dictionary<int, JointPose>();

        /// <summary>
        /// Optional user note for this keyframe (e.g., "Apex of jump")
        /// </summary>
        public string Note { get; set; }

        public Keyframe()
        {
        }

        public Keyframe(float time)
        {
            Time = time;
        }

        /// <summary>
        /// Creates a deep copy of this keyframe.
        /// </summary>
        public Keyframe Clone()
        {
            var clone = new Keyframe(Time)
            {
                Note = Note
            };

            foreach (var kvp in JointPoses)
            {
                clone.JointPoses[kvp.Key] = kvp.Value;
            }

            return clone;
        }
    }

    /// <summary>
    /// Represents the position and rotation of a single joint at a specific moment.
    /// This is a value type (struct) for performance.
    /// </summary>
    public struct JointPose
    {
        /// <summary>
        /// World-space position of the joint.
        /// </summary>
        public SKPoint Position { get; set; }

        /// <summary>
        /// Rotation in radians.
        /// </summary>
        public float Rotation { get; set; }

        public JointPose(SKPoint position, float rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        /// <summary>
        /// Linear interpolation between two joint poses.
        /// </summary>
        public static JointPose Lerp(JointPose a, JointPose b, float t)
        {
            return new JointPose(
                new SKPoint(
                    a.Position.X + (b.Position.X - a.Position.X) * t,
                    a.Position.Y + (b.Position.Y - a.Position.Y) * t
                ),
                LerpAngle(a.Rotation, b.Rotation, t)
            );
        }

        /// <summary>
        /// Spherical linear interpolation for angles (handles wrapping around 2π).
        /// </summary>
        private static float LerpAngle(float a, float b, float t)
        {
            // Normalize angles to [-π, π]
            float da = (b - a) % (MathF.PI * 2);
            float shortestAngle = 2 * da % (MathF.PI * 2) - da;
            return a + shortestAngle * t;
        }
    }
}
