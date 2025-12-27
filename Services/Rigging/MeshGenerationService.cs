using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SkiaSharp;
using SpriteEditor.ViewModels;
using TriangleNet.Geometry;
using TriangleNet.Meshing;

namespace SpriteEditor.Services.Rigging
{
    /// <summary>
    /// Service responsible for mesh generation (vertices and triangulation).
    /// Extracted from RiggingViewModel to separate concerns.
    /// </summary>
    public class MeshGenerationService
    {
        private const float POISSON_RADIUS = 20f;
        private const int POISSON_ATTEMPTS = 30;
        private const float EDGE_LENGTH_CAP = 150f;

        /// <summary>
        /// Automatically generates vertices from sprite image alpha channel.
        /// Uses Poisson disk sampling for interior points and alpha boundary detection.
        /// </summary>
        public List<VertexModel> GenerateVerticesFromImage(SKBitmap bitmap, int startId = 0)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            var vertices = new List<VertexModel>();
            int currentId = startId;

            // Step 1: Extract alpha boundary (outer contour)
            var boundaryPoints = ExtractAlphaBoundary(bitmap);
            if (boundaryPoints.Count < 3)
            {
                throw new InvalidOperationException("Not enough boundary points found. Ensure sprite has transparent background.");
            }

            // Step 2: Add boundary points as vertices
            foreach (var point in boundaryPoints)
            {
                vertices.Add(new VertexModel(currentId++, point));
            }

            // Step 3: Generate interior points using Poisson disk sampling
            var interiorPoints = PoissonDiskSampling(bitmap, boundaryPoints, POISSON_RADIUS, POISSON_ATTEMPTS);
            foreach (var point in interiorPoints)
            {
                vertices.Add(new VertexModel(currentId++, point));
            }

            return vertices;
        }

        /// <summary>
        /// Triangulates vertices using Delaunay triangulation.
        /// </summary>
        public List<TriangleModel> TriangulateVertices(List<VertexModel> vertices)
        {
            if (vertices == null || vertices.Count < 3)
                return new List<TriangleModel>();

            var triangles = new List<TriangleModel>();
            var polygon = new Polygon();

            // Add all vertices to polygon
            var idToVertex = vertices.ToDictionary(v => v.Id, v => v);
            foreach (var vertex in vertices)
            {
                polygon.Add(new Vertex(vertex.BindPosition.X, vertex.BindPosition.Y) { ID = vertex.Id });
            }

            // Triangulate
            var mesh = (TriangleNet.Mesh)polygon.Triangulate();

            // Convert Triangle.NET output to our models
            foreach (var tri in mesh.Triangles)
            {
                int id0 = tri.GetVertexID(0);
                int id1 = tri.GetVertexID(1);
                int id2 = tri.GetVertexID(2);

                if (idToVertex.TryGetValue(id0, out var v0) &&
                    idToVertex.TryGetValue(id1, out var v1) &&
                    idToVertex.TryGetValue(id2, out var v2))
                {
                    // Filter out degenerate or overly large triangles
                    if (!HasEdgeLongerThan(v0, v1, v2, EDGE_LENGTH_CAP))
                    {
                        triangles.Add(new TriangleModel(v0, v1, v2));
                    }
                }
            }

            return triangles;
        }

        #region Helper Methods

        /// <summary>
        /// Extracts boundary points from sprite alpha channel.
        /// </summary>
        private List<SKPoint> ExtractAlphaBoundary(SKBitmap bitmap)
        {
            var boundary = new List<SKPoint>();
            int width = bitmap.Width;
            int height = bitmap.Height;

            // Simple edge detection: scan perimeter
            // Top edge
            for (int x = 0; x < width; x += 5) // Sample every 5 pixels
            {
                for (int y = 0; y < height; y++)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.Alpha > 128) // Opaque enough
                    {
                        boundary.Add(new SKPoint(x, y));
                        break;
                    }
                }
            }

            // Right edge
            for (int y = 0; y < height; y += 5)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.Alpha > 128)
                    {
                        boundary.Add(new SKPoint(x, y));
                        break;
                    }
                }
            }

            // Bottom edge
            for (int x = width - 1; x >= 0; x -= 5)
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.Alpha > 128)
                    {
                        boundary.Add(new SKPoint(x, y));
                        break;
                    }
                }
            }

            // Left edge
            for (int y = height - 1; y >= 0; y -= 5)
            {
                for (int x = 0; x < width; x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.Alpha > 128)
                    {
                        boundary.Add(new SKPoint(x, y));
                        break;
                    }
                }
            }

            // Remove duplicates
            return boundary.Distinct().ToList();
        }

        /// <summary>
        /// Poisson disk sampling for evenly distributed interior points.
        /// </summary>
        private List<SKPoint> PoissonDiskSampling(SKBitmap bitmap, List<SKPoint> boundary, float radius, int attempts)
        {
            var points = new List<SKPoint>();
            var active = new List<SKPoint>();
            var random = new Random();

            // Start with a random point inside sprite bounds
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

                    // Check if point is inside bounds and not too close to existing points
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

            if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
                return false;

            var color = bitmap.GetPixel(x, y);
            return color.Alpha > 128; // Point is on opaque pixel
        }

        private bool IsTooClose(List<SKPoint> points, SKPoint newPoint, float minDistance)
        {
            foreach (var point in points)
            {
                float dist = Distance(point, newPoint);
                if (dist < minDistance)
                    return true;
            }
            return false;
        }

        private float Distance(SKPoint a, SKPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private bool HasEdgeLongerThan(VertexModel a, VertexModel b, VertexModel c, float cap)
        {
            float dAB = Distance(a.BindPosition, b.BindPosition);
            float dBC = Distance(b.BindPosition, c.BindPosition);
            float dCA = Distance(c.BindPosition, a.BindPosition);
            return (dAB > cap) || (dBC > cap) || (dCA > cap);
        }

        #endregion
    }
}
