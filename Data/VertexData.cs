using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SkiaSharp;

namespace SpriteEditor.Data
{
    /// <summary>
    /// Mesh-in bir nöqtəsini (vertex) təmsil edir.
    /// </summary>
    public class VertexData
    {
        public int Id { get; set; }

        /// <summary>
        /// Nöqtənin şəklin koordinat sistemindəki orijinal ("sakit") mövqeyi
        /// </summary>
        [JsonInclude]
        public SKPoint Position { get; set; }

        /// <summary>
        /// Bu nöqtənin hansı sümüklərdən təsirləndiyini saxlayır.
        /// Key = JointId (Oynaq ID-si)
        /// Value = Weight (Təsir dərəcəsi, 0.0 - 1.0)
        /// </summary>
        public Dictionary<int, float> Weights { get; set; }

        public VertexData()
        {
            Weights = new Dictionary<int, float>();
        }
    }
}
