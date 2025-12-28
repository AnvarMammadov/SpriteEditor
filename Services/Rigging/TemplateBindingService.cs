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
        private readonly MeshGenerationService _meshGenerationService = new MeshGenerationService();
        private readonly AutoWeightService _autoWeightService = new AutoWeightService();

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
                    MinAngle = templateJoint.MinAngle,
                    MaxAngle = templateJoint.MaxAngle,
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
                    joint.BindRotation = joint.Rotation; // CRITICAL
                }
                
                // CRITICAL
                joint.BindPosition = joint.Position;
            }

            // Step 3: Always Auto-Generate Mesh
            // We ignore the template's static mesh because it doesn't fit the specific sprite contour.
            /*
            if (template.Mesh != null && template.Mesh.Vertices != null)
            {
               // ... (code omitted for brevity, keeping it disabled) ...
            }
            else
            */
            {
                // ALWAYS: Auto-generate mesh conforming to sprite
                GenerateAndWeightMesh(result, sprite, template, overlayPosition, overlayScale, overlayRotation);
            }

            return result;
        }

        /// <summary>
        /// Transform normalized template position to world coordinates.
        /// Applies overlay transform for consistent positioning.
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
            List<JointModel> editedJoints,
            SKPoint overlayPos,
            float overlayScale,
            float overlayRot)
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
                    MinAngle = editedJoint.MinAngle,
                    MaxAngle = editedJoint.MaxAngle,
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
                    joint.BindRotation = joint.Rotation; // CRITICAL: Update BindRotation to match bind pose!
                }
                
                // CRITICAL: Ensure BindPosition is set for all joints
                joint.BindPosition = joint.Position;
            }

            // Step 3: Always Auto-Generate Mesh
            // We ignore the template's static mesh because it doesn't fit the specific sprite contour.
            /*
            if (template.Mesh != null && template.Mesh.Vertices != null)
            {
                // ... (code omitted for brevity, keeping it disabled) ...
            }
            else
            */
            {
                // FALLBACK: Auto-generate mesh if template lacks one
                GenerateAndWeightMesh(result, sprite, template, overlayPos, overlayScale, overlayRot);
            }

            return result;
        }

        private void GenerateAndWeightMesh(
            BindingResult result, 
            SKBitmap sprite, 
            RigTemplate template,
            SKPoint overlayPos,
            float overlayScale,
            float overlayRot)
        {
            try
            {
                // 1. Generate Mesh (Vertices + Triangles) with Contour Constraints
                var (rawVertices, rawTriangles) = _meshGenerationService.GenerateMesh(sprite);
                
                if (rawVertices.Count == 0) return;

                // Add to result
                result.Vertices.AddRange(rawVertices);
                result.Triangles.AddRange(rawTriangles);

                // 2. Transform Vertices to World Space (same as joints)
                // CRITICAL: Vertices must be in SAME coordinate system as joints!
                var spriteBounds = DetectSpriteBounds(sprite);
                SKPoint spriteCenter = new SKPoint(
                    spriteBounds.Left + spriteBounds.Width / 2f,
                    spriteBounds.Top + spriteBounds.Height / 2f
                );

                foreach (var v in result.Vertices)
                {
                    // Save original local position as Texture Coordinate
                    v.TextureCoordinate = new SKPoint(v.BindPosition.X, v.BindPosition.Y);

                    // Apply Overlay Transform to match joint coordinate system
                    // 1. Center vertices around sprite center
                    float localX = v.BindPosition.X - spriteCenter.X;
                    float localY = v.BindPosition.Y - spriteCenter.Y;

                    // 2. Apply Rotation
                    float cos = MathF.Cos(overlayRot);
                    float sin = MathF.Sin(overlayRot);
                    float rotX = localX * cos - localY * sin;
                    float rotY = localX * sin + localY * cos;

                    // 3. Apply Scale
                    rotX *= overlayScale;
                    rotY *= overlayScale;

                    // 4. Apply Translation
                    float worldX = overlayPos.X + rotX;
                    float worldY = overlayPos.Y + rotY;

                    // Update Vertex Position (now in world space, matching joints)
                    v.BindPosition = new SKPoint(worldX, worldY);
                    v.CurrentPosition = new SKPoint(worldX, worldY);
                }

                // 3. Auto-Weight
                var obsVertices = new System.Collections.ObjectModel.ObservableCollection<VertexModel>(result.Vertices);
                var obsJoints = new System.Collections.ObjectModel.ObservableCollection<JointModel>(result.Joints);
                var obsTriangles = new System.Collections.ObjectModel.ObservableCollection<TriangleModel>(result.Triangles);

                _autoWeightService.CalculateWeights(obsVertices, obsJoints, obsTriangles, template);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Mesh generation failed: {ex.Message}");
            }
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
