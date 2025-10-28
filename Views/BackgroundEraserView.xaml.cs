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
            if (_viewModel == null || !_viewModel.IsImageLoaded) return;

            var imageControl = sender as Image;
            var bitmapSource = imageControl.Source as BitmapSource;
            if (bitmapSource == null) return;

            // === ƏSAS MƏNTİQ: Klik koordinatını Piksel koordinatına çevirmək ===

            // 1. Klik nöqtəsini şəklin daxilində (Viewbox-dan asılı olmayaraq) tap
            Point clickPos = e.GetPosition(imageControl);

            // 2. Klik koordinatını (məs. 800x600-lük şəkildə 100,50) 
            //    real piksel koordinatına (məs. 320x240-lik şəkildə 40,31) çevir
            int pixelX = (int)(clickPos.X / imageControl.ActualWidth * bitmapSource.PixelWidth);
            int pixelY = (int)(clickPos.Y / imageControl.ActualHeight * bitmapSource.PixelHeight);

            // Sərhədlərdən kənara çıxmamağı yoxla
            if (pixelX < 0 || pixelX >= bitmapSource.PixelWidth ||
                pixelY < 0 || pixelY >= bitmapSource.PixelHeight)
            {
                return;
            }

            // 3. Həmin tək pikselin rəngini al (WPF-in daxili üsulu ilə)
            // Həmin pikseldən 1x1 ölçüdə yeni bir şəkil kəs
            CroppedBitmap cb = new CroppedBitmap(bitmapSource,
                new Int32Rect(pixelX, pixelY, 1, 1));

            // 1x1 şəklin rəng məlumatını (4 bayt: Mavi, Yaşıl, Qırmızı, Alfa) oxu
            byte[] pixels = new byte[4];
            cb.CopyPixels(pixels, 4, 0); // 4 bayt/piksel

            // 4. Oxunan rəngi ViewModel-dəki TargetColor xassəsinə ötür
            // Qeyd: WPF baytları B-G-R-A ardıcıllığında saxlayır
            _viewModel.TargetColor = Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);

            // === YENİ KOD ===
            // 5. Kliklənən nöqtəni də ViewModel-ə ötür
            _viewModel.StartPixelX = pixelX;
            _viewModel.StartPixelY = pixelY;
            // =================
        }
    }
}
