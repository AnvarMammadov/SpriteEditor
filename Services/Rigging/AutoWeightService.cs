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
        // Auto-weight parameters (will be configurable)
        public float SigmaFactor { get; set; } = 0.20f;
        public float RadialPower { get; set; } = 1.0f;
        public float LongitudinalPower { get; set; } = 0f; // CRITICAL: Was 0.5f, but that zeros weights at bone endpoints!
        public float MinKeepThreshold { get; set; } = 0.02f;
        public int TopK { get; set; } = 4;
        public float ParentBlend { get; set; } = 0.25f;
        public float AncestorDecay { get; set; } = 0.40f;
        public int SmoothIterations { get; set; } = 3;
        public float SmoothMu { get; set; } = 0.30f;

        // CRITICAL: Region filtering to prevent cross-contamination
        public bool UseRegionFiltering { get; set; } = false; // TEMP: Disabled for debug
        private Data.RigTemplate _currentTemplate;

        /// <summary>
        /// Calculates automatic weights for all vertices based on bone distances.
        /// With optional region filtering to prevent cross-contamination.
        /// </summary>
        public void CalculateWeights(
            ObservableCollection<VertexModel> vertices,
            ObservableCollection<JointModel> joints,
            ObservableCollection<TriangleModel> triangles,
            Data.RigTemplate template = null)
        {
            _currentTemplate = template;
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

                    float t, distance;
                    ProjectToSegment(vertex.BindPosition, bone.ParentPos, bone.ChildPos, out t, out distance);

                    // Calculate radial falloff (Gaussian)
                    float boneLength = Distance(bone.ParentPos, bone.ChildPos);
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
        /// Prevents cross-contamination (e.g., left leg won't affect right leg).
        /// </summary>
        private List<int> GetAllowedBonesForVertex(
            VertexModel vertex,
            List<BoneSegment> bones,
            ObservableCollection<JointModel> joints)
        {
            if (_currentTemplate == null || _currentTemplate.Regions == null)
                return bones.Select(b => b.ChildId).ToList(); // Allow all if no template

            // Find closest bone to determine vertex's region
            float closestDist = float.MaxValue;
            int closestBoneId = -1;

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

            // Find closest joint's region in template
            var closestJoint = joints.FirstOrDefault(j => j.Id == closestBoneId);
            if (closestJoint == null)
                return bones.Select(b => b.ChildId).ToList();

            string closestJointName = closestJoint.Name;
            var templateJoint = _currentTemplate.Joints.FirstOrDefault(tj => tj.Name == closestJointName);
            if (templateJoint == null || string.IsNullOrEmpty(templateJoint.RegionName))
                return bones.Select(b => b.ChildId).ToList();

            // Find region and get allowed joint names
            var region = _currentTemplate.Regions.FirstOrDefault(r => r.Name == templateJoint.RegionName);
            if (region == null || region.AllowedJoints == null)
                return bones.Select(b => b.ChildId).ToList();

            // Convert allowed joint names to IDs
            var allowedIds = new List<int>();
            foreach (var allowedName in region.AllowedJoints)
            {
                var joint = joints.FirstOrDefault(j => j.Name == allowedName);
                if (joint != null)
                {
                    allowedIds.Add(joint.Id);
                }
            }

            return allowedIds;
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
                    
                    // BUGFIX: Only average weights from neighbors in the SAME REGION
                    // This prevents cross-contamination during smoothing
                    var vertexJointIds = new HashSet<int>(vertex.Weights.Keys);
                    
                    foreach (var neighbor in neighborList)
                    {
                        // Check if neighbor shares at LEAST one joint with current vertex
                        // This indicates they're in the same region
                        var neighborJointIds = new HashSet<int>(neighbor.Weights.Keys);
                        bool sameRegion = vertexJointIds.Overlaps(neighborJointIds);
                        
                        if (sameRegion)
                        {
                            foreach (var (jointId, weight) in neighbor.Weights)
                            {
                                // Only include joints that are already in vertex's weights
                                // This prevents new bones from "bleeding in" during smoothing
                                if (vertexJointIds.Contains(jointId))
                                {
                                    AddWeight(neighborAverage, jointId, weight);
                                }
                            }
                        }
                    }

                    if (neighborAverage.Count > 0)
                    {
                        // Average (count only neighbors that contributed)
                        int contributingNeighbors = neighborList.Count(n => 
                            vertexJointIds.Overlaps(new HashSet<int>(n.Weights.Keys)));
                        
                        if (contributingNeighbors > 0)
                        {
                            var keys = neighborAverage.Keys.ToList();
                            foreach (var key in keys)
                            {
                                neighborAverage[key] /= contributingNeighbors;
                            }

                            // Blend (only with joints already in smoothed weights)
                            var blended = new Dictionary<int, float>();
                            foreach (var jointId in smoothed.Keys)
                            {
                                float original = smoothed[jointId];
                                float avg = neighborAverage.ContainsKey(jointId) ? neighborAverage[jointId] : original;
                                blended[jointId] = (1f - mu) * original + mu * avg;
                            }

                            smoothed = blended;
                        }
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
