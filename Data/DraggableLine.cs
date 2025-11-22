using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpriteEditor.ViewModels
{
    // Hər bir xətt bir "Obyekt"dir və hərəkət edə bilər
    public partial class DraggableLine : ObservableObject
    {
        [ObservableProperty]
        private double _position; // Xəttin koordinatı (X və ya Y)

        public bool IsVertical { get; set; } // Şaquli yoxsa Üfüqi?
    }
}
