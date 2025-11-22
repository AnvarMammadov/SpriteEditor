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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SpriteEditor.Data;

namespace SpriteEditor.Services
{
    public class TexturePackerService
    {
        public async Task<(byte[] AtlasImage, List<PackedSprite> Map)> PackImagesAsync(
            List<string> filePaths,
            int atlasWidth,
            int atlasHeight,
            int padding,
            bool trimTransparency) // <--- YENİ PARAMETR
        {
            return await Task.Run(() =>
            {
                var packedSprites = new List<PackedSprite>();

                // 1. Şəkilləri yüklə və emal et
                // Tuple: (Image obyekt, Orijinal ölçülər, Kəsilmiş ölçülər, Offsetlər)
                var processedImages = new List<(Image<Rgba32> Img, PackedSprite Info)>();

                foreach (var path in filePaths)
                {
                    var img = Image.Load<Rgba32>(path);
                    var spriteInfo = new PackedSprite
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        FilePath = path,
                        OriginalWidth = img.Width,
                        OriginalHeight = img.Height,
                        IsTrimmed = trimTransparency
                    };

                    if (trimTransparency)
                    {
                        // Sərhədləri tap
                        var bounds = GetBoundingBox(img);

                        // Şəffaf boşluqları kəs (Crop)
                        if (bounds.Width > 0 && bounds.Height > 0)
                        {
                            img.Mutate(x => x.Crop(bounds));

                            // Yeni ölçüləri və offseti qeyd et
                            spriteInfo.Width = bounds.Width;
                            spriteInfo.Height = bounds.Height;
                            spriteInfo.OffsetX = bounds.X;
                            spriteInfo.OffsetY = bounds.Y;
                        }
                        else
                        {
                            // Tamamilə şəffaf şəkildirsə (boşdursa)
                            spriteInfo.Width = 1;
                            spriteInfo.Height = 1;
                            spriteInfo.OffsetX = 0;
                            spriteInfo.OffsetY = 0;
                            img.Mutate(x => x.Resize(1, 1));
                        }
                    }
                    else
                    {
                        // Trim yoxdursa, olduğu kimi saxla
                        spriteInfo.Width = img.Width;
                        spriteInfo.Height = img.Height;
                        spriteInfo.OffsetX = 0;
                        spriteInfo.OffsetY = 0;
                    }

                    processedImages.Add((img, spriteInfo));
                }

                // 2. Sıralama (Shelf alqoritmi üçün hündürlüyə görə)
                // Kəsilmiş (kiçilmiş) hündürlüyə görə sıralayırıq
                processedImages = processedImages.OrderByDescending(i => i.Info.Height).ToList();

                // 3. Yerləşdirmə (Shelf Algorithm)
                int currentX = padding;
                int currentY = padding;
                int rowHeight = 0;

                // Atlas kətanı
                using (var atlas = new Image<Rgba32>(atlasWidth, atlasHeight))
                {
                    atlas.Mutate(x => x.BackgroundColor(Color.Transparent));

                    foreach (var item in processedImages)
                    {
                        var img = item.Img;
                        var info = item.Info;

                        // Sətirdə yer yoxdursa, aşağı düş
                        if (currentX + info.Width + padding > atlasWidth)
                        {
                            currentX = padding;
                            currentY += rowHeight + padding;
                            rowHeight = 0;
                        }

                        // Atlasda yer yoxdursa xəta
                        if (currentY + info.Height + padding > atlasHeight)
                        {
                            // Yaddaşa qənaət üçün açılmış şəkilləri bağlayaq
                            foreach (var p in processedImages) p.Img.Dispose();
                            throw new Exception(App.GetStr("Str_Msg_AtlasFull"));
                        }

                        // Koordinatları yaz
                        info.X = currentX;
                        info.Y = currentY;
                        packedSprites.Add(info);

                        // Şəkli atlasa çək
                        atlas.Mutate(ctx => ctx.DrawImage(img, new Point(info.X, info.Y), 1f));

                        // Növbəti mövqe
                        currentX += info.Width + padding;
                        if (info.Height > rowHeight) rowHeight = info.Height;

                        // Emal olunmuş şəkli yaddaşdan sil (Atlasa köçürüldü)
                        img.Dispose();
                    }

                    // 4. Yekun nəticə
                    using (var ms = new MemoryStream())
                    {
                        atlas.SaveAsPng(ms);
                        return (ms.ToArray(), packedSprites);
                    }
                }
            });
        }

        // Köməkçi Metod: Şəffaf olmayan ən kiçik çərçivəni tapır
        private Rectangle GetBoundingBox(Image<Rgba32> image)
        {
            int minX = image.Width;
            int minY = image.Height;
            int maxX = 0;
            int maxY = 0;
            bool hasPixels = false;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        // Əgər piksel tam şəffaf deyilsə (Alpha > 0)
                        if (row[x].A > 0)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            hasPixels = true;
                        }
                    }
                }
            });

            if (!hasPixels) return new Rectangle(0, 0, 0, 0);

            return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
