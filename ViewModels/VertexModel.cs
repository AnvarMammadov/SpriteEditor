using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

namespace SpriteEditor.ViewModels
{
    /// <summary>
    /// Mesh-in bir nöqtəsini (vertex) ViewModel səviyyəsində təmsil edir.
    /// Bu, ObservableObject-dur, çünki mövqeyi dəyişə bilər.
    /// </summary>
    public partial class VertexModel : ObservableObject
    {
        public int Id { get; }

        /// <summary>
        /// Nöqtənin şəklin koordinat sistemindəki orijinal ("sakit") mövqeyi
        /// </summary>
        public SKPoint BindPosition { get; set; }

        /// <summary>
        /// Texture Coordinate (UV) - Şəklin hansı hissəsinin bu verteksə aid olduğunu bildirir.
        /// (Pixel koordinatları ilə)
        /// </summary>
        public SKPoint TextureCoordinate { get; set; }

        /// <summary>
        /// Nöqtənin skelet deformasiyasından sonrakı cari mövqeyi
        /// </summary>
        [ObservableProperty]
        private SKPoint _currentPosition;

        /// <summary>
        /// Bu nöqtənin hansı sümüklərdən təsirləndiyini saxlayır.
        /// Key = JointId (Oynaq ID-si)
        /// Value = Weight (Təsir dərəcəsi, 0.0 - 1.0)
        /// </summary>
        public Dictionary<int, float> Weights { get; set; }

        public VertexModel(int id, SKPoint position)
        {
            Id = id;
            BindPosition = position;
            CurrentPosition = position; // Başlanğıcda cari mövqe orijinal mövqeyə bərabərdir
            Weights = new Dictionary<int, float>();
        }
    }
}
