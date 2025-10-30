using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpriteEditor.ViewModels
{
    /// <summary>
    /// Mesh-in bir üçbucağını ViewModel səviyyəsində təmsil edir.
    /// Bu, ID-lər əvəzinə birbaşa VertexModel referanslarını saxlayır.
    /// </summary>
    public class TriangleModel
    {
        /// <summary>
        /// Birinci nöqtə (Vertex)
        /// </summary>
        public VertexModel V1 { get; }

        /// <summary>
        /// İkinci nöqtə (Vertex)
        /// </summary>
        public VertexModel V2 { get; }

        /// <summary>
        /// Üçüncü nöqtə (Vertex)
        /// </summary>
        public VertexModel V3 { get; }

        public TriangleModel(VertexModel v1, VertexModel v2, VertexModel v3)
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
        }
    }
}
