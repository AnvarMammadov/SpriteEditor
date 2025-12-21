using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SpriteEditor.Views
{
    public partial class SvgToolView : UserControl
    {
        private Point _origin;
        private Point _start;

        public SvgToolView()
        {
            InitializeComponent();

            // Default Zoom/Pan dəyərlərini sıfırla
            ViewScaleTransform.ScaleX = 1;
            ViewScaleTransform.ScaleY = 1;
            ViewTranslateTransform.X = 0;
            ViewTranslateTransform.Y = 0;
        }

        // 1. ZOOM (Mouse Wheel)
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var zoomFactor = e.Delta > 0 ? 1.1 : 0.9;

            // Cari miqyas
            double currentScale = ViewScaleTransform.ScaleX;
            double newScale = currentScale * zoomFactor;

            // Limitlər (Çox kiçik və ya çox böyük olmasın)
            if (newScale < 0.1 || newScale > 20) return;

            // Mouse-un olduğu nöqtəyə görə zoom etmək
            Point mousePos = e.GetPosition(ViewContent);

            ViewScaleTransform.ScaleX = newScale;
            ViewScaleTransform.ScaleY = newScale;

            // Sürüşməni düzəlt ki, mouse-un altındakı nöqtə sabit qalsın
            double absoluteX = mousePos.X * currentScale + ViewTranslateTransform.X;
            double absoluteY = mousePos.Y * currentScale + ViewTranslateTransform.Y;

            ViewTranslateTransform.X = absoluteX - mousePos.X * newScale;
            ViewTranslateTransform.Y = absoluteY - mousePos.Y * newScale;
        }

        // 2. PAN START (Mouse Down)
        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border != null)
            {
                // Mouse-u tuturuq ki, çərçivədən çıxanda da işləsin
                border.CaptureMouse();
                _start = e.GetPosition(ZoomArea);
                _origin = new Point(ViewTranslateTransform.X, ViewTranslateTransform.Y);

                // Cursoru dəyiş (Opsional)
                Mouse.OverrideCursor = Cursors.SizeAll;
            }
        }

        // 3. PAN END (Mouse Up)
        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border != null)
            {
                border.ReleaseMouseCapture();
                Mouse.OverrideCursor = null;
            }
        }

        // 4. PAN MOVE (Mouse Move)
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (ZoomArea.IsMouseCaptured)
            {
                Vector v = _start - e.GetPosition(ZoomArea);
                ViewTranslateTransform.X = _origin.X - v.X;
                ViewTranslateTransform.Y = _origin.Y - v.Y;
            }
        }

        // 5. RESET (Right Click)
        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Görüntünü sıfırla
            ViewScaleTransform.ScaleX = 1;
            ViewScaleTransform.ScaleY = 1;
            ViewTranslateTransform.X = 0;
            ViewTranslateTransform.Y = 0;
        }
    }
}