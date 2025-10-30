using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

namespace SpriteEditor.ViewModels
{
    /// <summary>
    /// Bir sümük (Bone) iki oynaq (Joint) arasında çəkilən xətdir.
    /// Biz sadəlik üçün sümükləri elə oynaqlardan ibarət zəncir kimi saxlayacağıq.
    /// </summary>

    // === DƏYİŞİKLİK (PLAN 2): ObservableObject-dən miras alırıq ===
    public partial class JointModel : ObservableObject
    {
        public int Id { get; set; }
        public SKPoint Position { get; set; }
        public JointModel Parent { get; set; }  // Hansı oynağa bağlıdır (Root üçün null)


        /// <summary>
        /// Bu oynağı valideyninə bağlayan sümüyün uzunluğu.
        /// Valideyn yoxdursa (root), bu dəyər 0 ola bilər.
        /// </summary>
        public float BoneLength { get; set; }

        /// <summary>
        /// Sümüyün mütləq (dünya) fırlanma bucağı (radianda).
        /// </summary>
        public float Rotation { get; set; }

        // === YENİ XASSƏ (PLAN 2) ===
        [ObservableProperty]
        private string _name;
        // ===========================

        public JointModel(int id, SKPoint position, JointModel parent = null)
        {
            Id = id;
            Position = position;
            Parent = parent;

            BoneLength = 0;
            Rotation = 0;
            // Başlanğıcda adı ID-yə görə təyin edək
            _name = $"Joint_{id}";
        }

        // Adı TextBox-da göstərmək üçün (istəyə bağlı, amma faydalıdır)
        public override string ToString()
        {
            return Name;
        }
    }
}
