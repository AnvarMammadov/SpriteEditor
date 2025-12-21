using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views
{
    public partial class SpriteSlicerView : UserControl
    {
        public SpriteSlicerView()
        {
            InitializeComponent();
        }

        // === Grid Mode Üçün Resize Logic (Sizin köhnə kodunuz) ===
        private void GridLine_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var thumb = sender as Thumb;
            var line = thumb?.DataContext as GridLineViewModel;
            if (line == null) return;
            if (line.IsVertical) line.X1 = line.X2 = line.X1 + e.HorizontalChange;
            else line.Y1 = line.Y2 = line.Y1 + e.VerticalChange;
        }

        private void ThumbMove_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var vm = DataContext as SpriteSlicerViewModel;
            if (vm != null)
            {
                vm.SlicerX += e.HorizontalChange;
                vm.SlicerY += e.VerticalChange;
            }
        }

        private void ThumbResize_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var vm = DataContext as SpriteSlicerViewModel;
            if (vm == null) return;
            var thumb = sender as Thumb;
            if (thumb == null) return;

            switch (thumb.Name)
            {
                case "ThumbRight": vm.SlicerWidth += e.HorizontalChange; break;
                case "ThumbBottom": vm.SlicerHeight += e.VerticalChange; break;
                    // Digər thumblar ehtiyac olarsa əlavə edilə bilər
            }
        }

        // === YENİ: PEN TOOL MOUSE EVENT ===
        // Bu metod olmazsa, nöqtə qoymaq mümkün deyil!
        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as SpriteSlicerViewModel;
            if (vm == null || !vm.IsImageLoaded) return;

            if (vm.CurrentMode == SpriteSlicerViewModel.SlicerMode.PolygonPen)
            {
                // Canvas üzərində kliklənən nöqtəni al
                Point clickPoint = e.GetPosition((IInputElement)sender);

                if (e.ChangedButton == MouseButton.Left)
                {
                    // Sol klik: Nöqtə əlavə et
                    vm.CurrentDrawingPoints.Add(clickPoint);
                }
                else if (e.ChangedButton == MouseButton.Right)
                {
                    // Sağ klik: Tamamla
                    if (vm.AddCurrentPolygonToListCommand.CanExecute(null))
                    {
                        vm.AddCurrentPolygonToListCommand.Execute(null);
                    }
                }
            }
        }
    }
}