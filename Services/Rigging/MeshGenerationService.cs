using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using SpriteEditor.ViewModels;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Topology;

namespace SpriteEditor.Services.Rigging
{
    /// <summary>
    /// Service responsible for mesh generation (vertices and triangulation).
    /// Uses Constrained Delaunay Triangulation (CDT) to ensure the mesh follows the sprite contour.
    /// </summary>
    public class MeshGenerationService
    {
        private const float POISSON_RADIUS = 30f; // Smaller radius for better detail
        private const int POISSON_ATTEMPTS = 30;
        private const float SIMPLIFICATION_EPSILON = 2.0f; // RDP epsilon (pixels)

        /// <summary>
        /// Generates a mesh (vertices and triangles) that fits the sprite contour.
        /// </summary>
        public (List<VertexModel> Vertices, List<TriangleModel> Triangles) GenerateMesh(SKBitmap bitmap, int startId = 0)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            var vertices = new List<VertexModel>();
            var triangles = new List<TriangleModel>();
            int currentId = startId;

            // Step 1: Trace Contours (Islands)
            var contours = TraceContours(bitmap);
            if (contours.Count == 0)
                return (vertices, triangles); // Empty image

            // Step 2: Create TriangleNet Polygon
            var polygon = new Polygon();
            var accumulatedPoints = new List<SKPoint>();
            var boundaryPoints = new HashSet<SKPoint>();

            foreach (var contour in contours)
            {
                // Simplify contour
                var simplified = SimplifyContour(contour, SIMPLIFICATION_EPSILON);
                if (simplified.Count < 3) continue;

                // Add to polygon as segment loop
                var contourVertices = new List<Vertex>();
                foreach (var p in simplified)
                {
                    var v = new Vertex(p.X, p.Y);
                    contourVertices.Add(v);
                    polygon.Add(v);
                    accumulatedPoints.Add(p);
                    boundaryPoints.Add(p);
                }

                // Add segments
                for (int i = 0; i < contourVertices.Count; i++)
                {
                    var v1 = contourVertices[i];
                    var v2 = contourVertices[(i + 1) % contourVertices.Count];
                    polygon.Add(new Segment(v1, v2)); // Constraint
                }
            }

            // Step 3: Generate Interior Points
            // We only generate points that are INSIDE the contours
            var interiorPoints = PoissonDiskSampling(bitmap, boundaryPoints, POISSON_RADIUS, POISSON_ATTEMPTS);
            foreach (var p in interiorPoints)
            {
                // Only add if not too close to boundary (Poisson handles this self-distance, but we check against boundary too)
                 // And strictly inside
                if (IsPointInsideAnyContour(p, contours)) 
                {
                     polygon.Add(new Vertex(p.X, p.Y));
                     accumulatedPoints.Add(p);
                }
            }

            // Step 4: Constrained Triangulation
            var options = new ConstraintOptions { ConformingDelaunay = true }; // Ensure quality
            var mesh = (TriangleNet.Mesh)polygon.Triangulate(options);

            // Step 5: Convert back to Models
            var vertexMap = new Dictionary<int, VertexModel>(); // TriangleNet ID -> VertexModel

            // Create VertexModels
            foreach (var v in mesh.Vertices)
            {
                var vm = new VertexModel(currentId++, new SKPoint((float)v.X, (float)v.Y));
                vertices.Add(vm);
                vertexMap[v.ID] = vm;
            }

            // Create TriangleModels
            foreach (var t in mesh.Triangles)
            {
                // Get pre-created VertexModels
                // Note: TriangleNet topology uses integer IDs mapped to our VertexModels
                if (vertexMap.TryGetValue(t.GetVertexID(0), out var v0) &&
                    vertexMap.TryGetValue(t.GetVertexID(1), out var v1) &&
                    vertexMap.TryGetValue(t.GetVertexID(2), out var v2))
                {
                    // Check if triangle centroid is inside the contour (to filter out "webbing" in holes)
                    var centroid = new SKPoint(
                        (v0.BindPosition.X + v1.BindPosition.X + v2.BindPosition.X) / 3f,
                        (v0.BindPosition.Y + v1.BindPosition.Y + v2.BindPosition.Y) / 3f
                    );

                    if (IsPointInsideAnyContour(centroid, contours))
                    {
                        triangles.Add(new TriangleModel(v0, v1, v2));
                    }
                }
            }

            return (vertices, triangles);
        }

        #region Legacy Methods (Forwarders)
        
        public List<VertexModel> GenerateVerticesFromImage(SKBitmap bitmap, int startId = 0)
        {
            var (verts, tris) = GenerateMesh(bitmap, startId);
            return verts;
        }

        public List<TriangleModel> TriangulateVertices(List<VertexModel> vertices)
        {
             return new List<TriangleModel>();
        }

        #endregion

        #region Contour Tracing

        private List<List<SKPoint>> TraceContours(SKBitmap bitmap)
        {
            var contours = new List<List<SKPoint>>();
            var visited = new bool[bitmap.Width, bitmap.Height];
            int width = bitmap.Width;
            int height = bitmap.Height;

            // Access pixels directly via byte array for performance if possible, 
            // but GetPixel is safe. For optimization, we rely on the algorithm change.

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Optimization: Skip known transparent or visited
                    // Note: We don't skip by 5 anymore because we need to find the exact top-left edge for Moore
                    if (visited[x, y]) continue;
                    
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.Alpha > 20)
                    {
                        // Found a new island!
                        
                        // 1. Trace the boundary using Moore-Neighbor algorithm (Ordered & Fast)
                        var contour = TraceBoundaryMoore(bitmap, x, y);
                        
                        // 2. Simplify immediately (optimization)
                        if (contour.Count > 3)
                        {
                            contours.Add(contour);
                        }

                        // 3. Mark the ENTIRE island as visited using Flood Fill
                        // This prevents re-detecting the same island or its internal pixels
                        FloodFillMarkVisited(bitmap, visited, x, y);
                    }
                }
            }
            return contours;
        }

        /// <summary>
        /// Traces the boundary of an island using Moore-Neighbor Tracing.
        /// Returns an ordered list of vertices.
        /// </summary>
        private List<SKPoint> TraceBoundaryMoore(SKBitmap bitmap, int startX, int startY)
        {
            var points = new List<SKPoint>();
            
            // Moore-Neighbor Tracing
            // Start with a known boundary pixel (startX, startY)
            // We need to enter from a "background" pixel. Since we scan Left->Right, Top->Bottom,
            // (startX-1, startY) is definitely background or visited.
            
            int width = bitmap.Width;
            int height = bitmap.Height;
            
            var B = new SKPoint(startX, startY);
            var C = new SKPoint(startX - 1, startY); // Backtrack pointer
            
            var start = B;
            points.Add(start);
            
            // Limit iterations to prevent infinite loops in degenerate cases
            int maxIter = width * height; 
            int iter = 0;

            while (iter < maxIter)
            {
                // Inspect 8 neighbors of B, starting from C, in clockwise direction
                // Find the first non-transparent pixel
                var nextOffset = FindNextBoundaryPixel(bitmap, (int)B.X, (int)B.Y, (int)C.X, (int)C.Y);
                
                if (nextOffset == null)
                {
                    // Isolated pixel
                    break;
                }
                
                var nextP = nextOffset.Value.Pixel;
                var backtrack = nextOffset.Value.Backtrack;

                // Stop if we closed the loop
                if (nextP == start && points.Count > 1)
                {
                    break;
                }

                points.Add(nextP);
                B = nextP;
                C = backtrack;
                iter++;
            }

            return points;
        }

        private (SKPoint Pixel, SKPoint Backtrack)? FindNextBoundaryPixel(SKBitmap bitmap, int bx, int by, int cx, int cy)
        {
            // 8 neighbors offsets (Clockwise)
            // We need to find the index of C relative to B
            int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
            int[] dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

            // Find starting index from C
            int startIndex = -1;
            for (int i = 0; i < 8; i++)
            {
                if (bx + dx[i] == cx && by + dy[i] == cy)
                {
                    startIndex = i;
                    break;
                }
            }

            if (startIndex == -1) startIndex = 0; // Fallback

            // Check neighbors in clockwise order
            for (int i = 0; i < 8; i++)
            {
                int idx = (startIndex + 1 + i) % 8; // Start from next clockwise
                int nx = bx + dx[idx];
                int ny = by + dy[idx];

                if (nx >= 0 && nx < bitmap.Width && ny >= 0 && ny < bitmap.Height)
                {
                    if (bitmap.GetPixel(nx, ny).Alpha > 20)
                    {
                        // Found next boundary pixel!
                        // The backtrack pixel for the next step is the one immediately preceding this one (counter-clockwise)
                        int backIdx = (idx + 4 + 1) % 8; // Heuristic: previous valid background
                        // Actually Moore algorithm sets C to the current neighbor checked immediately before the found one.
                        // The one *before* idx was idx-1.
                        int prevIdx = (idx - 1 + 8) % 8;
                        
                        return (new SKPoint(nx, ny), new SKPoint(bx + dx[prevIdx], by + dy[prevIdx]));
                    }
                }
            }

            return null; // Isolated
        }

        private void FloodFillMarkVisited(SKBitmap bitmap, bool[,] visited, int startX, int startY)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            
            var stack = new Stack<SKPoint>();
            stack.Push(new SKPoint(startX, startY));
            
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                int x = (int)p.X;
                int y = (int)p.Y;

                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                if (visited[x, y]) continue;
                
                visited[x, y] = true;

                // 4-connected fill sufficient for marking
                // Optimized: check bounds and alpha inline
                if (x + 1 < w && !visited[x + 1, y] && bitmap.GetPixel(x + 1, y).Alpha > 20) stack.Push(new SKPoint(x + 1, y));
                if (x - 1 >= 0 && !visited[x - 1, y] && bitmap.GetPixel(x - 1, y).Alpha > 20) stack.Push(new SKPoint(x - 1, y));
                if (y + 1 < h && !visited[x, y + 1] && bitmap.GetPixel(x, y + 1).Alpha > 20) stack.Push(new SKPoint(x, y + 1));
                if (y - 1 >= 0 && !visited[x, y - 1] && bitmap.GetPixel(x, y - 1).Alpha > 20) stack.Push(new SKPoint(x, y - 1));
            }
        }

        #endregion

        #region Simplification (RDP)

        private List<SKPoint> SimplifyContour(List<SKPoint> points, float epsilon)
        {
             if (points.Count < 3) return points;
             
             int index = 0;
             float dmax = 0;
             int last = points.Count - 1;
             
             for (int i = 1; i < last; i++)
             {
                 float d = PerpendicularDistance(points[i], points[0], points[last]);
                 if (d > dmax)
                 {
                     index = i;
                     dmax = d;
                 }
             }
             
             List<SKPoint> result;
             if (dmax > epsilon)
             {
                 var rec1 = SimplifyContour(points.GetRange(0, index + 1), epsilon);
                 var rec2 = SimplifyContour(points.GetRange(index, last - index + 1), epsilon);
                 
                 result = new List<SKPoint>(rec1);
                 result.RemoveAt(result.Count - 1); 
                 result.AddRange(rec2);
             }
             else
             {
                 result = new List<SKPoint> { points[0], points[last] };
             }
             return result;
        }

        private float PerpendicularDistance(SKPoint pt, SKPoint lineStart, SKPoint lineEnd)
        {
             float dx = lineEnd.X - lineStart.X;
             float dy = lineEnd.Y - lineStart.Y;
             float mag = (float)Math.Sqrt(dx * dx + dy * dy);
             if (mag < 0.0001f) return Distance(pt, lineStart);
             
             float u = ((pt.X - lineStart.X) * dx + (pt.Y - lineStart.Y) * dy) / (mag * mag);
             
             SKPoint intersection;
             if (u < 0 || u > 1) 
                 intersection = (Distance(pt, lineStart) < Distance(pt, lineEnd)) ? lineStart : lineEnd;
             else
                 intersection = new SKPoint(lineStart.X + u * dx, lineStart.Y + u * dy);
                 
             return Distance(pt, intersection);
        }

        #endregion

        #region Helpers

        private List<SKPoint> PoissonDiskSampling(SKBitmap bitmap, HashSet<SKPoint> boundary, float radius, int attempts)
        {
            var points = new List<SKPoint>();
            var active = new List<SKPoint>();
            var random = new Random();

            var firstPoint = new SKPoint(bitmap.Width / 2f, bitmap.Height / 2f);
            points.Add(firstPoint);
            active.Add(firstPoint);

            while (active.Count > 0)
            {
                int index = random.Next(active.Count);
                var point = active[index];
                bool found = false;

                for (int i = 0; i < attempts; i++)
                {
                    float angle = (float)(random.NextDouble() * Math.PI * 2);
                    float distance = radius + (float)(random.NextDouble() * radius);
                    var newPoint = new SKPoint(
                        point.X + distance * MathF.Cos(angle),
                        point.Y + distance * MathF.Sin(angle)
                    );

                    if (IsInsideSprite(bitmap, newPoint) && !IsTooClose(points, newPoint, radius))
                    {
                        points.Add(newPoint);
                        active.Add(newPoint);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    active.RemoveAt(index);
                }
            }

            return points;
        }

        private bool IsInsideSprite(SKBitmap bitmap, SKPoint point)
        {
            int x = (int)point.X;
            int y = (int)point.Y;
            if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height) return false;
            return bitmap.GetPixel(x, y).Alpha > 20;
        }

        private bool IsTooClose(List<SKPoint> points, SKPoint newPoint, float minDistance)
        {
             foreach (var p in points)
             {
                 if (Distance(p, newPoint) < minDistance) return true;
             }
             return false;
        }

        private bool IsPointInsideAnyContour(SKPoint p, List<List<SKPoint>> contours)
        {
             foreach (var c in contours)
             {
                 if (IsPointInPolygon(p, c)) return true;
             }
             return false;
        }

        private bool IsPointInPolygon(SKPoint p, List<SKPoint> polygon)
        {
             bool inside = false;
             int j = polygon.Count - 1;
             for (int i = 0; i < polygon.Count; j = i++)
             {
                 if (((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y)) &&
                     (p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                 {
                     inside = !inside;
                 }
             }
             return inside;
        }
        


        private float Distance(SKPoint a, SKPoint b)
        {
            return MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }

        #endregion
    }
}
