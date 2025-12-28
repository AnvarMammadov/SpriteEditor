using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

namespace SpriteEditor.ViewModels
{
    /// <summary>
    /// Bir sümük (Bone) iki oynaq (Joint) arasında çəkilən xətdir.
    /// Hierarchical structure: Parent-Child relationships for IK and FK.
    /// </summary>
    public partial class JointModel : ObservableObject
    {
        public int Id { get; set; }
        public SKPoint Position { get; set; }
        public JointModel Parent { get; set; }  // Parent joint (null for root)

        /// <summary>
        /// Child joints connected to this joint.
        /// </summary>
        public List<JointModel> Children { get; set; } = new List<JointModel>();

        /// <summary>
        /// Length of the bone connecting this joint to its parent.
        /// Fixed after binding to prevent stretching.
        /// </summary>
        public float BoneLength { get; set; }

        /// <summary>
        /// Absolute (world) rotation angle in radians.
        /// </summary>
        public float Rotation { get; set; }

        [ObservableProperty]
        private string _name;

        /// <summary>
        /// Bind pose position (position when skeleton was bound to mesh).
        /// Used for skinning calculations.
        /// </summary>
        public SKPoint BindPosition { get; set; }

        /// <summary>
        /// Bind pose rotation (rotation when skeleton was bound to mesh).
        /// Used for skinning calculations.
        /// </summary>
        public float BindRotation { get; set; }

        // === IK AND CONSTRAINT PROPERTIES ===
        /// <summary>
        /// Minimum rotation angle (degrees) relative to bind pose.
        /// Prevents unrealistic joint bending.
        /// </summary>
        public float MinAngle { get; set; } = -180f;

        /// <summary>
        /// Maximum rotation angle (degrees) relative to bind pose.
        /// Prevents unrealistic joint bending.
        /// </summary>
        public float MaxAngle { get; set; } = 180f;

        /// <summary>
        /// IK chain name (e.g., "LeftArm", "RightLeg").
        /// Joints with the same chain name form an IK chain.
        /// </summary>
        public string IKChainName { get; set; }
        // ====================================

        public JointModel(int id, SKPoint position, JointModel parent = null)
        {
            Id = id;
            Position = position;
            Parent = parent;
            Children = new List<JointModel>();

            // If parent exists, add this joint to parent's children
            if (parent != null)
            {
                parent.Children.Add(this);
            }

            BoneLength = 0;
            Rotation = 0;
            _name = $"Joint_{id}";
        }

        /// <summary>
        /// Get all descendant joints (recursive).
        /// </summary>
        public List<JointModel> GetAllChildren()
        {
            var result = new List<JointModel>();
            foreach (var child in Children)
            {
                result.Add(child);
                result.AddRange(child.GetAllChildren());
            }
            return result;
        }

        /// <summary>
        /// Get the root joint (top of hierarchy).
        /// </summary>
        public JointModel GetRoot()
        {
            var current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
            }
            return current;
        }

        /// <summary>
        /// Get depth in hierarchy (root = 0).
        /// </summary>
        public int GetDepth()
        {
            int depth = 0;
            var current = this;
            while (current.Parent != null)
            {
                depth++;
                current = current.Parent;
            }
            return depth;
        }

        /// <summary>
        /// Get chain from this joint to root (used for IK).
        /// Returns list with this joint first, root last.
        /// </summary>
        public List<JointModel> GetChainToRoot()
        {
            var chain = new List<JointModel>();
            var current = this;
            while (current != null)
            {
                chain.Add(current);
                current = current.Parent;
            }
            return chain;
        }

        /// <summary>
        /// Gets the IK chain for this joint based on IKChainName identity.
        /// Stops when parent has a different ChainName or null.
        /// Essential for isolating limbs from torso.
        /// </summary>
        public List<JointModel> GetIKChain()
        {
            var chain = new List<JointModel>();
            
            if (string.IsNullOrEmpty(IKChainName))
                return chain;

            var current = this;
            while (current != null)
            {
                // Stop if current joint doesn't belong to the same chain
                // (Unless it's the very first joint we clicked - though that case implies mismatch)
                if (current.IKChainName != this.IKChainName)
                    break;

                chain.Add(current);
                current = current.Parent;
            }
            return chain;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
