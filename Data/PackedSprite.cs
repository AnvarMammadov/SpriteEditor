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
    }
}
