using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // Thumb və DragDeltaEventArgs üçün vacibdir
using System.Windows.Input;
using SpriteEditor.ViewModels; // MainViewModel-i tanımaq üçün

namespace SpriteEditor.Views
{
    /// <summary>
    /// Interaction logic for SpriteSlicerView.xaml
    /// </summary>
    public partial class SpriteSlicerView : UserControl
    {
        private SpriteSlicerViewModel _viewModel;
        private const double ThumbSize = 10.0; // Stilimizdə (Style) təyin etdiyimiz ölçü
        private const double MinSlicerSize = 10.0; // Slicer Box-un minimum eni/hündürlüyü
        private const double MinLineSpacing = 8.0;   // qonşu xətlə minimum məsafə
        private const double GridSnapSize = 2.0;     // grid ölçüsü (snap bu dəyərə olacaq)

        public SpriteSlicerView()
        {
            InitializeComponent();

            // View-a DataContext (yəni MainViewModel) təyin olunanda xəbər tutmaq üçün
            // Bu, _viewModel referansını əldə etmək üçün ən etibarlı yoldur
            this.DataContextChanged += SpriteSlicerView_DataContextChanged;
        }

        private void SpriteSlicerView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Köhnə ViewModel-dən abunəliyi ləğv et (əgər varsa)
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // Yeni ViewModel-i referans olaraq götür
            _viewModel = e.NewValue as SpriteSlicerViewModel;

            // Yeni ViewModel-in "Slicer..." xassələri dəyişdikdə...
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        /// <summary>
        /// ViewModel-dəki Slicer xassələri dəyişəndə (məsələn, kodla və ya sürükləmə ilə),
        /// 8 ölçüləndirmə nöqtəsinin yerini yeniləyirik.
        /// </summary>
        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.StartsWith("Slicer"))
            {
                UpdateResizeThumbPositions();
            }
        }

        /// <summary>
        /// Slicer Box-un 8 künc/kənar nöqtəsinin yerini hesablayıb təyin edir.
        /// </summary>
        private void UpdateResizeThumbPositions()
        {
            if (_viewModel == null) return;

            double x = _viewModel.SlicerX;
            double y = _viewModel.SlicerY;
            double width = _viewModel.SlicerWidth;
            double height = _viewModel.SlicerHeight;
            double halfThumb = ThumbSize / 2;

            // XAML-dakı 8 Thumb elementinin Canvas üzərindəki yerini təyin edirik
            Canvas.SetLeft(ThumbTopLeft, x - halfThumb);
            Canvas.SetTop(ThumbTopLeft, y - halfThumb);

            Canvas.SetLeft(ThumbTopRight, x + width - halfThumb);
            Canvas.SetTop(ThumbTopRight, y - halfThumb);

            Canvas.SetLeft(ThumbBottomLeft, x - halfThumb);
            Canvas.SetTop(ThumbBottomLeft, y + height - halfThumb);

            Canvas.SetLeft(ThumbBottomRight, x + width - halfThumb);
            Canvas.SetTop(ThumbBottomRight, y + height - halfThumb);

            Canvas.SetLeft(ThumbTop, x + width / 2 - halfThumb);
            Canvas.SetTop(ThumbTop, y - halfThumb);

            Canvas.SetLeft(ThumbBottom, x + width / 2 - halfThumb);
            Canvas.SetTop(ThumbBottom, y + height - halfThumb);

            Canvas.SetLeft(ThumbLeft, x - halfThumb);
            Canvas.SetTop(ThumbLeft, y + height / 2 - halfThumb);

            Canvas.SetLeft(ThumbRight, x + width - halfThumb);
            Canvas.SetTop(ThumbRight, y + height / 2 - halfThumb);
        }

        /// <summary>
        /// Bütün Slicer Box-u hərəkət etdirmək üçün (ThumbMove).
        /// </summary>
        private void ThumbMove_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_viewModel == null) return;

            // Yeni X və Y koordinatlarını hesabla
            double newX = _viewModel.SlicerX + e.HorizontalChange;
            double newY = _viewModel.SlicerY + e.VerticalChange;

            // Sərhədləri yoxla (Şəkildən kənara çıxmasın)
            double maxX = _viewModel.ImagePixelWidth - _viewModel.SlicerWidth;
            double maxY = _viewModel.ImagePixelHeight - _viewModel.SlicerHeight;

            // Math.Max(0, ...) -> 0-dan az olmasın
            // Math.Min(..., maxX) -> Şəklin sağ sərhədindən çox olmasın
            _viewModel.SlicerX = Math.Max(0, Math.Min(newX, maxX));
            _viewModel.SlicerY = Math.Max(0, Math.Min(newY, maxY));
        }

        /// <summary>
        /// Slicer Box-u ölçüləndirmək üçün (8 künc/kənar nöqtəsi).
        /// </summary>
        private void ThumbResize_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_viewModel == null || !(sender is Thumb thumb)) return;

            // Mövcud dəyərləri götür
            double x = _viewModel.SlicerX;
            double y = _viewModel.SlicerY;
            double width = _viewModel.SlicerWidth;
            double height = _viewModel.SlicerHeight;

            // Hansı nöqtənin sürükləndiyinə görə hesablama apar
            switch (thumb.Name)
            {
                case "ThumbTopLeft":
                    x += e.HorizontalChange;
                    width -= e.HorizontalChange;
                    y += e.VerticalChange;
                    height -= e.VerticalChange;
                    break;
                case "ThumbTop":
                    y += e.VerticalChange;
                    height -= e.VerticalChange;
                    break;
                case "ThumbTopRight":
                    width += e.HorizontalChange;
                    y += e.VerticalChange;
                    height -= e.VerticalChange;
                    break;
                case "ThumbLeft":
                    x += e.HorizontalChange;
                    width -= e.HorizontalChange;
                    break;
                case "ThumbRight":
                    width += e.HorizontalChange;
                    break;
                case "ThumbBottomLeft":
                    x += e.HorizontalChange;
                    width -= e.HorizontalChange;
                    height += e.VerticalChange;
                    break;
                case "ThumbBottom":
                    height += e.VerticalChange;
                    break;
                case "ThumbBottomRight":
                    width += e.HorizontalChange;
                    height += e.VerticalChange;
                    break;
            }

            // === Sərhəd Yoxlamaları ===

            // 1. Minimum ölçünü təmin et
            if (width < MinSlicerSize)
            {
                if (thumb.Name.Contains("Left")) // Sol tərəfdən kiçildirdisə
                    x = _viewModel.SlicerX + _viewModel.SlicerWidth - MinSlicerSize;
                width = MinSlicerSize;
            }
            if (height < MinSlicerSize)
            {
                if (thumb.Name.Contains("Top")) // Üst tərəfdən kiçildirdisə
                    y = _viewModel.SlicerY + _viewModel.SlicerHeight - MinSlicerSize;
                height = MinSlicerSize;
            }

            // 2. Şəklin sərhədlərini təmin et (kənara çıxmasın)
            if (x < 0) { width += x; x = 0; } // Sol sərhəd
            if (y < 0) { height += y; y = 0; } // Üst sərhəd

            if (x + width > _viewModel.ImagePixelWidth)
            {
                width = _viewModel.ImagePixelWidth - x;
            }
            if (y + height > _viewModel.ImagePixelHeight)
            {
                height = _viewModel.ImagePixelHeight - y;
            }

            // Yekun dəyərləri ViewModel-ə ötür
            _viewModel.SlicerX = x;
            _viewModel.SlicerY = y;
            _viewModel.SlicerWidth = width;
            _viewModel.SlicerHeight = height;
        }

        // Bu metodu SpriteSlicerView.xaml.cs faylinda yeniləyin

        private void GridLine_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (_viewModel == null || SlicerCanvas == null)
                return;

            var thumb = sender as System.Windows.Controls.Primitives.Thumb;
            var lineVM = thumb?.DataContext as GridLineViewModel;
            if (lineVM == null)
                return;

            // Mouse-un Canvas üzərindəki mövqeyi (Viewbox olsa da, Canvas.ActualWidth/Həqiqi ölçü ilə gəlir)
            Point pos = Mouse.GetPosition(SlicerCanvas);

            // Canvas ölçüsünü şəkil piksel ölçüsünə map edirik
            double scaleX = _viewModel.ImagePixelWidth / SlicerCanvas.ActualWidth;
            double scaleY = _viewModel.ImagePixelHeight / SlicerCanvas.ActualHeight;

            // Slicer Box sərhədləri (şəkil koordinatlarında)
            double slicerLeft = _viewModel.SlicerX;
            double slicerRight = _viewModel.SlicerX + _viewModel.SlicerWidth;
            double slicerTop = _viewModel.SlicerY;
            double slicerBottom = _viewModel.SlicerY + _viewModel.SlicerHeight;

            if (lineVM.IsVertical)
            {
                // 1) Mouse X -> şəkil koordinatı
                double newX = pos.X * scaleX;

                // 2) Slicer sərhədlərinə görə clamp
                newX = Math.Max(slicerLeft + 1, Math.Min(newX, slicerRight - 1));

                // 3) Qonşu vertical xətləri tap (özündən başqa)
                var otherLines = _viewModel.GridLines
                    .Where(l => l.IsVertical && !ReferenceEquals(l, lineVM))
                    .ToList();

                double leftLimit = slicerLeft;
                double rightLimit = slicerRight;

                foreach (var l in otherLines)
                {
                    if (l.X1 < newX && l.X1 > leftLimit)
                        leftLimit = l.X1;

                    if (l.X1 > newX && l.X1 < rightLimit)
                        rightLimit = l.X1;
                }

                // 4) Qonşularla minimum məsafəni saxla
                newX = Math.Max(leftLimit + MinLineSpacing, Math.Min(newX, rightLimit - MinLineSpacing));

                // 5) Grid snap (8 px)
                newX = Math.Round(newX / GridSnapSize) * GridSnapSize;

                // Snap-dan sonra yenə safety clamp
                newX = Math.Max(leftLimit + MinLineSpacing, Math.Min(newX, rightLimit - MinLineSpacing));

                lineVM.X1 = newX;
                lineVM.X2 = newX;
            }
            else
            {
                // Üfüqi xətt

                // 1) Mouse Y -> şəkil koordinatı
                double newY = pos.Y * scaleY;

                // 2) Slicer sərhədlərinə görə clamp
                newY = Math.Max(slicerTop + 1, Math.Min(newY, slicerBottom - 1));

                // 3) Qonşu horizontal xətləri tap
                var otherLines = _viewModel.GridLines
                    .Where(l => !l.IsVertical && !ReferenceEquals(l, lineVM))
                    .ToList();

                double topLimit = slicerTop;
                double bottomLimit = slicerBottom;

                foreach (var l in otherLines)
                {
                    if (l.Y1 < newY && l.Y1 > topLimit)
                        topLimit = l.Y1;

                    if (l.Y1 > newY && l.Y1 < bottomLimit)
                        bottomLimit = l.Y1;
                }

                // 4) Qonşularla minimum məsafə
                newY = Math.Max(topLimit + MinLineSpacing, Math.Min(newY, bottomLimit - MinLineSpacing));

                // 5) Grid snap
                newY = Math.Round(newY / GridSnapSize) * GridSnapSize;

                // Snap-dan sonra yenə clamp
                newY = Math.Max(topLimit + MinLineSpacing, Math.Min(newY, bottomLimit - MinLineSpacing));

                lineVM.Y1 = newY;
                lineVM.Y2 = newY;
            }

            e.Handled = true;
        }
    }
}