using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using SpriteEditor.Data;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Services.Rigging
{
    /// <summary>
    /// Service for one-click template binding.
    /// Transforms template coordinates and loads pre-configured mesh + weights.
    /// </summary>
    public class TemplateBindingService
    {
        /// <summary>
        /// Bind template to sprite with given overlay transform.
        /// Returns complete skeleton + mesh ready for physics simulation.
        /// </summary>
        public BindingResult BindTemplate(
            RigTemplate template,
            SKBitmap sprite,
            SKPoint overlayPosition,
            float overlayScale,
            float overlayRotation)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (sprite == null)
                throw new ArgumentNullException(nameof(sprite));

            var result = new BindingResult();
            int jointIdCounter = 0;
            int vertexIdCounter = 0;

            // Step 1: Transform and create joints
            var jointMap = new Dictionary<string, JointModel>();
            
            foreach (var templateJoint in template.Joints)
            {
                var worldPos = TransformTemplatePosition(
                    templateJoint.NormalizedPosition,
                    sprite,
                    overlayPosition,
                    overlayScale,
                    overlayRotation
                );

                var joint = new JointModel(jointIdCounter++, worldPos)
                {
                    Name = templateJoint.Name,
                    Mass = templateJoint.Mass,
                    IsAnchored = templateJoint.IsAnchored,
                    MinAngle = templateJoint.MinAngle,
                    MaxAngle = templateJoint.MaxAngle,
                    Stiffness = templateJoint.Stiffness,
                    IKChainName = templateJoint.IKChainName
                };

                result.Joints.Add(joint);
                jointMap[templateJoint.Name] = joint;
            }

            // Step 2: Set parent relationships
            for (int i = 0; i < template.Joints.Count; i++)
            {
                var templateJoint = template.Joints[i];
                var joint = result.Joints[i];

                if (!string.IsNullOrEmpty(templateJoint.ParentName) &&
                    jointMap.TryGetValue(templateJoint.ParentName, out var parent))
                {
                    joint.Parent = parent;

                    // Calculate bone length
                    float dx = joint.Position.X - parent.Position.X;
                    float dy = joint.Position.Y - parent.Position.Y;
                    joint.BoneLength = MathF.Sqrt(dx * dx + dy * dy);
                    joint.Rotation = MathF.Atan2(dy, dx);
                }
            }

            // Step 3: Load pre-configured mesh from template
            if (template.Mesh != null && template.Mesh.Vertices != null)
            {
                var vertexMap = new Dictionary<int, VertexModel>();

                foreach (var templateVertex in template.Mesh.Vertices)
                {
                    var worldPos = TransformTemplatePosition(
                        templateVertex.NormalizedPosition,
                        sprite,
                        overlayPosition,
                        overlayScale,
                        overlayRotation
                    );

                    var vertex = new VertexModel(vertexIdCounter++, worldPos);

                    // Convert template weights (joint names) to joint IDs
                    if (templateVertex.Weights != null)
                    {
                        foreach (var (jointName, weight) in templateVertex.Weights)
                        {
                            if (jointMap.TryGetValue(jointName, out var joint))
                            {
                                vertex.Weights[joint.Id] = weight;
                            }
                        }
                    }

                    result.Vertices.Add(vertex);
                    vertexMap[templateVertex.Id] = vertex;
                }

                // Step 4: Load triangles
                if (template.Mesh.Triangles != null)
                {
                    foreach (var templateTriangle in template.Mesh.Triangles)
                    {
                        if (vertexMap.TryGetValue(templateTriangle.V1, out var v1) &&
                            vertexMap.TryGetValue(templateTriangle.V2, out var v2) &&
                            vertexMap.TryGetValue(templateTriangle.V3, out var v3))
                        {
                            result.Triangles.Add(new TriangleModel(v1, v2, v3));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Transform normalized template position to world coordinates.
        /// </summary>
        private SKPoint TransformTemplatePosition(
            SKPoint normalizedPos,
            SKBitmap sprite,
            SKPoint overlayPosition,
            float overlayScale,
            float overlayRotation)
        {
            // Detect sprite bounds (non-transparent area)
            var bounds = DetectSpriteBounds(sprite);

            // Map normalized (0-1) to sprite bounds
            float spriteX = bounds.Left + normalizedPos.X * bounds.Width;
            float spriteY = bounds.Top + normalizedPos.Y * bounds.Height;

            // Apply overlay transform (scale + rotation around overlay center)
            float relX = (spriteX - overlayPosition.X) * overlayScale;
            float relY = (spriteY - overlayPosition.Y) * overlayScale;

            if (MathF.Abs(overlayRotation) > 0.001f)
            {
                float cos = MathF.Cos(overlayRotation);
                float sin = MathF.Sin(overlayRotation);
                float rotX = relX * cos - relY * sin;
                float rotY = relX * sin + relY * cos;
                relX = rotX;
                relY = rotY;
            }

            return new SKPoint(
                overlayPosition.X + relX,
                overlayPosition.Y + relY
            );
        }

        /// <summary>
        /// Detect sprite bounds (bounding box of non-transparent pixels).
        /// </summary>
        private SKRectI DetectSpriteBounds(SKBitmap bitmap)
        {
            int minX = bitmap.Width, maxX = 0;
            int minY = bitmap.Height, maxY = 0;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.Alpha > 10)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (minX > maxX || minY > maxY)
                return new SKRectI(0, 0, bitmap.Width, bitmap.Height);

            return new SKRectI(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// NEW: Bind template using manually edited joint positions.
        /// This respects user's adjustments to overlay joints.
        /// </summary>
        public BindingResult BindTemplateWithEditedJoints(
            RigTemplate template,
            SKBitmap sprite,
            List<JointModel> editedJoints)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (sprite == null)
                throw new ArgumentNullException(nameof(sprite));
            if (editedJoints == null || editedJoints.Count == 0)
                throw new ArgumentException("Edited joints cannot be null or empty", nameof(editedJoints));

            var result = new BindingResult();
            int jointIdCounter = 0;
            int vertexIdCounter = 0;

            // Step 1: Use edited joint positions directly!
            var jointMap = new Dictionary<string, JointModel>();
            
            foreach (var editedJoint in editedJoints)
            {
                // Create new joint with edited position
                var joint = new JointModel(jointIdCounter++, editedJoint.Position)
                {
                    Name = editedJoint.Name,
                    Mass = editedJoint.Mass,
                    IsAnchored = editedJoint.IsAnchored,
                    MinAngle = editedJoint.MinAngle,
                    MaxAngle = editedJoint.MaxAngle,
                    Stiffness = editedJoint.Stiffness,
                    IKChainName = editedJoint.IKChainName
                };

                result.Joints.Add(joint);
                jointMap[joint.Name] = joint;
            }

            // Step 2: Set parent relationships  
            for (int i = 0; i < template.Joints.Count && i < result.Joints.Count; i++)
            {
                var templateJoint = template.Joints[i];
                var joint = result.Joints[i];

                if (!string.IsNullOrEmpty(templateJoint.ParentName) &&
                    jointMap.TryGetValue(templateJoint.ParentName, out var parent))
                {
                    joint.Parent = parent;

                    // Calculate bone length and rotation
                    float dx = joint.Position.X - parent.Position.X;
                    float dy = joint.Position.Y - parent.Position.Y;
                    joint.BoneLength = MathF.Sqrt(dx * dx + dy * dy);
                    joint.Rotation = MathF.Atan2(dy, dx);
                }
            }

            // Step 3: Load mesh from template (use template mesh structure)
            if (template.Mesh != null && template.Mesh.Vertices != null)
            {
                var vertexMap = new Dictionary<int, VertexModel>();
                var spriteBounds = DetectSpriteBounds(sprite);

                foreach (var templateVertex in template.Mesh.Vertices)
                {
                    // Transform vertex to sprite space
                    float worldX = spriteBounds.Left + templateVertex.NormalizedPosition.X * spriteBounds.Width;
                    float worldY = spriteBounds.Top + templateVertex.NormalizedPosition.Y * spriteBounds.Height;

                    var vertex = new VertexModel(vertexIdCounter++, new SKPoint(worldX, worldY));

                    // Convert template weights to joint IDs
                    if (templateVertex.Weights != null)
                    {
                        foreach (var weightEntry in templateVertex.Weights)
                        {
                            if (jointMap.TryGetValue(weightEntry.Key, out var joint))
                            {
                                vertex.Weights[joint.Id] = weightEntry.Value;
                            }
                        }
                    }

                    result.Vertices.Add(vertex);
                    vertexMap[result.Vertices.Count - 1] = vertex;
                }

                // Copy triangles
                if (template.Mesh.Triangles != null)
                {
                    foreach (var tri in template.Mesh.Triangles)
                    {
                        if (vertexMap.TryGetValue(tri.V1, out var v1) &&
                            vertexMap.TryGetValue(tri.V2, out var v2) &&
                            vertexMap.TryGetValue(tri.V3, out var v3))
                        {
                            result.Triangles.Add(new TriangleModel(v1, v2, v3));
                        }
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Result of template binding operation.
    /// </summary>
    public class BindingResult
    {
        public List<JointModel> Joints { get; set; } = new List<JointModel>();
        public List<VertexModel> Vertices { get; set; } = new List<VertexModel>();
        public List<TriangleModel> Triangles { get; set; } = new List<TriangleModel>();
    }
}
