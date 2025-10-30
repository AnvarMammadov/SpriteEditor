using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpriteEditor.Data
{
    /// <summary>
    /// Mesh-in bir üçbucağını təmsil edir (3 Vertex ID-si ilə).
    /// </summary>
    public class TriangleData
    {
        /// <summary>
        /// Birinci nöqtənin (Vertex) ID-si
        /// </summary>
        public int V1 { get; set; }

        /// <summary>
        /// İkinci nöqtənin (Vertex) ID-si
        /// </summary>
        public int V2 { get; set; }

        /// <summary>
        /// Üçüncü nöqtənin (Vertex) ID-si
        /// </summary>
        public int V3 { get; set; }
    }
}
