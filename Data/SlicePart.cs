using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpriteEditor.Data
{
    public class SlicePart : ObservableObject
    {
        public string Name { get; set; } // Məs: "Blade", "Hilt"
        public ObservableCollection<Point> Points { get; set; } = new ObservableCollection<Point>();
        public bool IsSelected { get; set; }

        // UI-da göstərmək üçün rəng (məs: Yaşıl yarım-şəffaf)
        public Brush FillColor { get; set; } = new SolidColorBrush(Color.FromArgb(100, 0, 255, 0));
    }
}
