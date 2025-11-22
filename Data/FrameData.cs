using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SpriteEditor.Data
{
    public class FrameData
    {
        public string FilePath { get; set; }
        public BitmapImage ImageSource { get; set; }
        public string Name { get; set; } // Fayl adı (məs: Run_01)
    }
}
