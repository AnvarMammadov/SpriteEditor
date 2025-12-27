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

        /// <summary>
        /// Oynaq yaradılanda (bind olunan anda) olan mövqeyi.
        /// Skinning hesablamaları üçün lazımdır.
        /// </summary>
        public SKPoint BindPosition { get; set; }

        /// <summary>
        /// Oynaq yaradılanda (bind olunan anda) olan fırlanma bucağı.
        /// Skinning hesablamaları üçün lazımdır.
        /// </summary>
        public float BindRotation { get; set; }

        // === PHYSICS PROPERTIES (for ragdoll simulation) ===
        public SKPoint PreviousPosition { get; set; }  // For Verlet integration
        public float Mass { get; set; } = 1.0f;
        public bool IsAnchored { get; set; } = false;
        public float MinAngle { get; set; } = -180f;
        public float MaxAngle { get; set; } = 180f;
        public float Stiffness { get; set; } = 0.5f;
        public string IKChainName { get; set; }
        // ===================================================

        public JointModel(int id, SKPoint position, JointModel parent = null)
        {
            Id = id;
            Position = position;
            Parent = parent;

            BoneLength = 0;
            Rotation = 0;
            // Başlanğıcda adı ID-yə görə təyin edək
            _name = $"Joint_{id}";
            
            // Initialize physics properties
            PreviousPosition = position;  // For Verlet integration
        }

        // Adı TextBox-da göstərmək üçün (istəyə bağlı, amma faydalıdır)
        public override string ToString()
        {
            return Name;
        }
    }
}
