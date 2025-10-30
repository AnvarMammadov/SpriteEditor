using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;

namespace SpriteEditor.ViewModels
{
    /// <summary>
    /// Bir sümük (Bone) iki oynaq (Joint) arasında çəkilən xətdir.
    /// Biz sadəlik üçün sümükləri elə oynaqlardan ibarət zəncir kimi saxlayacağıq.
    /// </summary>
    public class JointModel
    {
        public int Id { get; set; }
        public SKPoint Position { get; set; }
        public JointModel Parent { get; set; }  // Hansı oynağa bağlıdır (Root üçün null)

        public JointModel(int id, SKPoint position, JointModel parent = null)
        {
            Id = id;
            Position = position;
            Parent = parent;
        }

    }
}
