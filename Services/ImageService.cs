using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
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
    }
}
