using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpriteEditor.Data
{
    /// <summary>
    /// Bütün mesh məlumatlarını (nöqtələr və üçbucaqlar) saxlayan sinif.
    /// </summary>
    public class MeshData
    {
        public List<VertexData> Vertices { get; set; }
        public List<TriangleData> Triangles { get; set; }

        public MeshData()
        {
            Vertices = new List<VertexData>();
            Triangles = new List<TriangleData>();
        }
    }
}
