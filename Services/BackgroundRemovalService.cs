using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SpriteEditor.Services
{
    /// <summary>
    /// Professional Background Removal Service with advanced algorithms
    /// </summary>
    public class BackgroundRemovalService
    {
        // ====================================================================
        // REMOVAL METHODS ENUM
        // ====================================================================

        public enum RemovalMethod
        {
            FloodFill,      // Classic magic wand
            ColorRange,     // Remove all similar colors
            ChromaKey,      // Green/Blue screen removal
            EdgeDetection   // Smart edge-based removal
        }

        // ====================================================================
        // 1. FLOOD FILL METHOD (Enhanced with feathering)
        // ====================================================================

        public byte[] RemoveBackgroundFloodFill(byte[] imageData, int startX, int startY, float tolerance, int featherRadius = 0)
        {
            using (Image<Rgba32> image = Image.Load<Rgba32>(imageData))
            {
                Rgba32 targetColor = image[startX, startY];
                float maxDistance = (float)Math.Sqrt(Math.Pow(255, 2) * 3);
                float toleranceDistance = maxDistance * (tolerance / 100f);

                // Create mask
                bool[,] mask = new bool[image.Width, image.Height];
                var pixelsToProcess = new Queue<Point>();
                var visitedPixels = new HashSet<Point>();

                pixelsToProcess.Enqueue(new Point(startX, startY));

                // Flood fill algorithm
                while (pixelsToProcess.Count > 0)
                {
                    Point currentPoint = pixelsToProcess.Dequeue();
                    int x = currentPoint.X;
                    int y = currentPoint.Y;

                    if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
                        continue;

                    if (visitedPixels.Contains(currentPoint))
                        continue;

                    visitedPixels.Add(currentPoint);

                    Rgba32 currentColor = image[x, y];
                    double distance = ColorDistance(currentColor, targetColor);

                    if (distance <= toleranceDistance)
                    {
                        mask[x, y] = true;

                        // Add neighbors
                        pixelsToProcess.Enqueue(new Point(x + 1, y));
                        pixelsToProcess.Enqueue(new Point(x - 1, y));
                        pixelsToProcess.Enqueue(new Point(x, y + 1));
                        pixelsToProcess.Enqueue(new Point(x, y - 1));
                    }
                }

                // Apply mask with optional feathering
                if (featherRadius > 0)
                {
                    ApplyFeathering(image, mask, featherRadius);
                }
                else
                {
                    ApplyMask(image, mask);
                }

                return SaveToBytes(image);
            }
        }

        // ====================================================================
        // 2. COLOR RANGE METHOD (Remove all similar colors globally)
        // ====================================================================

        public byte[] RemoveBackgroundColorRange(byte[] imageData, Rgba32 targetColor, float tolerance)
        {
            using (Image<Rgba32> image = Image.Load<Rgba32>(imageData))
            {
                float maxDistance = (float)Math.Sqrt(Math.Pow(255, 2) * 3);
                float toleranceDistance = maxDistance * (tolerance / 100f);

                // Process all pixels
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                        for (int x = 0; x < pixelRow.Length; x++)
                        {
                            double distance = ColorDistance(pixelRow[x], targetColor);
                            if (distance <= toleranceDistance)
                            {
                                pixelRow[x] = new Rgba32(pixelRow[x].R, pixelRow[x].G, pixelRow[x].B, 0);
                            }
                        }
                    }
                });

                return SaveToBytes(image);
            }
        }

        // ====================================================================
        // 3. CHROMA KEY METHOD (Optimized for green/blue screens)
        // ====================================================================

        public byte[] RemoveBackgroundChromaKey(byte[] imageData, ChromaKeyColor chromaColor, float tolerance, float spillSuppression = 0.5f)
        {
            using (Image<Rgba32> image = Image.Load<Rgba32>(imageData))
            {
                Rgba32 keyColor = chromaColor switch
                {
                    ChromaKeyColor.Green => new Rgba32(0, 255, 0),
                    ChromaKeyColor.Blue => new Rgba32(0, 0, 255),
                    ChromaKeyColor.Red => new Rgba32(255, 0, 0),
                    _ => new Rgba32(0, 255, 0)
                };

                float maxDistance = (float)Math.Sqrt(Math.Pow(255, 2) * 3);
                float toleranceDistance = maxDistance * (tolerance / 100f);

                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                        for (int x = 0; x < pixelRow.Length; x++)
                        {
                            double distance = ColorDistance(pixelRow[x], keyColor);
                            if (distance <= toleranceDistance)
                            {
                                // Calculate alpha based on distance (soft edges)
                                byte alpha = (byte)Math.Max(0, Math.Min(255, 255 * (distance / toleranceDistance)));
                                
                                // Apply spill suppression
                                if (spillSuppression > 0 && alpha > 0)
                                {
                                    pixelRow[x] = SuppressSpill(pixelRow[x], keyColor, spillSuppression);
                                }
                                
                                pixelRow[x] = new Rgba32(pixelRow[x].R, pixelRow[x].G, pixelRow[x].B, alpha);
                            }
                        }
                    }
                });

                return SaveToBytes(image);
            }
        }

        // ====================================================================
        // 4. EDGE REFINEMENT (Smooth and feather edges)
        // ====================================================================

        public byte[] RefineEdges(byte[] imageData, int smoothRadius, int featherRadius)
        {
            using (Image<Rgba32> image = Image.Load<Rgba32>(imageData))
            {
                // Create alpha mask
                bool[,] mask = new bool[image.Width, image.Height];
                
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        mask[x, y] = image[x, y].A > 0;
                    }
                }

                // Apply smoothing
                if (smoothRadius > 0)
                {
                    mask = SmoothMask(mask, image.Width, image.Height, smoothRadius);
                }

                // Apply feathering
                if (featherRadius > 0)
                {
                    ApplyFeathering(image, mask, featherRadius);
                }

                return SaveToBytes(image);
            }
        }

        // ====================================================================
        // 5. INVERT SELECTION
        // ====================================================================

        public byte[] InvertSelection(byte[] imageData)
        {
            using (Image<Rgba32> image = Image.Load<Rgba32>(imageData))
            {
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                        for (int x = 0; x < pixelRow.Length; x++)
                        {
                            pixelRow[x] = new Rgba32(pixelRow[x].R, pixelRow[x].G, pixelRow[x].B, (byte)(255 - pixelRow[x].A));
                        }
                    }
                });

                return SaveToBytes(image);
            }
        }

        // ====================================================================
        // HELPER METHODS
        // ====================================================================

        private double ColorDistance(Rgba32 color1, Rgba32 color2)
        {
            return Math.Sqrt(
                Math.Pow(color1.R - color2.R, 2) +
                Math.Pow(color1.G - color2.G, 2) +
                Math.Pow(color1.B - color2.B, 2)
            );
        }

        private void ApplyMask(Image<Rgba32> image, bool[,] mask)
        {
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (mask[x, y])
                    {
                        var pixel = image[x, y];
                        image[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, 0);
                    }
                }
            }
        }

        private void ApplyFeathering(Image<Rgba32> image, bool[,] mask, int featherRadius)
        {
            int width = image.Width;
            int height = image.Height;

            // Calculate distance transform
            float[,] distanceMap = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y])
                    {
                        // Find distance to nearest non-masked pixel
                        float minDist = featherRadius + 1;
                        for (int dy = -featherRadius; dy <= featherRadius; dy++)
                        {
                            for (int dx = -featherRadius; dx <= featherRadius; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height && !mask[nx, ny])
                                {
                                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                                    minDist = Math.Min(minDist, dist);
                                }
                            }
                        }
                        distanceMap[x, y] = minDist;
                    }
                    else
                    {
                        distanceMap[x, y] = 0;
                    }
                }
            }

            // Apply feathering based on distance
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y])
                    {
                        float dist = distanceMap[x, y];
                        byte alpha = (byte)(255 * Math.Min(1.0f, dist / featherRadius));
                        var pixel = image[x, y];
                        image[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, alpha);
                    }
                }
            }
        }

        private bool[,] SmoothMask(bool[,] mask, int width, int height, int radius)
        {
            bool[,] smoothedMask = new bool[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int count = 0;
                    int total = 0;

                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                if (mask[nx, ny]) count++;
                                total++;
                            }
                        }
                    }

                    smoothedMask[x, y] = (count > total / 2);
                }
            }

            return smoothedMask;
        }

        private Rgba32 SuppressSpill(Rgba32 pixel, Rgba32 keyColor, float amount)
        {
            // Reduce the key color channel
            byte r = pixel.R;
            byte g = pixel.G;
            byte b = pixel.B;

            if (keyColor.G > keyColor.R && keyColor.G > keyColor.B) // Green
            {
                g = (byte)Math.Max(0, g - (int)(g * amount));
            }
            else if (keyColor.B > keyColor.R && keyColor.B > keyColor.G) // Blue
            {
                b = (byte)Math.Max(0, b - (int)(b * amount));
            }
            else if (keyColor.R > keyColor.G && keyColor.R > keyColor.B) // Red
            {
                r = (byte)Math.Max(0, r - (int)(r * amount));
            }

            return new Rgba32(r, g, b, pixel.A);
        }

        private byte[] SaveToBytes(Image<Rgba32> image)
        {
            using (var ms = new MemoryStream())
            {
                image.SaveAsPng(ms);
                return ms.ToArray();
            }
        }
    }

    // ====================================================================
    // ENUMS
    // ====================================================================

    public enum ChromaKeyColor
    {
        Green,
        Blue,
        Red
    }
}

