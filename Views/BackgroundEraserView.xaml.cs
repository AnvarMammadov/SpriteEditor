using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views
{
    public partial class BackgroundEraserView : UserControl
    {
        // ViewModel-ə birbaşa müraciət üçün
        private BackgroundEraserViewModel _viewModel;

        private bool _isErasing = false;
        public BackgroundEraserView()
        {
            InitializeComponent();

            // SpriteSlicerView.xaml.cs kimi, ViewModel-i əldə edək
            this.DataContextChanged += (sender, e) =>
            {
                _viewModel = e.NewValue as BackgroundEraserViewModel;
            };
        }

        // Addım 1-də XAML-da təyin etdiyimiz metod
        private void OriginalImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // ViewModel və ya şəkil yoxdursa, heçnə etmə
            if (_viewModel == null || _viewModel.LoadedImageSource == null) return;

            // === DÜZƏLİŞ BURADADIR: ALƏT REJİMİNƏ GÖRƏ MƏNTİQ ===
            if (_viewModel.CurrentToolMode == EraserToolMode.Pipet)
            {
                // Əgər alət "Pipet"dirsə, rəng seçmə məntiqini işə sal
                RunPipetTool(sender, e);
            }
            else // Rejim ManualEraser-dirsə
            {
                // Əgər alət "Manual Silgi"dirsə, silmə prosesinə başla
                _isErasing = true;

                // Kliklənən nöqtədən silməyə başla
                RunManualEraser(sender, e);

                // Siçanı tut (Capture) ki, pəncərədən kənara çıxsa da işləsin
                (sender as IInputElement)?.CaptureMouse();
            }
        }


        // === YENİ METOD: Siçan Hərəkət Etdikdə ===
        private void OriginalImage_MouseMove(object sender, MouseEventArgs e)
        {
            // Yalnız silgi rejimində və siçan basılı olduqda işlə
            if (!_isErasing || _viewModel.CurrentToolMode != EraserToolMode.ManualEraser)
            {
                return;
            }

            // Hərəkət etdikcə sil
            RunManualEraser(sender, e);
        }

        // === YENİ METOD: Siçan Buraxıldıqda ===
        private void OriginalImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isErasing = false;
            // Siçanı burax
            (sender as IInputElement)?.ReleaseMouseCapture();
        }

        // --- Köməkçi Metodlar ---

        /// <summary>
        /// Mövcud Pipet məntiqini ayrıca metoda çıxardıq
        /// </summary>
        private void RunPipetTool(object sender, MouseButtonEventArgs e)
        {
            var imageControl = sender as Image;
            var bitmapSource = imageControl.Source as BitmapSource;
            if (bitmapSource == null) return;

            Point clickPos = e.GetPosition(imageControl);
            int pixelX = (int)(clickPos.X / imageControl.ActualWidth * bitmapSource.PixelWidth);
            int pixelY = (int)(clickPos.Y / imageControl.ActualHeight * bitmapSource.PixelHeight);

            if (pixelX < 0 || pixelX >= bitmapSource.PixelWidth ||
                pixelY < 0 || pixelY >= bitmapSource.PixelHeight)
            {
                return;
            }

            CroppedBitmap cb = new CroppedBitmap(bitmapSource, new Int32Rect(pixelX, pixelY, 1, 1));
            byte[] pixels = new byte[4];
            cb.CopyPixels(pixels, 4, 0);

            _viewModel.TargetColor = Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
            _viewModel.StartPixelX = pixelX;
            _viewModel.StartPixelY = pixelY;
        }

        /// <summary>
        /// Manual Silgi məntiqi (Pikselləri şəffaf edir)
        /// </summary>
        private void RunManualEraser(object sender, MouseEventArgs e)
        {
            var imageControl = sender as Image;
            // DİQQƏT: Birbaşa WriteableBitmap-i götürürük
            var wb = _viewModel.LoadedImageSource as WriteableBitmap;
            if (wb == null) return;

            // 1. Klik koordinatlarını piksel koordinatlarına çevir
            Point clickPos = e.GetPosition(imageControl);
            int pixelX = (int)(clickPos.X / imageControl.ActualWidth * wb.PixelWidth);
            int pixelY = (int)(clickPos.Y / imageControl.ActualHeight * wb.PixelHeight);

            // 2. Fırça ölçüsünü ViewModel-dən al
            int brushSize = _viewModel.BrushSize;
            int halfBrush = brushSize / 2;

            // 3. Silinəcək sahəni (rectangle) hesabla
            // (Şəklin kənarlarına çıxmamaq şərtilə)
            int startX = Math.Max(0, pixelX - halfBrush);
            int startY = Math.Max(0, pixelY - halfBrush);
            int endX = Math.Min(wb.PixelWidth, pixelX + halfBrush);
            int endY = Math.Min(wb.PixelHeight, pixelY + halfBrush);

            int width = endX - startX;
            int height = endY - startY;

            if (width <= 0 || height <= 0) return;

            // 4. Həmin sahəni doldurmaq üçün tam şəffaf (0,0,0,0) byte massivi yarat
            int bytesPerPixel = wb.Format.BitsPerPixel / 8; // (Bgra32 üçün 4 olacaq)
            int stride = width * bytesPerPixel;
            byte[] transparentData = new byte[height * stride];
            // C#-da yeni byte[] massivi avtomatik 0-larla doldurulur,
            // bu da B:0, G:0, R:0, A:0 deməkdir (tam şəffaf)

            // 5. Pikselləri WriteableBitmap-ə yaz (ən vacib hissə)
            try
            {
                wb.Lock(); // Yaddaşı kilidlə
                Int32Rect rect = new Int32Rect(startX, startY, width, height);
                wb.WritePixels(rect, transparentData, stride, 0);
            }
            finally
            {
                wb.Unlock(); // Kilidi aç
            }
        }


        private void OriginalImage_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // ViewModel və ya şəkil yoxdursa, heçnə etmə
            if (_viewModel == null || _viewModel.LoadedImageSource == null) return;

            // Yaxınlaşdırma üçün standart olaraq Ctrl düyməsini tələb edirik
            // Əgər Ctrl basılı deyilsə, normal scroll etsin (şaquli)
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;

            // Zoom sürəti
            double zoomFactor = 1.1;
            double scale;

            if (e.Delta > 0)
            {
                // Yaxınlaşdır
                scale = ImageScaleTransform.ScaleX * zoomFactor;
            }
            else
            {
                // Uzaqlaşdır
                scale = ImageScaleTransform.ScaleX / zoomFactor;
            }

            // Zoom həddlərini təyin edək (məsələn, 10%-dən 1000%-ə qədər)
            // Bu, istifadəçinin sonsuz kiçiltmə və ya böyütməsinin qarşısını alır
            scale = Math.Max(0.1, Math.Min(scale, 10.0));

            // Həm eni, həm hündürlüyü eyni miqdarda dəyiş
            ImageScaleTransform.ScaleX = scale;
            ImageScaleTransform.ScaleY = scale;

            // Bu event-i "handled" (icra edilmiş) olaraq işarələyirik ki,
            // ScrollViewer eyni anda həm zoom, həm də şaquli scroll etməyə çalışmasın.
            e.Handled = true;
        }

    }
}
