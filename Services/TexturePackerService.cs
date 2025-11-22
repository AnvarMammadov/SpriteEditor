using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SpriteEditor.Data;

namespace SpriteEditor.Services
{
    public class TexturePackerService
    {
        /// <summary>
        /// Verilən şəkilləri tək bir atlasda birləşdirir.
        /// </summary>
        public async Task<(byte[] AtlasImage, List<PackedSprite> Map)> PackImagesAsync(
            List<string> filePaths,
            int atlasWidth,
            int atlasHeight,
            int padding)
        {
            return await Task.Run(() =>
            {
                var packedSprites = new List<PackedSprite>();

                // 1. Şəkillərin ölçülərini oxu (Pikselləri yükləmədən, sadəcə metadata)
                var images = new List<(string Path, int W, int H)>();
                foreach (var path in filePaths)
                {
                    using (var info = Image.Load(path))
                    {
                        images.Add((path, info.Width, info.Height));
                    }
                }

                // 2. Hündürlüyə görə sırala (Ən hündürlər əvvəl gəlsin - Shelf alqoritmi üçün vacibdir)
                images = images.OrderByDescending(i => i.H).ToList();

                // 3. Yerləşdirmə Alqoritmi (Simple Shelf)
                int currentX = padding;
                int currentY = padding;
                int rowHeight = 0;

                foreach (var img in images)
                {
                    // Əgər cari sətirə sığmırsa, yeni sətirə keç
                    if (currentX + img.W + padding > atlasWidth)
                    {
                        currentX = padding;
                        currentY += rowHeight + padding;
                        rowHeight = 0; // Yeni sətir hündürlüyü sıfırlanır
                    }

                    // Əgər hündürlük atlası aşırsa -> Xəta və ya Atlası böyütmək lazımdır
                    if (currentY + img.H + padding > atlasHeight)
                    {
                        // Resursdan oxuyur: "Str_Msg_AtlasFull"
                        throw new Exception(App.GetStr("Str_Msg_AtlasFull"));
                    }

                    // Koordinatları yadda saxla
                    packedSprites.Add(new PackedSprite
                    {
                        Name = Path.GetFileNameWithoutExtension(img.Path),
                        FilePath = img.Path,
                        X = currentX,
                        Y = currentY,
                        Width = img.W,
                        Height = img.H
                    });

                    // Növbəti şəkil üçün X-i sürüşdür
                    currentX += img.W + padding;

                    // Sətrin hündürlüyünü ən hündür şəklə görə yenilə
                    if (img.H > rowHeight) rowHeight = img.H;
                }

                // 4. Atlası Çək (ImageSharp ilə)
                // Boş kətan yaradırıq
                using (var atlas = new Image<Rgba32>(atlasWidth, atlasHeight))
                {
                    // Arxa fonu şəffaf et
                    atlas.Mutate(x => x.BackgroundColor(Color.Transparent));

                    foreach (var sprite in packedSprites)
                    {
                        using (var spriteImg = Image.Load(sprite.FilePath))
                        {
                            // Koordinatlara görə kətanın üzərinə çək
                            atlas.Mutate(ctx => ctx.DrawImage(spriteImg, new Point(sprite.X, sprite.Y), 1f));
                        }
                    }

                    // Nəticəni byte massivinə çevir
                    using (var ms = new MemoryStream())
                    {
                        atlas.SaveAsPng(ms);
                        return (ms.ToArray(), packedSprites);
                    }
                }
            });
        }
    }
}
