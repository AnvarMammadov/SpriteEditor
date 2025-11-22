using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpriteEditor.ViewModels
{
    // Artıq sadə klass yox, ObservableObject olur ki, UI yenilənsin
    public partial class GridLineViewModel : ObservableObject
    {
        [ObservableProperty] private double _x1;
        [ObservableProperty] private double _y1;
        [ObservableProperty] private double _x2;
        [ObservableProperty] private double _y2;

        // Xəttin şaquli yoxsa üfüqi olduğunu bilmək üçün
        public bool IsVertical { get; set; }
    }
}
