using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SpriteEditor.Views
{
    public partial class SvgToolView : UserControl
    {
        private Point _lastMousePosition;
        private bool _isPanning;

        public SvgToolView()
        {
            InitializeComponent();

            // Başlanğıc dəyərlər
            ViewScaleTransform.ScaleX = 1;
            ViewScaleTransform.ScaleY = 1;
            ViewTranslateTransform.X = 0;
            ViewTranslateTransform.Y = 0;
        }

        // ===============================================
        // 1. ZOOM (MOUSE CURSOR-A GÖRƏ)
        // ===============================================
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Zoom Area (Konteyner) üzərindəki mouse koordinatı
            Point mousePosInContainer = e.GetPosition(ZoomArea);

            // Obyektin (Content) daxilindəki həmin nöqtə (Scale olunmamış koordinat)
            Point mousePosInContent = e.GetPosition(ViewContent);

            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            double currentScale = ViewScaleTransform.ScaleX;
            double newScale = currentScale * zoomFactor;

            // Limitlər
            if (newScale < 0.1 || newScale > 20) return;

            // Scale tətbiq edirik
            ViewScaleTransform.ScaleX = newScale;
            ViewScaleTransform.ScaleY = newScale;

            // DÜZƏLİŞ: TranslateTransform-u elə dəyişirik ki, mouse eyni nöqtənin üzərində qalsın.
            // Düstur: YeniOffset = MouseEkranYeri - (ObyektDaxiliYeri * YeniScale)
            ViewTranslateTransform.X = mousePosInContainer.X - (mousePosInContent.X * newScale);
            ViewTranslateTransform.Y = mousePosInContainer.Y - (mousePosInContent.Y * newScale);
        }

        // ===============================================
        // 2. PAN START & PIVOT SETTING
        // ===============================================
        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = this.DataContext as ViewModels.SvgToolViewModel;

            // A) PIVOT REJİMİ
            if (viewModel != null && viewModel.IsPivotMode)
            {
                // Pivot üçün daxili koordinatı alırıq
                Point p = e.GetPosition(ViewContent);

                // Margin-i (60) çıxırıq
                viewModel.SetPivotFromClick(p.X - 60, p.Y - 60);
                return; // Pan etmə, sadəcə pivot qoy
            }

            // B) PAN REJİMİ (SÜRÜŞDÜRMƏ)
            var border = sender as FrameworkElement;
            if (border != null)
            {
                _isPanning = true;
                _lastMousePosition = e.GetPosition(ZoomArea); // Başlanğıc nöqtə (Konteynerə görə)
                border.CaptureMouse();
                Mouse.OverrideCursor = Cursors.SizeAll;
            }
        }

        // ===============================================
        // 3. PAN MOVE (REAL VAİXT)
        // ===============================================
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentMousePosition = e.GetPosition(ZoomArea);

                // Nə qədər yer dəyişib?
                Vector delta = currentMousePosition - _lastMousePosition;

                // Obyekti həmin qədər sürüşdür
                ViewTranslateTransform.X += delta.X;
                ViewTranslateTransform.Y += delta.Y;

                // Son mövqeyi yenilə
                _lastMousePosition = currentMousePosition;
            }
        }

        // ===============================================
        // 4. PAN END
        // ===============================================
        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                var border = sender as FrameworkElement;
                border?.ReleaseMouseCapture();
                Mouse.OverrideCursor = null;
            }
        }

        // ===============================================
        // 5. RESET VIEW
        // ===============================================
        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Reset to default
            ViewScaleTransform.ScaleX = 1;
            ViewScaleTransform.ScaleY = 1;
            ViewTranslateTransform.X = 0;
            ViewTranslateTransform.Y = 0;
        }
    }
}