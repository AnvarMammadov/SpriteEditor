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
        /// Bir spritesheet-i verilən sətir və sütunlara görə kəsir və qovluğa yazır.
        /// </summary>
        /// <param name="imagePath">Orijinal faylın yolu</param>
        /// <param name="columns">Sütun sayı</param>
        /// <param name="rows">Sətr sayı</param>
        /// <param name="outputDirectory">Kəsilən spritların saxlanacağı qovluq</param>
        public void SliceSpriteSheet(string imagePath, int columns, int rows, string outputDirectory)
        {
            // 1. Orijinal şəkli ImageSharp ilə yükləyirik
            using (Image sourceImage = Image.Load(imagePath))
            {
                // 2. Hər bir spritın enini və hündürlüyünü hesablayırıq
                int cellWidth = sourceImage.Width / columns;
                int cellHeight = sourceImage.Height / rows;

                // 3. Hər bir sətir (row) və sütun (column) üzrə döngü (loop)
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        // 4. Kəsiləcək spritın koordinatlarını təyin edirik
                        var cropRectangle = new Rectangle(
                            x * cellWidth,   // Başlanğıc X
                            y * cellHeight,  // Başlanğıc Y
                            cellWidth,       // En
                            cellHeight       // Hündürlük
                        );

                        // 5. Şəklin həmin hissəsini klonlayırıq (kəsirik)
                        // 'Clone' orijinal şəkli dəyişməmək üçün vacibdir
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
