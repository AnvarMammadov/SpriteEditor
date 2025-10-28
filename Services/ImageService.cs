using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                            string outputFileName = $"sprite_{y}_{x}.png";
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
            using (Image<Rgba32> image = Image.Load<Rgba32>(imagePath))
            {
                // 1. Başlanğıc rəngi (hədəf rəng) kliklənən nöqtədən götürürük
                Rgba32 targetColor = image[startX, startY];

                // 2. Həssaslıq dərəcəsini hesablayırıq (Euclidean)
                float maxDistance = (float)Math.Sqrt(Math.Pow(255, 2) * 3);
                float toleranceDistance = maxDistance * (tolerancePercent / 100f);

                // 3. Əməliyyat üçün strukturlar yaradırıq
                var pixelsToProcess = new Queue<Point>(); // Yoxlanılacaq piksellər
                var visitedPixels = new HashSet<Point>(); // Artıq yoxlanılmışlar (sonsuz döngü üçün)

                // 4. Başlanğıc nöqtəni əlavə edirik
                pixelsToProcess.Enqueue(new Point(startX, startY));

                // 5. "Sel" (Flood) başlayır
                while (pixelsToProcess.Count > 0)
                {
                    Point currentPoint = pixelsToProcess.Dequeue();
                    int x = currentPoint.X;
                    int y = currentPoint.Y;

                    // A. Sərhədləri yoxla
                    if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
                        continue;

                    // B. Artıq yoxlanılıbsa, davam etmə
                    if (visitedPixels.Contains(currentPoint))
                        continue;

                    // C. Yoxlanıldı olaraq işarələ
                    visitedPixels.Add(currentPoint);

                    // D. Hazırkı pikselin rəngini al
                    Rgba32 currentColor = image[x, y];

                    // E. Rəng fərqini hesabla
                    double distance = Math.Sqrt(
                        Math.Pow(currentColor.R - targetColor.R, 2) +
                        Math.Pow(currentColor.G - targetColor.G, 2) +
                        Math.Pow(currentColor.B - targetColor.B, 2)
                    );

                    // F. Əgər rəng həssaslıq daxilindədirsə (fona aiddirsə)...
                    if (distance <= toleranceDistance)
                    {
                        // Rəngi şəffaf et
                        // (Struct olduğu üçün birbaşa .A = 0 etmək əvəzinə yenidən təyin edirik)
                        image[x, y] = new Rgba32(currentColor.R, currentColor.G, currentColor.B, 0);

                        // Qonşuları yoxlama siyahısına əlavə et
                        pixelsToProcess.Enqueue(new Point(x + 1, y)); // Sağ
                        pixelsToProcess.Enqueue(new Point(x - 1, y)); // Sol
                        pixelsToProcess.Enqueue(new Point(x, y + 1)); // Aşağı
                        pixelsToProcess.Enqueue(new Point(x, y - 1)); // Yuxarı
                    }
                    // Əgər rəng fərqlidirsə (məs. personajın köynəyi), heçnə etmirik
                    // və "sel" burada dayanır.
                }

                // 6. Nəticəni yaddaşa yaz
                using (var ms = new MemoryStream())
                {
                    image.Save(ms, new PngEncoder());
                    return ms.ToArray();
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
