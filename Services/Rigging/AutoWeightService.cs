using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SkiaSharp;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Services.Rigging
{
    /// <summary>
    /// Service responsible for automatic weight calculation in skinning.
    /// Extracted from RiggingViewModel to separate concerns.
    /// </summary>
    public class AutoWeightService
    {
        // Sprite bitmap for alpha-based separation
        private SKBitmap _spriteBitmap;

        // Auto-weight parameters (will be configurable)
        public float SigmaFactor { get; set; } = 0.20f;
        public float RadialPower { get; set; } = 1.0f;
        public float LongitudinalPower { get; set; } = 0f;
        public float MinKeepThreshold { get; set; } = 0.001f; // USER FIX: Was 0.02f - too aggressive, caused tearing
        public int TopK { get; set; } = 4;
        public float ParentBlend { get; set; } = 0.25f;
        public float AncestorDecay { get; set; } = 0.40f;
        public int SmoothIterations { get; set; } = 30;   // USER FIX: Was 15 - increased for smoother weights
        public float SmoothMu { get; set; } = 0.5f;        // USER FIX: Was 0.45 - more aggressive blending

        // CRITICAL: Region filtering and deformation boundaries to prevent cross-contamination
        public bool UseRegionFiltering { get; set; } = true;  // PHASE 4: Enable by default for natural deformation
        public float MaxInfluenceRadius { get; set; } = 3.0f; // Increased from 2.5x to allow slightly more reach
        private Data.RigTemplate _currentTemplate;

        /// <summary>
        /// Calculates automatic weights for all vertices based on bone distances.
        /// With optional region filtering to prevent cross-contamination.
        /// </summary>
        public void CalculateWeights(
            ObservableCollection<VertexModel> vertices,
            ObservableCollection<JointModel> joints,
            ObservableCollection<TriangleModel> triangles,
            Data.RigTemplate template = null,
            SKBitmap spriteBitmap = null)
        {
            _currentTemplate = template;
            _spriteBitmap = spriteBitmap;
            var bones = BuildBoneSegments(joints);
            
            System.Diagnostics.Debug.WriteLine($"=== AUTO WEIGHT: Bones={bones.Count}, Vertices={vertices.Count} ===");
            
            if (bones.Count == 0) 
            {
                System.Diagnostics.Debug.WriteLine("WARNING: No bones found! Skipping weight calculation.");
                return;
            }

            // Step 1: Calculate raw weights for each vertex
            int vertexIndex = 0;
            foreach (var vertex in vertices)
            {
                var rawWeights = new Dictionary<int, float>();

                // CRITICAL: Determine which bones can affect this vertex (region filtering)
                List<int> allowedBoneIds = null;
                if (UseRegionFiltering && _currentTemplate != null)
                {
                    allowedBoneIds = GetAllowedBonesForVertex(vertex, bones, joints);
                }

                foreach (var bone in bones)
                {
                    // Skip this bone if region filtering is active and bone not allowed
                    if (allowedBoneIds != null && !allowedBoneIds.Contains(bone.ChildId))
                        continue;

                    // Alpha-based separation: Skip if transparent barrier exists between vertex and bone
                    if (_spriteBitmap != null && HasAlphaBarrier(vertex.BindPosition, bone.ParentPos, bone.ChildPos))
                        continue;

                    float t, distance;
                    ProjectToSegment(vertex.BindPosition, bone.ParentPos, bone.ChildPos, out t, out distance);

                    // PHASE 4: Hard limit on influence radius to prevent far-away bones affecting vertices
                    float boneLength = Distance(bone.ParentPos, bone.ChildPos);
                    float maxRadius = boneLength * MaxInfluenceRadius;
                    if (distance > maxRadius)
                        continue; // Skip bones that are too far away

                    // Calculate radial falloff (Gaussian)
                    float sigma = MathF.Max(1e-3f, boneLength * SigmaFactor);
                    float radialFalloff = 1.0f / (1.0f + (distance / sigma) * (distance / sigma));
                    radialFalloff = MathF.Pow(radialFalloff, RadialPower);

                    // Calculate longitudinal falloff (cosine)
                    float longitudinalFalloff = 0.5f * (1f + MathF.Cos(MathF.PI * MathF.Abs(2f * t - 1f)));
                    longitudinalFalloff = MathF.Pow(longitudinalFalloff, LongitudinalPower);

                    float weight = radialFalloff * longitudinalFalloff;
                    
                    // DEBUG: Log first vertex, first bone
                    if (vertexIndex == 0 && rawWeights.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Bone {bone.ChildId}: dist={distance:F1}, boneLen={boneLength:F1}, sigma={sigma:F1}, t={t:F2}");
                        System.Diagnostics.Debug.WriteLine($"    radial={radialFalloff:F3}, longit={longitudinalFalloff:F3}, weight={weight:F3}");
                    }
                    
                    if (weight > 0f)
                    {
                        AddWeight(rawWeights, bone.ChildId, weight);
                    }
                }

                // DEBUG: Log first vertex
                if (vertexIndex == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Vertex 0 raw weights: {rawWeights.Count} bones");
                    foreach (var w in rawWeights.Take(3))
                    {
                        System.Diagnostics.Debug.WriteLine($"  Joint {w.Key}: {w.Value:F3}");
                    }
                }

                // Step 2: Blend with parent/ancestor weights
                if (rawWeights.Count > 0)
                {
                    var blendedWeights = new Dictionary<int, float>(rawWeights);
                    foreach (var (jointId, weight) in rawWeights)
                    {
                        float carry = weight * ParentBlend;
                        var currentJoint = FindJointById(joints, jointId);
                        float factor = 1.0f;

                        while (carry > 1e-5f && currentJoint != null && currentJoint.Parent != null)
                        {
                            currentJoint = currentJoint.Parent;
                            factor *= AncestorDecay;
                            float give = carry * factor;
                            if (give > 1e-5f)
                            {
                                AddWeight(blendedWeights, currentJoint.Id, give);
                            }
                        }
                    }
                    vertex.Weights = blendedWeights;
                }
                else
                {
                    vertex.Weights.Clear();
                }

                // Step 3: Prune and normalize
                PruneAndNormalize(vertex.Weights, TopK, MinKeepThreshold);
                
                // CRITICAL FIX: Ensure vertex has at least ONE weight
                // Prevents orphaned vertices that don't move with ANY bone
                if (vertex.Weights.Count == 0)
                {
                    // Assign to closest bone as fallback
                    int closestBoneId = -1;
                    float closestDist = float.MaxValue;
                    
                    foreach (var bone in bones)
                    {
                        float t, dist;
                        ProjectToSegment(vertex.BindPosition, bone.ParentPos, bone.ChildPos, out t, out dist);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestBoneId = bone.ChildId;
                        }
                    }
                    
                    if (closestBoneId >= 0)
                    {
                        vertex.Weights[closestBoneId] = 1.0f; // 100% weight to closest bone
                        System.Diagnostics.Debug.WriteLine($"WARNING: Vertex {vertexIndex} had no weights. Assigned to bone {closestBoneId} (dist={closestDist:F1})");
                    }
                }
                
                vertexIndex++;
            }

            // Step 4: Smooth weights (optional, requires mesh topology)
            if (triangles.Count > 0 && vertices.Count > 0 && SmoothIterations > 0)
            {
                var neighbors = BuildVertexNeighbors(vertices, triangles);
                for (int iteration = 0; iteration < SmoothIterations; iteration++)
                {
                    SmoothWeightsOnce(vertices, neighbors, SmoothMu, TopK, MinKeepThreshold);
                }
            }
            
            System.Diagnostics.Debug.WriteLine("=== AUTO WEIGHT COMPLETE ===");
        }

        #region Helper Methods

        /// <summary>
        /// Determines which bones can influence a vertex based on template regions.
        /// PHASE 4 FIX: Uses PrimaryJoints for region determination to prevent cross-contamination.
        /// </summary>
        private List<int> GetAllowedBonesForVertex(
            VertexModel vertex,
            List<BoneSegment> bones,
            ObservableCollection<JointModel> joints)
        {
            if (_currentTemplate == null || _currentTemplate.Regions == null)
                return bones.Select(b => b.ChildId).ToList(); // Allow all if no template

            // PHASE 4 BUGFIX: Determine region based on PRIMARY bones only
            // This prevents torso vertices from being classified as arm region
            string vertexRegion = null;
            float closestDist = float.MaxValue;

            foreach (var region in _currentTemplate.Regions)
            {
                // Skip if no primary joints defined (fallback to old behavior)
                if (region.PrimaryJoints == null || region.PrimaryJoints.Count == 0)
                    continue;

                // Find closest bone among this region's PRIMARY joints
                foreach (var bone in bones)
                {
                    var joint = joints.FirstOrDefault(j => j.Id == bone.ChildId);
                    if (joint == null)
                        continue;

                    // Check if this bone is a PRIMARY joint of this region
                    if (!region.PrimaryJoints.Contains(joint.Name))
                        continue;

                    float t, dist;
                    ProjectToSegment(vertex.BindPosition, bone.ParentPos, bone.ChildPos, out t, out dist);

                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        vertexRegion = region.Name;
                    }
                }
            }

            // Fallback: If no region found with primary joints, use old closest bone method
            if (string.IsNullOrEmpty(vertexRegion))
            {
                float fallbackClosestDist = float.MaxValue;
                int closestBoneId = -1;

                foreach (var bone in bones)
                {
                    float t, dist;
                    ProjectToSegment(vertex.BindPosition, bone.ParentPos, bone.ChildPos, out t, out dist);
                    if (dist < fallbackClosestDist)
                    {
                        fallbackClosestDist = dist;
                        closestBoneId = bone.ChildId;
                    }
                }

                var closestJoint = joints.FirstOrDefault(j => j.Id == closestBoneId);
                if (closestJoint != null)
                {
                    var templateJoint = _currentTemplate.Joints.FirstOrDefault(tj => tj.Name == closestJoint.Name);
                    if (templateJoint != null && !string.IsNullOrEmpty(templateJoint.RegionName))
                    {
                        vertexRegion = templateJoint.RegionName;
                    }
                }
            }

            if (string.IsNullOrEmpty(vertexRegion))
                return bones.Select(b => b.ChildId).ToList(); // Final fallback

            // Find region and get allowed joint names
            var targetRegion = _currentTemplate.Regions.FirstOrDefault(r => r.Name == vertexRegion);
            if (targetRegion == null || targetRegion.AllowedJoints == null)
                return bones.Select(b => b.ChildId).ToList();

            // Convert allowed joint names to IDs
            var allowedIds = new List<int>();
            foreach (var allowedName in targetRegion.AllowedJoints)
            {
                var joint = joints.FirstOrDefault(j => j.Name == allowedName);
                if (joint != null)
                {
                    allowedIds.Add(joint.Id);
                }
            }

            return allowedIds;
        }

        /// <summary>
        /// Checks if there's a transparent barrier (alpha==0 pixels) between vertex and bone.
        /// Uses raycast to prevent weight assignment across separated body parts.
        /// </summary>
        private bool HasAlphaBarrier(SKPoint vertexPos, SKPoint boneStart, SKPoint boneEnd)
        {
            if (_spriteBitmap == null)
                return false; // No alpha check if no bitmap provided

            // Find closest point on bone segment to vertex
            float t, dist;
            ProjectToSegment(vertexPos, boneStart, boneEnd, out t, out dist);
            SKPoint bonePoint = new SKPoint(
                boneStart.X + t * (boneEnd.X - boneStart.X),
                boneStart.Y + t * (boneEnd.Y - boneStart.Y)
            );

            // Sample pixels along line between vertex and bone using Bresenham algorithm
            return LineHasTransparentPixels(vertexPos, bonePoint);
        }

        /// <summary>
        /// Bresenham-style line traversal to check for alpha==0 pixels.
        /// Returns true if any transparent pixel found along the path.
        /// </summary>
        private bool LineHasTransparentPixels(SKPoint start, SKPoint end)
        {
            int x0 = (int)start.X;
            int y0 = (int)start.Y;
            int x1 = (int)end.X;
            int y1 = (int)end.Y;

            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // Check if pixel is within bounds
                if (x0 >= 0 && x0 < _spriteBitmap.Width &&
                    y0 >= 0 && y0 < _spriteBitmap.Height)
                {
                    var pixel = _spriteBitmap.GetPixel(x0, y0);
                    if (pixel.Alpha == 0)  // Transparent pixel found - barrier detected
                        return true;
                }

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return false; // No transparent barrier found
        }

        private struct BoneSegment
        {
            public SKPoint ParentPos;
            public SKPoint ChildPos;
            public int ChildId;
        }

        private List<BoneSegment> BuildBoneSegments(ObservableCollection<JointModel> joints)
        {
            var bones = new List<BoneSegment>();
            foreach (var joint in joints)
            {
                if (joint.Parent != null)
                {
                    bones.Add(new BoneSegment
                    {
                        ParentPos = joint.Parent.BindPosition, // CRITICAL: Use BindPosition, not Position!
                        ChildPos = joint.BindPosition,
                        ChildId = joint.Id
                    });
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Built {bones.Count} bone segments");
            if (bones.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"  First bone: Parent=({bones[0].ParentPos.X:F1}, {bones[0].ParentPos.Y:F1}), Child=({bones[0].ChildPos.X:F1}, {bones[0].ChildPos.Y:F1})");
            }
            
            return bones;
        }

        private void ProjectToSegment(SKPoint point, SKPoint segmentStart, SKPoint segmentEnd, out float t, out float distance)
        {
            var segmentVector = segmentEnd - segmentStart;
            float segmentLengthSquared = segmentVector.X * segmentVector.X + segmentVector.Y * segmentVector.Y;

            if (segmentLengthSquared < 1e-6f)
            {
                t = 0f;
                distance = Distance(point, segmentStart);
                return;
            }

            var pointVector = point - segmentStart;
            t = (pointVector.X * segmentVector.X + pointVector.Y * segmentVector.Y) / segmentLengthSquared;
            t = Math.Clamp(t, 0f, 1f);

            var projection = new SKPoint(
                segmentStart.X + t * segmentVector.X,
                segmentStart.Y + t * segmentVector.Y
            );
            distance = Distance(point, projection);
        }

        private float Distance(SKPoint a, SKPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private void AddWeight(Dictionary<int, float> weights, int jointId, float weight)
        {
            if (weights.ContainsKey(jointId))
                weights[jointId] += weight;
            else
                weights[jointId] = weight;
        }

        private JointModel FindJointById(ObservableCollection<JointModel> joints, int id)
        {
            return joints.FirstOrDefault(j => j.Id == id);
        }

        private void PruneAndNormalize(Dictionary<int, float> weights, int topK, float minKeep)
        {
            if (weights.Count == 0) return;

            // Keep only top K weights
            var sorted = weights.OrderByDescending(kv => kv.Value).Take(topK).ToList();
            weights.Clear();
            foreach (var kv in sorted)
            {
                if (kv.Value >= minKeep)
                {
                    weights[kv.Key] = kv.Value;
                }
            }

            // Normalize to sum = 1.0
            float sum = weights.Values.Sum();
            if (sum > 1e-6f)
            {
                var keys = weights.Keys.ToList();
                foreach (var key in keys)
                {
                    weights[key] /= sum;
                }
            }
        }

        private Dictionary<VertexModel, List<VertexModel>> BuildVertexNeighbors(
            ObservableCollection<VertexModel> vertices,
            ObservableCollection<TriangleModel> triangles)
        {
            var neighbors = new Dictionary<VertexModel, List<VertexModel>>();
            foreach (var vertex in vertices)
            {
                neighbors[vertex] = new List<VertexModel>();
            }

            foreach (var triangle in triangles)
            {
                AddNeighbor(neighbors, triangle.V1, triangle.V2);
                AddNeighbor(neighbors, triangle.V1, triangle.V3);
                AddNeighbor(neighbors, triangle.V2, triangle.V3);
            }

            return neighbors;
        }

        private void AddNeighbor(Dictionary<VertexModel, List<VertexModel>> neighbors, VertexModel a, VertexModel b)
        {
            if (neighbors.ContainsKey(a) && !neighbors[a].Contains(b))
                neighbors[a].Add(b);
            if (neighbors.ContainsKey(b) && !neighbors[b].Contains(a))
                neighbors[b].Add(a);
        }

        /// <summary>
        /// Enhanced Laplacian smoothing with edge-aware weights.
        /// Prevents discontinuities that cause mesh tearing.
        /// </summary>
        private void SmoothWeightsOnce(
            ObservableCollection<VertexModel> vertices,
            Dictionary<VertexModel, List<VertexModel>> neighbors,
            float mu,
            int topK,
            float minKeep)
        {
            var newWeights = new Dictionary<VertexModel, Dictionary<int, float>>();

            foreach (var vertex in vertices)
            {
                var smoothed = new Dictionary<int, float>(vertex.Weights);

                if (neighbors.TryGetValue(vertex, out var neighborList) && neighborList.Count > 0)
                {
                    var neighborAverage = new Dictionary<int, float>();
                    float totalEdgeWeight = 0f;
                    
                    // BUGFIX + ENHANCEMENT: Edge-aware smoothing
                    // Weight neighbors by inverse distance (closer = more influence)
                    var vertexJointIds = new HashSet<int>(vertex.Weights.Keys);
                    
                    foreach (var neighbor in neighborList)
                    {
                        // Check if neighbor shares at LEAST one joint with current vertex
                        var neighborJointIds = new HashSet<int>(neighbor.Weights.Keys);
                        bool sameRegion = vertexJointIds.Overlaps(neighborJointIds);
                        
                        if (sameRegion)
                        {
                            // Edge-aware weight: closer neighbors have more influence
                            float dist = Distance(vertex.BindPosition, neighbor.BindPosition);
                            float edgeWeight = 1f / (1f + dist * 0.05f); // Normalize by small factor
                            
                            foreach (var (jointId, weight) in neighbor.Weights)
                            {
                                // Include all neighbor joints (not just shared ones)
                                // This helps propagate weights smoothly
                                if (!neighborAverage.ContainsKey(jointId))
                                    neighborAverage[jointId] = 0f;
                                neighborAverage[jointId] += weight * edgeWeight;
                            }
                            
                            totalEdgeWeight += edgeWeight;
                        }
                    }

                    if (neighborAverage.Count > 0 && totalEdgeWeight > 0.001f)
                    {
                        // Average by total edge weight
                        var keys = neighborAverage.Keys.ToList();
                        foreach (var key in keys)
                        {
                            neighborAverage[key] /= totalEdgeWeight;
                        }

                        // Blend: mu% average, (1-mu)% original
                        var blended = new Dictionary<int, float>();
                        
                        // Combine all joints from both original and neighbor average
                        var allJoints = new HashSet<int>(smoothed.Keys);
                        foreach (var j in neighborAverage.Keys) allJoints.Add(j);
                        
                        foreach (var jointId in allJoints)
                        {
                            float original = smoothed.ContainsKey(jointId) ? smoothed[jointId] : 0f;
                            float avg = neighborAverage.ContainsKey(jointId) ? neighborAverage[jointId] : 0f;
                            blended[jointId] = (1f - mu) * original + mu * avg;
                        }

                        smoothed = blended;
                    }
                }

                PruneAndNormalize(smoothed, topK, minKeep);
                newWeights[vertex] = smoothed;
            }

            // Apply smoothed weights
            foreach (var (vertex, weights) in newWeights)
            {
                vertex.Weights = weights;
            }
        }

        #endregion
    }
}
