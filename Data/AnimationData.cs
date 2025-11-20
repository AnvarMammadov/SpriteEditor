using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpriteEditor.Data
{
    /// <summary>
    /// Bir saniyədəki dəyər (Açar Kadr)
    /// </summary>
    public class KeyframeData
    {
        public float Time { get; set; }  // Zaman (saniyə ilə)
        public float Value { get; set; } // Dəyər (Bucaq və ya Koordinat)
        // Gələcəkdə bura Easing (Yumşalma) növü əlavə edəcəyik
    }

    /// <summary>
    /// Bir sümüyün bir xassəsinin (məs: Rotation) zamanla dəyişimi
    /// </summary>
    public class AnimationTrackData
    {
        public int JointId { get; set; }         // Hansı sümük?
        public string PropertyName { get; set; } // "Rotation", "PosX", "PosY"
        public List<KeyframeData> Keyframes { get; set; } = new List<KeyframeData>();
    }

    /// <summary>
    /// Tam bir animasiya (məs: "Walking")
    /// </summary>
    public class AnimationClipData
    {
        public string Name { get; set; } = "New Animation";
        public float Duration { get; set; } = 2.0f; // Ümumi uzunluq
        public bool IsLooping { get; set; } = true; // Təkrarlansın?
        public List<AnimationTrackData> Tracks { get; set; } = new List<AnimationTrackData>();
    }
}
