using System.Collections.Generic;
using SkiaSharp;

namespace SpriteEditor.Data
{
    /// <summary>
    /// Represents a rigging template with predefined skeleton structure.
    /// Used for one-click auto-rigging of characters.
    /// </summary>
    public class RigTemplate
    {
        /// <summary>
        /// Template name (e.g., "Humanoid", "Quadruped", "Weapon")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Template category for UI grouping
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Description shown to user
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// List of joints in this template
        /// </summary>
        public List<TemplateJoint> Joints { get; set; }

        /// <summary>
        /// Bone influence regions for proper weight isolation
        /// </summary>
        public List<BoneInfluenceRegion> Regions { get; set; }
        public TemplateMesh Mesh { get; set; }

        public RigTemplate()
        {
            Joints = new List<TemplateJoint>();
            Regions = new List<BoneInfluenceRegion>();
        }
    }

    /// <summary>
    /// Represents a joint in a template with normalized coordinates.
    /// \u003c/summary>
    public class TemplateJoint
    {
        /// <summary>
        /// Joint name (e.g., "LeftShoulder", "RightKnee")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Normalized position (0-1 range relative to sprite bounds)
        /// X: 0 = left edge, 1 = right edge
        /// Y: 0 = top edge, 1 = bottom edge
        /// </summary>
        public SKPoint NormalizedPosition { get; set; }

        /// <summary>
        /// Parent joint name (null for root)
        /// </summary>
        public string ParentName { get; set; }

        /// <summary>
        /// Region this joint belongs to (for weight calculation)
        /// </summary>
        public string RegionName { get; set; }

        /// <summary>
        /// Symmetry pair (e.g., "RightShoulder" for "LeftShoulder")
        /// Used for mirroring animations
        /// </summary>
        public string SymmetryPair { get; set; }

        // === CONSTRAINT AND IK PROPERTIES ===

        /// <summary>
        /// Minimum rotation angle (degrees) relative to bind pose.
        /// Prevents unrealistic joint bending (e.g., knee bending backwards).
        /// Default: -180 (no limit)
        /// </summary>
        public float MinAngle { get; set; } = -180f;

        /// <summary>
        /// Maximum rotation angle (degrees) relative to bind pose.
        /// Prevents unrealistic joint bending.
        /// Default: 180 (no limit)
        /// </summary>
        public float MaxAngle { get; set; } = 180f;

        /// <summary>
        /// IK chain name (e.g., "LeftArm", "RightLeg").
        /// Joints with same chain name form an IK chain.
        /// Null if not part of IK chain.
        /// </summary>
        public string IKChainName { get; set; }
    }

    /// <summary>
    /// Defines a region that constrains bone influence for weight calculation.
    /// Prevents left leg from affecting right leg, etc.
    /// </summary>
    public class BoneInfluenceRegion
    {
        /// <summary>
        /// Region name (e.g., "LeftArm", "RightLeg", "Torso")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// List of joint names that can influence vertices in this region
        /// </summary>
        public List<string> AllowedJoints { get; set; }

        public BoneInfluenceRegion()
        {
            AllowedJoints = new List<string>();
        }
    }
    /// <summary>
    /// Pre-configured mesh data in a template (NEW: for zero-configuration workflow)
    /// </summary>
    public class TemplateMesh
    {
        public List<TemplateVertex> Vertices { get; set; } = new List<TemplateVertex>();
        public List<TemplateTriangle> Triangles { get; set; } = new List<TemplateTriangle>();
    }

    /// <summary>
    /// Pre-configured vertex with normalized position and weights
    /// </summary>
    public class TemplateVertex
    {
        public int Id { get; set; }
        public SKPoint NormalizedPosition { get; set; }

        /// <summary>
        /// Pre-configured weights (joint name -> weight)
        /// Example: { "Chest": 0.8, "Neck": 0.2 }
        /// </summary>
        public Dictionary<string, float> Weights { get; set; } = new Dictionary<string, float>();
    }

    /// <summary>
    /// Triangle definition (uses vertex IDs)
    /// </summary>
    public class TemplateTriangle
    {
        public int V1 { get; set; }
        public int V2 { get; set; }
        public int V3 { get; set; }
    }


}
