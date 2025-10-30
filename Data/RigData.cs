using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpriteEditor.Data
{
    /// <summary>
    /// Bütün skelet faylını təmsil edən əsas sinif.
    /// </summary>
    public class RigData
    {
        public string ImageFileName { get; set; } // Hansı şəkil üçün olduğunu bilmək
        public List<JointData> Joints { get; set; }

        public RigData()
        {
            Joints = new List<JointData>();
        }
    }
}
