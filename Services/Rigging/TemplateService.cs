using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using SkiaSharp;
using SpriteEditor.Data;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Services.Rigging
{
    /// <summary>
    /// Service for loading and applying rig templates.
    /// </summary>
    public class TemplateService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// Loads a rig template from JSON file.
        /// </summary>
        public RigTemplate LoadTemplate(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Template file not found: {filePath}");

            string json = File.ReadAllText(filePath);
            var template = JsonSerializer.Deserialize<RigTemplate>(json, _jsonOptions);

            if (template == null)
                throw new InvalidOperationException($"Failed to deserialize template: {filePath}");

            return template;
        }

        /// <summary>
        /// Loads the built-in Humanoid template.
        /// </summary>
        public RigTemplate LoadHumanoidTemplate()
        {
            // Try Professional template first
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string templatePath = Path.Combine(baseDir, "Data", "Templates", "Humanoid.Professional.template.json");

            if (!File.Exists(templatePath))
            {
                // Fallback: try Perfect template
                templatePath = Path.Combine(baseDir, "Data", "Templates", "Humanoid.Perfect.template.json");
            }

            if (!File.Exists(templatePath))
            {
                // Fallback: try old template
                templatePath = Path.Combine(baseDir, "Data", "Templates", "Humanoid.template.json");
            }

            if (!File.Exists(templatePath))
            {
                // Last resort: try relative to executable
                templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Templates", "Humanoid.Professional.template.json");
            }

            return LoadTemplate(templatePath);
        }

        /// <summary>
        /// Applies a template to create joints for a sprite.
        /// Maps normalized template coordinates to actual pixel coordinates.
        /// </summary>
        public void ApplyTemplate(
            RigTemplate template,
            SKBitmap sprite,
            ObservableCollection<JointModel> joints,
            ref int jointIdCounter)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (sprite == null)
                throw new ArgumentNullException(nameof(sprite));

            // Clear existing joints
            joints.Clear();

            // Detect sprite bounds (non-transparent area)
            var bounds = DetectSpriteBounds(sprite);
            float spriteWidth = bounds.Width;
            float spriteHeight = bounds.Height;
            float offsetX = bounds.Left;
            float offsetY = bounds.Top;

            // Create joints dictionary for parent lookup
            var jointNameMap = new Dictionary<string, JointModel>();

            // First pass: create all joints
            foreach (var templateJoint in template.Joints)
            {
                // Map normalized position to pixel coordinates
                float pixelX = offsetX + templateJoint.NormalizedPosition.X * spriteWidth;
                float pixelY = offsetY + templateJoint.NormalizedPosition.Y * spriteHeight;

                var joint = new JointModel(jointIdCounter++, new SKPoint(pixelX, pixelY))
                {
                    Name = templateJoint.Name,
                    // === NEW: Copy physics properties from template ===
                    Mass = templateJoint.Mass,
                    IsAnchored = templateJoint.IsAnchored,
                    MinAngle = templateJoint.MinAngle,
                    MaxAngle = templateJoint.MaxAngle,
                    Stiffness = templateJoint.Stiffness,
                    IKChainName = templateJoint.IKChainName
                    // ==================================================
                };

                joints.Add(joint);
                jointNameMap[templateJoint.Name] = joint;
            }

            // Second pass: set up parent relationships and calculate bone lengths
            for (int i = 0; i < template.Joints.Count; i++)
            {
                var templateJoint = template.Joints[i];
                var joint = joints[i];

                if (!string.IsNullOrEmpty(templateJoint.ParentName) && 
                    jointNameMap.TryGetValue(templateJoint.ParentName, out var parent))
                {
                    joint.Parent = parent;

                    // Calculate bone length (distance from parent to this joint)
                    float dx = joint.Position.X - parent.Position.X;
                    float dy = joint.Position.Y - parent.Position.Y;
                    joint.BoneLength = MathF.Sqrt(dx * dx + dy * dy);
                    
                    // Calculate and set initial rotation
                    joint.Rotation = MathF.Atan2(dy, dx);
                    joint.BindRotation = joint.Rotation;
                }
                
                
                // Set BindPosition for all joints
                joint.BindPosition = joint.Position;

                // Anchor the ROOT (Head/Hips) by default so it doesn't fall
                if (joint.Parent == null)
                {
                    joint.IsAnchored = true;
                }
            }
        }

        /// <summary>
        /// Detects the bounding box of non-transparent pixels in the sprite.
        /// </summary>
        private SKRect DetectSpriteBounds(SKBitmap sprite)
        {
            int minX = sprite.Width;
            int minY = sprite.Height;
            int maxX = 0;
            int maxY = 0;

            bool foundPixel = false;

            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    var pixel = sprite.GetPixel(x, y);
                    if (pixel.Alpha > 32) // Non-transparent
                    {
                        foundPixel = true;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (!foundPixel)
            {
                // Fallback to full sprite if no pixels found
                return new SKRect(0, 0, sprite.Width, sprite.Height);
            }

            return new SKRect(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Gets bone influence region for a vertex based on template regions.
        /// Returns list of allowed joint names that can influence the vertex.
        /// </summary>
        public List<string> GetAllowedJointsForVertex(
            RigTemplate template,
            SKPoint vertexPosition,
            SKRect spriteBounds,
            Dictionary<string, JointModel> jointNameMap)
        {
            // Simple heuristic: find closest joint and return its region
            float closestDist = float.MaxValue;
            string closestJointRegion = null;

            foreach (var templateJoint in template.Joints)
            {
                if (jointNameMap.TryGetValue(templateJoint.Name, out var joint))
                {
                    float dist = Distance(vertexPosition, joint.Position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestJointRegion = templateJoint.RegionName;
                    }
                }
            }

            // Find region and return allowed joints
            if (!string.IsNullOrEmpty(closestJointRegion))
            {
                var region = template.Regions.FirstOrDefault(r => r.Name == closestJointRegion);
                if (region != null)
                {
                    return region.AllowedJoints;
                }
            }

            // Fallback: allow all joints
            return jointNameMap.Keys.ToList();
        }

        private float Distance(SKPoint a, SKPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }
    }
}
