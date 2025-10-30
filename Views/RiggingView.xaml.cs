using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views
{
    /// <summary>
    /// Interaction logic for RiggingView.xaml
    /// </summary>
    public partial class RiggingView : UserControl
    {
        private RiggingViewModel _viewModel;

        // === Sümükləri çəkmək üçün stillər (Paint objects) ===
        private readonly SKPaint _bonePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Yellow,
            StrokeWidth = 2,
            IsAntialias = true
        };
        private readonly SKPaint _jointPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.Cyan,
            IsAntialias = true
        };

        private readonly SKPaint _selectedJointPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Magenta,
            StrokeWidth = 2,
            IsAntialias = true
        };
        // ====================================================

        public RiggingView()
        {
            InitializeComponent();

            // Hadisələr XAML-da CanvasBorder-ə bağlıdır
            this.DataContextChanged += RiggingView_DataContextChanged;
        }

        private void RiggingView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Köhnə ViewModel-dən abunəliyi kəs
            if (_viewModel != null)
            {
                _viewModel.RequestRedraw -= ViewModel_RequestRedraw;
                _viewModel.RequestCenterCamera -= ViewModel_RequestCenterCamera;
            }

            _viewModel = e.NewValue as RiggingViewModel;

            // Yeni ViewModel-in "RequestRedraw" hadisəsinə abunə ol
            if (_viewModel != null)
            {
                _viewModel.RequestRedraw += ViewModel_RequestRedraw;
                _viewModel.RequestCenterCamera += ViewModel_RequestCenterCamera;
            }
        }

        private void ViewModel_RequestCenterCamera(object sender, System.EventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.CenterCamera((float)SKCanvasView.ActualWidth, (float)SKCanvasView.ActualHeight, true);
        }

        private void ViewModel_RequestRedraw(object sender, System.EventArgs e)
        {
            SKCanvasView.InvalidateVisual();
        }

        // ƏSAS RENDER MƏNTİQİ:
        private void SKCanvasView_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            SKSurface surface = e.Surface;
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.DarkGray);


            if (_viewModel == null) return;

            // === 1. KAMERA TRANSFORMUNU TƏTBİQ ET ===
            canvas.Save();

            // === QƏTİ DÜZƏLİŞ: (P*S)+O MODELİ ===
            // ViewModel-dəki (World * Scale) + Offset riyaziyyatına uyğun gələn
            // SkiaSharp render sırası ƏVVƏLCƏ Scale, SONRA Translate olmalıdır.

            // 1. Kameranın miqyasına görə kətanı böyüt/kiçilt
            canvas.Scale(_viewModel.CameraScale);

            // 2. Kameranın EKRAN ofsetinə görə kətanı sürüşdür
            // DÜZƏLİŞ: SkiaSharp-da Translate() əmri cari transformasiyaya (yəni Scale-ə)
            // məruz qalmış fəzada işləyir. EKRAN ofsetini tətbiq etmək üçün
            // onu Miqyasa bölməliyik.
            canvas.Translate(_viewModel.CameraOffset.X / _viewModel.CameraScale,
                             _viewModel.CameraOffset.Y / _viewModel.CameraScale);
            // ==========================================


            // 1. Yüklənmiş Şəkli Çək
            SKBitmap bitmap = _viewModel.LoadedBitmap;
            if (bitmap != null)
            {
                // Şəkli DÜNYA koordinatının başlanğıcında (0, 0) çəkirik
                canvas.DrawBitmap(bitmap, 0, 0);
            }

            // 2. Bütün oynaql (Joints) və sümükləri (Bones) çək
            // Oynaqların mövqeyi (Joint.Position) DÜNYA koordinatındadır
            foreach (var joint in _viewModel.Joints)
            {
                canvas.DrawCircle(joint.Position, 5f, _jointPaint);

                if (joint.Parent != null)
                {
                    canvas.DrawLine(joint.Parent.Position, joint.Position, _bonePaint);
                }
            }

            // 3. Seçilmiş oynağı (SelectedJoint) fərqli rəngdə (halqa) çək
            if (_viewModel.SelectedJoint != null)
            {
                canvas.DrawCircle(_viewModel.SelectedJoint.Position, 7f, _selectedJointPaint);
            }

            // 4. Sümük yaratma önizləməsini (Preview) çək
            if (_viewModel.CurrentTool == RiggingToolMode.CreateJoint && _viewModel.SelectedJoint != null)
            {
                canvas.DrawLine(_viewModel.SelectedJoint.Position, _viewModel.CurrentMousePosition, _bonePaint);
            }

            // === 3. KAMERA TRANSFORMUNU LƏĞV ET ===
            canvas.Restore();
            // ======================================
        }

        // === DÜZƏLİŞ: Siçan koordinatlarını birbaşa SKCanvasView-dən alaq ===
        private SKPoint GetSkiaScreenPos(MouseEventArgs e)
        {
            // Koordinatları `Border`-dən yox, `SKCanvasView`-in özündən alırıq
            Point wpfPos = e.GetPosition(SKCanvasView);
            return new SKPoint((float)wpfPos.X, (float)wpfPos.Y);
        }


        private void SKCanvasView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;
            (sender as UIElement)?.CaptureMouse();
            _viewModel.OnCanvasLeftClicked(GetSkiaScreenPos(e));
        }

        private void SKCanvasView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;
            (sender as UIElement)?.ReleaseMouseCapture();
        }

        private void SKCanvasView_MouseMove(object sender, MouseEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.OnCanvasMouseMoved(GetSkiaScreenPos(e));
        }

        private void SKCanvasView_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.OnCanvasMouseMoved(new SKPoint(-1000, -1000));
            _viewModel.StopPan();
        }


        private void SKCanvasView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_viewModel == null || Keyboard.Modifiers != ModifierKeys.Control)
            {
                return;
            }

            e.Handled = true;
            _viewModel.HandleZoom(GetSkiaScreenPos(e), e.Delta);
        }

        private void SKCanvasView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;

            if (e.ChangedButton == MouseButton.Middle)
            {
                (sender as UIElement)?.CaptureMouse();
                _viewModel.StartPan(GetSkiaScreenPos(e));
            }
        }

        private void SKCanvasView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;

            if (e.ChangedButton == MouseButton.Middle)
            {
                (sender as UIElement)?.ReleaseMouseCapture();
                _viewModel.StopPan();
            }
        }


        // === YENİ METOD: Sağ Klik Hadisəsi ===
        private void SKCanvasView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;

            // ViewModel-ə xəbər ver ki, seçimi ləğv etsin
            _viewModel.DeselectCurrentJoint();

            // Sağ klik menyusunun (context menu) açılmasının qarşısını al
            e.Handled = true;
        }

        private void SKCanvasView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.CenterCamera((float)e.NewSize.Width, (float)e.NewSize.Height, false);
        }
    }
}