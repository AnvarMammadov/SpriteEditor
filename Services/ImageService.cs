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
        /// Şəkildən verilmiş rəngi və ona yaxın rəngləri şəffaf edir.
        /// (Daha dəqiq Euclidean alqoritmi ilə)
        /// </summary>
        public byte[] RemoveBackground(string imagePath, Rgba32 targetColor, float tolerancePercent)
        {
            // 1. Həssaslığı (tolerance) 0-100% aralığından 0-442 aralığına çeviririk
            // RGB rənglər arasındakı maksimum "Euclidean" məsafə: sqrt(255^2 + 255^2 + 255^2) ≈ 441.67
            float maxDistance = (float)Math.Sqrt(Math.Pow(255, 2) * 3);
            float toleranceDistance = maxDistance * (tolerancePercent / 100f);

            using (Image<Rgba32> image = Image.Load<Rgba32>(imagePath))
            {
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                        foreach (ref Rgba32 pixel in pixelRow)
                        {
                            // 2. Rənglər arasındakı "Euclidean" məsafəni hesablayırıq
                            // Bu, rəng oxşarlığını daha dəqiq tapır
                            double distance = Math.Sqrt(
                                Math.Pow(pixel.R - targetColor.R, 2) +
                                Math.Pow(pixel.G - targetColor.G, 2) +
                                Math.Pow(pixel.B - targetColor.B, 2)
                            );

                            // 3. Əgər məsafə bizim həssaslıq dərəcəmizdən azdırsa...
                            if (distance <= toleranceDistance)
                            {
                                // 4. Və piksel onsuz da şəffaf deyilsə, onu şəffaf et
                                if (pixel.A > 0)
                                {
                                    pixel.A = 0;
                                }
                            }
                        }
                    }
                });

                // 5. Nəticəni yaddaşa yaz
                using (var ms = new MemoryStream())
                {
                    image.Save(ms, new PngEncoder());
                    return ms.ToArray();
                }
            }



        }


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
    }
}
