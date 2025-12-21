using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows; // Int32Rect üçün (WPF)
using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing; // Fill metodu üçün vacibdir
using SixLabors.ImageSharp.Drawing; // Path və Polygon üçün
using SpriteEditor.Data;
using Path = System.IO.Path;

namespace SpriteEditor.Services
{
    public class ImageService
    {
        // 1. SPRITE SHEET KƏSİMİ (GRID İLƏ)
        public void SliceSpriteSheet(string imagePath, int columns, int rows,
                                     int cropX, int cropY, int cropWidth, int cropHeight,
                                     string outputDirectory)
        {
            using (Image sourceImage = Image.Load(imagePath))
            {
                string sanitizedBaseFileName = System.IO.Path.GetFileNameWithoutExtension(imagePath);
                foreach (char c in Path.GetInvalidFileNameChars())
                    sanitizedBaseFileName = sanitizedBaseFileName.Replace(c, '_');

                int cellWidth = cropWidth / columns;
                int cellHeight = cropHeight / rows;

                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        var cropRectangle = new Rectangle(
                            cropX + (x * cellWidth),
                            cropY + (y * cellHeight),
                            cellWidth,
                            cellHeight
                        );

                        using (Image sprite = sourceImage.Clone(ctx => ctx.Crop(cropRectangle)))
                        {
                            string outputFileName = $"{sanitizedBaseFileName}_{y}_{x}.png";
                            string outputPath = Path.Combine(outputDirectory, outputFileName);
                            sprite.Save(outputPath, new PngEncoder());
                        }
                    }
                }
            }
        }

        // 2. POLİQON (PEN TOOL) KƏSİMİ
        public void SlicePolygons(string sourcePath, IEnumerable<SlicePart> parts, string outputDir)
        {
            // Bu metod üçün 'SixLabors.ImageSharp.Drawing' NuGet paketi mütləq yüklənməlidir!
            using (var sourceImage = Image.Load<Rgba32>(sourcePath))
            {
                foreach (var part in parts)
                {
                    if (part.Points.Count < 3) continue;

                    // A. Bounding Box tapılması
                    var minX = (int)part.Points.Min(p => p.X);
                    var minY = (int)part.Points.Min(p => p.Y);
                    var maxX = (int)part.Points.Max(p => p.X);
                    var maxY = (int)part.Points.Max(p => p.Y);
                    var width = maxX - minX;
                    var height = maxY - minY;

                    if (width <= 0 || height <= 0) continue;

                    // B. Poliqon nöqtələrini lokal (0,0) koordinata çevirmək
                    var localPoints = part.Points.Select(p => new PointF((float)(p.X - minX), (float)(p.Y - minY))).ToArray();

                    // C. Şəffaf kətan yaradılması
                    using (var resultImage = new Image<Rgba32>(width, height))
                    {
                        // D. Orijinal şəkildən müvafiq kvadrat hissəni kəsirik
                        var cropRect = new Rectangle(minX, minY, width, height);
                        var croppedSection = sourceImage.Clone(ctx => ctx.Crop(cropRect));

                        // E. MASKALAMA (Əsas düzəliş buradadır)
                        // 'SixLabors.ImageSharp.Drawing.Processing.ImageBrush' tam adını yazırıq ki, WPF ilə qarışmasın
                        var brush = new SixLabors.ImageSharp.Drawing.Processing.ImageBrush(croppedSection);

                        var polygonOptions = new DrawingOptions
                        {
                            GraphicsOptions = new GraphicsOptions { Antialias = true }
                        };

                        // Poliqon fiqurunu yaradırıq
                        var polygon = new Polygon(new LinearLineSegment(localPoints));

                        // Şəkli (brush) poliqonun formasına (polygon) uyğun olaraq 'resultImage'-ə çəkirik (Fill)
                        resultImage.Mutate(x => x.Fill(polygonOptions, brush, polygon));

                        // F. Yaddaşa yaz
                        string safeName = string.Join("_", part.Name.Split(Path.GetInvalidFileNameChars()));
                        resultImage.Save(Path.Combine(outputDir, $"{safeName}.png"));
                    }
                }
            }
        }

        // 3. AVTO TƏYİN (AUTO DETECT)
        public List<Int32Rect> DetectSprites(string imagePath, byte alphaThreshold = 10)
        {
            var detectedRects = new List<Int32Rect>();
            using (Image<Rgba32> image = Image.Load<Rgba32>(imagePath))
            {
                int width = image.Width;
                int height = image.Height;
                bool[,] visited = new bool[width, height];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!visited[x, y] && image[x, y].A > alphaThreshold)
                        {
                            var rect = FindBoundingBox(image, visited, x, y, alphaThreshold);
                            detectedRects.Add(rect);
                        }
                    }
                }
            }
            return detectedRects;
        }

        private Int32Rect FindBoundingBox(Image<Rgba32> image, bool[,] visited, int startX, int startY, byte alphaThreshold)
        {
            int minX = startX, minY = startY;
            int maxX = startX, maxY = startY;

            Queue<System.Drawing.Point> queue = new Queue<System.Drawing.Point>();
            queue.Enqueue(new System.Drawing.Point(startX, startY));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();

                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;

                int[] dx = { 1, -1, 0, 0 };
                int[] dy = { 0, 0, 1, -1 };

                for (int i = 0; i < 4; i++)
                {
                    int nx = p.X + dx[i];
                    int ny = p.Y + dy[i];

                    if (nx >= 0 && nx < image.Width && ny >= 0 && ny < image.Height)
                    {
                        if (!visited[nx, ny] && image[nx, ny].A > alphaThreshold)
                        {
                            visited[nx, ny] = true;
                            queue.Enqueue(new System.Drawing.Point(nx, ny));
                        }
                    }
                }
            }
            return new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        // 4. RECT SİYAHISI İLƏ KƏSİM
        public void SliceByRects(string imagePath, List<Int32Rect> rects, string outputDirectory)
        {
            using (Image sourceImage = Image.Load(imagePath))
            {
                string sanitizedBaseFileName = Path.GetFileNameWithoutExtension(imagePath);
                for (int i = 0; i < rects.Count; i++)
                {
                    var r = rects[i];
                    var cropRectangle = new Rectangle(r.X, r.Y, r.Width, r.Height);
                    using (Image sprite = sourceImage.Clone(ctx => ctx.Crop(cropRectangle)))
                    {
                        string outputFileName = $"{sanitizedBaseFileName}_sprite_{i}.png";
                        string outputPath = Path.Combine(outputDirectory, outputFileName);
                        sprite.Save(outputPath, new PngEncoder());
                    }
                }
            }
        }

        // 5. GIF YARATMA (Unudulmuş metod)
        public void CreateGifFromImages(List<string> imagePaths, int delayMs, string outputPath)
        {
            using (var collection = new MagickImageCollection())
            {
                foreach (var path in imagePaths)
                {
                    var img = new MagickImage(path);
                    // Magick.NET-də AnimationDelay 1/100 saniyə vahidi ilə ölçülür.
                    img.AnimationDelay = (uint)delayMs / 10;
                    img.GifDisposeMethod = GifDisposeMethod.Background;
                    collection.Add(img);
                }

                collection.Quantize(new QuantizeSettings { Colors = 256 });
                collection.Optimize();
                collection.Write(outputPath);
            }
        }

        // 6. FORMAT DƏYİŞMƏ
        public void ConvertImageFormat(string inputPath, string outputPath)
        {
            string extension = Path.GetExtension(inputPath).ToLower();
            if (extension == ".avif" || extension == ".heic" || extension == ".webp" || extension == ".tiff" || extension == ".tif")
            {
                try
                {
                    using (var magickImage = new MagickImage(inputPath))
                    {
                        if (Path.GetExtension(outputPath).ToLower() == ".ico" && (magickImage.Width > 256 || magickImage.Height > 256))
                            magickImage.Resize(256, 256);

                        magickImage.Write(outputPath);
                    }
                    return;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Magick.NET Error: {ex.Message}"); }
            }

            using (Image image = Image.Load(inputPath))
            {
                image.Save(outputPath);
            }
        }
    }
}