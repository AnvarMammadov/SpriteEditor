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
            CanvasBorder.MouseDown += SKCanvasView_MouseDown;
            CanvasBorder.MouseUp += SKCanvasView_MouseUp;

            this.DataContextChanged += RiggingView_DataContextChanged;
        }

        private void RiggingView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Köhnə ViewModel-dən abunəliyi kəs
            if (_viewModel != null)
            {
                _viewModel.RequestRedraw -= ViewModel_RequestRedraw;
                _viewModel.RequestCenterCamera -= ViewModel_RequestCenterCamera; // YENİ
            }

            _viewModel = e.NewValue as RiggingViewModel;

            // Yeni ViewModel-in "RequestRedraw" hadisəsinə abunə ol
            if (_viewModel != null)
            {
                _viewModel.RequestRedraw += ViewModel_RequestRedraw;
                _viewModel.RequestCenterCamera += ViewModel_RequestCenterCamera; // YENİ
            }
        }


        // YENİ METOD: ViewModel mərkəzləmə istədikdə işə düşür
        private void ViewModel_RequestCenterCamera(object sender, System.EventArgs e)
        {
            if (_viewModel == null) return;

            // ViewModel-ə mərkəzləmə əmri veririk (forceRecenter: true)
            _viewModel.CenterCamera((float)SKCanvasView.ActualWidth, (float)SKCanvasView.ActualHeight, true);
        }

        // ViewModel "Yenidən çək" dedikdə, SKElement-ə xəbər veririk
        private void ViewModel_RequestRedraw(object sender, System.EventArgs e)
        {
            // Bu metod SKElement-i "PaintSurface" hadisəsini yenidən işə salmağa məcbur edir
            SKCanvasView.InvalidateVisual();
        }

        // ƏSAS RENDER MƏNTİQİ:
        // SKElement hər dəfə yenilənməli olduqda (məs. ölçü dəyişəndə, InvalidateVisual çağırılanda) bu metod işə düşür
        private void SKCanvasView_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            // 'e' bizə hazır kətan (canvas) və səth (surface) verir
            SKSurface surface = e.Surface;
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.DarkGray); // Fonu təmizlə


            if (_viewModel == null) return;

            // === 1. KAMERA TRANSFORMUNU TƏTBİQ ET ===
            canvas.Save(); // Hazırkı vəziyyəti yadda saxla

            // Kameranın ofsetinə görə kətanı sürüşdür
            canvas.Translate(_viewModel.CameraOffset);

            // Kameranın miqyasına görə kətanı böyüt/kiçilt
            canvas.Scale(_viewModel.CameraScale);
            // ==========================================


            // 1. Yüklənmiş Şəkli Çək
            SKBitmap bitmap = _viewModel.LoadedBitmap;
            if (bitmap != null)
            {
                // Şəkli (0, 0) koordinatından başlayaraq çəkirik
                canvas.DrawBitmap(bitmap, 0, 0);
            }

            // === YENİ: Sümükləri və Oynaqları çək ===

            // 2. Bütün oynaql (Joints) və sümükləri (Bones) çək
            foreach (var joint in _viewModel.Joints)
            {
                // Oynağın özünü (nöqtə) çək
                canvas.DrawCircle(joint.Position, 5f, _jointPaint);

                // Əgər bu oynağın atası (parent) varsa, sümüyü (xətti) çək
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
                // Seçilmiş oynağdan siçanın hazırkı yerinə xətt çək
                canvas.DrawLine(_viewModel.SelectedJoint.Position, _viewModel.CurrentMousePosition, _bonePaint);
            }
            // === 3. KAMERA TRANSFORMUNU LƏĞV ET ===
            canvas.Restore(); // Kətanı (0,0) və (1.0) vəziyyətinə qaytar
            // ======================================

            // Gələcəkdə bura HUD (məsələn, koordinatlar, "Zoom: 150%") çəkə bilərik
            // Bu elementlər kameradan təsirlənməyəcək.
        }

        private SKPoint GetSkiaScreenPos(object sender, MouseEventArgs e)
        {
            // İndi 'sender' bu kontekstdə mövcuddur
            Point wpfPos = e.GetPosition(sender as IInputElement);
            return new SKPoint((float)wpfPos.X, (float)wpfPos.Y);
        }


        private void SKCanvasView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;
            // Siçanı tuturuq ki, kənara çıxsa da "Up" hadisəsini tuta bilək
            (sender as UIElement)?.CaptureMouse();
            _viewModel.OnCanvasLeftClicked(GetSkiaScreenPos(sender, e));
        }

        /// <summary>
        /// Sol düymə buraxıldıqda siçan "girovluğunu" dayandırır.
        /// </summary>
        private void SKCanvasView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;
            (sender as UIElement)?.ReleaseMouseCapture();
        }

        private void SKCanvasView_MouseMove(object sender, MouseEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.OnCanvasMouseMoved(GetSkiaScreenPos(sender, e));
        }

        private void SKCanvasView_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.OnCanvasMouseMoved(new SKPoint(-1000, -1000)); // Önizləməni gizlət
            _viewModel.StopPan(); // Pan edirdisə dayandır
        }

        // === YENİ HADİSƏ HANDLER-LƏRİ ===

        private void SKCanvasView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_viewModel == null || Keyboard.Modifiers != ModifierKeys.Control)
            {
                // Yalnız Ctrl basılı olduqda zoom et
                // Əks halda, gələcəkdə normal ScrollViewer (əgər əlavə etsək) işləsin
                return;
            }

            e.Handled = true; // Bu hadisənin başqa elementə getməsinin qarşısını al
            _viewModel.HandleZoom(GetSkiaScreenPos(sender, e), e.Delta);
        }




        /// <summary>
        /// Bütün düymə basılmalarını idarə edən ümumi metod.
        /// </summary>
        private void SKCanvasView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;

            // Yalnız ORTA düymə (Middle) basılıbsa "Pan" (sürüşdürmə) rejimini başlat
            if (e.ChangedButton == MouseButton.Middle)
            {
                (sender as UIElement)?.CaptureMouse();
                _viewModel.StartPan(GetSkiaScreenPos(sender, e));
            }

            // (Sol düymə artıq "SKCanvasView_MouseLeftButtonDown" tərəfindən idarə olunur,
            // ona görə burada ona ehtiyac yoxdur)
        }

        /// <summary>
        /// Bütün düymə buraxılmalarını idarə edən ümumi metod.
        /// </summary>
        private void SKCanvasView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;

            // Yalnız ORTA düymə (Middle) buraxılıbsa "Pan" (sürüşdürmə) rejimini dayandır
            if (e.ChangedButton == MouseButton.Middle)
            {
                (sender as UIElement)?.ReleaseMouseCapture();
                _viewModel.StopPan();
            }
        }

        // DÜZƏLDİLMİŞ: SizeChanged metodu
        private void SKCanvasView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel == null) return;

            // Pəncərə ölçüsü dəyişəndə mərkəzləməyə cəhd edirik (forceRecenter: false)
            _viewModel.CenterCamera((float)e.NewSize.Width, (float)e.NewSize.Height, false);
        }


    }
}
