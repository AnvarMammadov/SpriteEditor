using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpriteEditor.Data
{
    public class PackedSprite
    {
        public string Name { get; set; }      // Fayl adı (məs: "player_run_01")
        public string FilePath { get; set; }  // Tam yol
        public int X { get; set; }            // Atlasdakı X
        public int Y { get; set; }            // Atlasdakı Y
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsRotated { get; set; }   // Gələcəkdə yerə qənaət üçün fırlatmaq istəsək

        public bool IsTrimmed { get; set; }   // Kəsilibmi?
        public int OriginalWidth { get; set; } // Orijinal En
        public int OriginalHeight { get; set; } // Orijinal Hündürlük
        public int OffsetX { get; set; }      // Soldan nə qədər kəsilib?
        public int OffsetY { get; set; }      // Yuxarıdan nə qədər kəsilib?
    }
}
