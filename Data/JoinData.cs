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
    /// Yadda saxlamaq üçün "təmiz" oynaq məlumatı.
    /// Parent obyektini yox, ParentId-ni saxlayır.
    /// </summary>
    public class JointData
    {
        public int Id { get; set; }

        // Root oynaqlarda (valideyni olmayan) bu -1 olacaq
        public int ParentId { get; set; }

        // SKPoint-in X və Y xassələrini JSON-a yazmaq üçün
        [JsonInclude]
        public SKPoint Position { get; set; }
    }
}
