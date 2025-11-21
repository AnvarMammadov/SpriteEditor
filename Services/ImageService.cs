using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SpriteEditor.Services
{
    public class ImageService
    {
        /// <summary>
        /// Bir spritesheet-i verilən sətir, sütun və kəsim sahəsinə (crop area) görə kəsir.
        /// </summary>
        /// <param name="imagePath">Orijinal faylın yolu</param>
        /// <param name="columns">Sütun sayı</param>
        /// <param name="rows">Sətr sayı</param>
        /// <param name="cropX">Kəsim sahəsinin başlanğıc X koordinatı</param>
        /// <param name="cropY">Kəsim sahəsinin başlanğıc Y koordinatı</param>
        /// <param name="cropWidth">Kəsim sahəsinin eni</param>
        /// <param name="cropHeight">Kəsim sahəsinin hündürlüyü</param>
        /// <param name="outputDirectory">Kəsilən spritların saxlanacağı qovluq</param>
        public void SliceSpriteSheet(string imagePath, int columns, int rows,
                                     int cropX, int cropY, int cropWidth, int cropHeight,
                                     string outputDirectory)
        {
            // 1. Orijinal şəkli ImageSharp ilə yükləyirik
            using (Image sourceImage = Image.Load(imagePath))
            {
                // === YENİ DÜZƏLİŞ: Fayl adını təmizləyək ===
                string sanitizedBaseFileName = Path.GetFileNameWithoutExtension(imagePath);
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    // Qadağan olunmuş simvolları (məsələn, ':', '/', '?') alt xətlə əvəz edirik
                    sanitizedBaseFileName = sanitizedBaseFileName.Replace(c, '_');
                }
                // ==========================================
                // 2. Hər bir spritın enini və hündürlüyünü Slicer Box-a görə hesablayırıq
                int cellWidth = cropWidth / columns;
                int cellHeight = cropHeight / rows;

                // 3. Hər bir sətir (row) və sütun (column) üzrə döngü (loop)
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        // 4. Kəsiləcək spritın koordinatlarını Slicer Box-a görə təyin edirik
                        var cropRectangle = new Rectangle(
                            cropX + (x * cellWidth),   // Başlanğıc X (SlicerX + (sütun * hücrə eni))
                            cropY + (y * cellHeight),  // Başlanğıc Y (SlicerY + (sətr * hücrə hündürlüyü))
                            cellWidth,                 // En
                            cellHeight                 // Hündürlük
                        );

                        // 5. Şəklin həmin hissəsini klonlayırıq (kəsirik)
                        using (Image sprite = sourceImage.Clone(ctx => ctx.Crop(cropRectangle)))
                        {
                            // 6. Yeni faylın adını yaradırıq (məs: sprite_sətir_1_sütun_0.png)
                            string outputFileName = $"{sanitizedBaseFileName}_{y}_{x}.png";
                            string outputPath = Path.Combine(outputDirectory, outputFileName);

                            // 7. Kəsilən spritı PNG formatında yaddaşa yazırıq
                            sprite.Save(outputPath, new PngEncoder());
                        }
                    }
                }
            }
        }





        /// <summary>
        /// "Flood Fill" (Magic Wand) alqoritmi ilə arxa fonu şəffaf edir.
        /// </summary>
        /// <param name="imagePath">Şəklin yolu</param>
        /// <param name="startX">Kliklənən pikselin X koordinatı</param>
        /// <param name="startY">Kliklənən pikselin Y koordinatı</param>
        /// <param name="tolerancePercent">Həssaslıq (0-100)</param>
        /// <returns>Nəticənin PNG byte massivi</returns>
        public byte[] RemoveBackground(string imagePath, int startX, int startY, float tolerancePercent)
        {
            // Orijinal şəkli yükləyirik (tipini Rgba32 məcbur etmədən)
            using (Image originalImage = Image.Load(imagePath))
            {
                // === YENİ DÜZƏLİŞ: ===
                // 1. Tamamilə yeni, boş və şəffaf bir Rgba32 kətan (canvas) yaradırıq
                using (Image<Rgba32> image = new Image<Rgba32>(originalImage.Width, originalImage.Height))
                {
                    // 2. Orijinal şəkli bu yeni kətanın üzərinə çəkirik
                    image.Mutate(ctx => ctx.DrawImage(originalImage, 1f));

                    // Artıq 100% əminik ki, "image" dəyişdirilə bilən Rgba32 formatındadır

                    // 3. Başlanğıc rəngi bu yeni kətandan götürürük
                    Rgba32 targetColor = image[startX, startY];

                    // 4. Həssaslıq
                    float maxDistance = (float)Math.Sqrt(Math.Pow(255, 2) * 3);
                    float toleranceDistance = maxDistance * (tolerancePercent / 100f);

                    // 5. Strukturlar
                    var pixelsToProcess = new Queue<SixLabors.ImageSharp.Point>();
                    var visitedPixels = new HashSet<SixLabors.ImageSharp.Point>();

                    // 6. Başlanğıc nöqtə
                    pixelsToProcess.Enqueue(new SixLabors.ImageSharp.Point(startX, startY));

                    // 7. "Sel" (Flood)
                    while (pixelsToProcess.Count > 0)
                    {
                        SixLabors.ImageSharp.Point currentPoint = pixelsToProcess.Dequeue();
                        int x = currentPoint.X;
                        int y = currentPoint.Y;

                        // A. Sərhəd yoxlaması
                        if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
                            continue;

                        // B. Ziyarət yoxlaması
                        if (visitedPixels.Contains(currentPoint))
                            continue;

                        visitedPixels.Add(currentPoint);

                        // D. Rəng al
                        Rgba32 currentColor = image[x, y];

                        // E. Fərqi hesabla
                        double distance = Math.Sqrt(
                            Math.Pow(currentColor.R - targetColor.R, 2) +
                            Math.Pow(currentColor.G - targetColor.G, 2) +
                            Math.Pow(currentColor.B - targetColor.B, 2)
                        );

                        // F. Əgər rəng həssaslıq daxilindədirsə...
                        if (distance <= toleranceDistance)
                        {
                            // Rəngi şəffaf et
                            image[x, y] = new Rgba32(currentColor.R, currentColor.G, currentColor.B, 0);

                            // Qonşuları əlavə et
                            pixelsToProcess.Enqueue(new SixLabors.ImageSharp.Point(x + 1, y));
                            pixelsToProcess.Enqueue(new SixLabors.ImageSharp.Point(x - 1, y));
                            pixelsToProcess.Enqueue(new SixLabors.ImageSharp.Point(x, y + 1));
                            pixelsToProcess.Enqueue(new SixLabors.ImageSharp.Point(x, y - 1));
                        }
                    }

                    // 8. Nəticəni yaddaşa yaz
                    using (var ms = new MemoryStream())
                    {
                        image.Save(ms, new PngEncoder());
                        return ms.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// "Flood Fill" (Magic Wand) alqoritmi ilə arxa fonu şəffaf edir (Byte massivindən).
        /// </summary>
        /// <param name="imageData">Şəklin byte massivi</param>
        /// <param name="startX">Kliklənən pikselin X koordinatı</param>
        /// <param name="startY">Kliklənən pikselin Y koordinatı</param>
        /// <param name="tolerancePercent">Həssaslıq (0-100)</param>
        /// <returns>Nəticənin PNG byte massivi</returns>
        public byte[] RemoveBackground(byte[] imageData, int startX, int startY, float tolerancePercent)
        {
            // Orijinal şəkli fayldan yox, MemoryStream-dən yükləyirik
            using (Image originalImage = Image.Load(new MemoryStream(imageData)))
            {
                // === YENİ DÜZƏLİŞ: ===
                // 1. Tamamilə yeni, boş və şəffaf bir Rgba32 kətan (canvas) yaradırıq
                using (Image<Rgba32> image = new Image<Rgba32>(originalImage.Width, originalImage.Height))
                {
                    // 2. Orijinal şəkli bu yeni kətanın üzərinə çəkirik
                    image.Mutate(ctx => ctx.DrawImage(originalImage, 1f));

                    // Artıq 100% əminik ki, "image" dəyişdirilə bilən Rgba32 formatındadır

                    // 3. Başlanğıc rəngi bu yeni kətandan götürürük
                    Rgba32 targetColor = image[startX, startY];

                    // 4. Həssaslıq
                    float maxDistance = (float)Math.Sqrt(Math.Pow(255, 2) * 3);
                    float toleranceDistance = maxDistance * (tolerancePercent / 100f);

                    // 5. Strukturlar
                    var pixelsToProcess = new Queue<SixLabors.ImageSharp.Point>();
                    var visitedPixels = new HashSet<SixLabors.ImageSharp.Point>();

                    // 6. Başlanğıc nöqtə
                    pixelsToProcess.Enqueue(new SixLabors.ImageSharp.Point(startX, startY));

                    // 7. "Sel" (Flood)
                    while (pixelsToProcess.Count > 0)
                    {
                        SixLabors.ImageSharp.Point currentPoint = pixelsToProcess.Dequeue();
                        int x = currentPoint.X;
                        int y = currentPoint.Y;

                        // A. Sərhəd yoxlaması
                        if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
                            continue;

                        // B. Ziyarət yoxlaması
                        if (visitedPixels.Contains(currentPoint))
                            continue;

                        visitedPixels.Add(currentPoint);

                        // D. Rəng al
                        Rgba32 currentColor = image[x, y];

                        // E. Fərqi hesabla
                        double distance = Math.Sqrt(
                            Math.Pow(currentColor.R - targetColor.R, 2) +
                            Math.Pow(currentColor.G - targetColor.G, 2) +
                            Math.Pow(currentColor.B - targetColor.B, 2)
                        );

                        // F. Əgər rəng həssaslıq daxilindədirsə...
                        if (distance <= toleranceDistance)
                        {
                            // Rəngi şəffaf et
                            image[x, y] = new Rgba32(currentColor.R, currentColor.G, currentColor.B, 0);

                            // Qonşuları əlavə et
                            pixelsToProcess.Enqueue(new SixLabors.ImageSharp.Point(x + 1, y));
                            pixelsToProcess.Enqueue(new SixLabors.ImageSharp.Point(x - 1, y));
                            pixelsToProcess.Enqueue(new SixLabors.ImageSharp.Point(x, y + 1));
                            pixelsToProcess.Enqueue(new SixLabors.ImageSharp.Point(x, y - 1));
                        }
                    }

                    // 8. Nəticəni yaddaşa yaz
                    using (var ms = new MemoryStream())
                    {
                        image.Save(ms, new PngEncoder());
                        return ms.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Şəkli yükləyir və verilən yeni fayl yoluna (yeni formatla) yadda saxlayır.
        /// ImageSharp uzantıya (.png, .jpg, .ico) baxaraq formatı avtomatik seçir.
        /// </summary>
        public void ConvertImageFormat(string inputPath, string outputPath)
        {
            using (Image image = Image.Load(inputPath))
            {
                // ICO üçün xüsusi hal: Ölçüləri standartlaşdırmaq məsləhətdir (məs: 256x256)
                // Amma sadəlik üçün birbaşa saxlayırıq, ImageSharp bunu idarə etməyə çalışacaq.
                image.Save(outputPath);
            }
        }

        /// <summary>
        /// Şəkil üzərindəki ayrı-ayrı obyektləri (adaları) avtomatik tapır.
        /// </summary>
        public List<Int32Rect> DetectSprites(string imagePath, byte alphaThreshold = 10)
        {
            var detectedRects = new List<Int32Rect>();

            using (Image<Rgba32> image = Image.Load<Rgba32>(imagePath))
            {
                int width = image.Width;
                int height = image.Height;
                bool[,] visited = new bool[width, height];

                // Pikselləri gəzirik
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Əgər piksel görünürdürsə (Alpha > threshold) və hələ yoxlanmayıbsa
                        if (!visited[x, y] && image[x, y].A > alphaThreshold)
                        {
                            // Yeni bir "Ada" tapdıq, onun sərhədlərini hesablayaq (BFS Alqoritmi)
                            var rect = FindBoundingBox(image, visited, x, y, alphaThreshold);
                            detectedRects.Add(rect);
                        }
                    }
                }
            }

            return detectedRects;
        }

        // Flood Fill (BFS) ilə adanın sərhədlərini tapır
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

                // 4 qonşu pikselə baxırıq
                int[] dx = { 1, -1, 0, 0 };
                int[] dy = { 0, 0, 1, -1 };

                for (int i = 0; i < 4; i++)
                {
                    int nx = p.X + dx[i];
                    int ny = p.Y + dy[i];

                    // Sərhəd daxilindədirmi?
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


        /// <summary>
        /// Grid yerinə, verilmiş xüsusi koordinatlara (Rect) əsasən kəsim edir.
        /// </summary>
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



        #region Diagnostika Testi
        /// <summary>
        /// === DİAQNOSTİKA TESTİ #2 ===
        /// ImageSharp-ın "Mutate" API-ı ilə şəffaflığı dəyişməyə çalışır.
        /// </summary>
        //public byte[] RemoveBackground(string imagePath, Rgba32 targetColor, float tolerancePercent)
        //{
        //    // Şəkli Rgba32 formatında (Alfa kanalı ilə) yükləyirik
        //    using (Image<Rgba32> image = Image.Load<Rgba32>(imagePath))
        //    {
        //        // Pikselləri tək-tək emal etmək əvəzinə,
        //        // yüksək səviyyəli "Mutate" əməliyyatını yoxlayırıq.
        //        image.Mutate(ctx =>
        //        {
        //            // Bütün şəklin şəffaflığını 50%-ə endir
        //            ctx.Opacity(0.5f);
        //        });

        //        // Nəticəni PNG olaraq yaddaşa yazırıq
        //        using (var ms = new MemoryStream())
        //        {
        //            image.Save(ms, new PngEncoder());
        //            return ms.ToArray();
        //        }
        //    }
        //}
        #endregion
    }
}
