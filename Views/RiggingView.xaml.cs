﻿using System;
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

        // === YENİ (PLAN 3): Mesh çəkmək üçün stillər ===
        private readonly SKPaint _meshLinePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(255, 0, 255, 100), // Şəffaf magenta
            StrokeWidth = 1,
            IsAntialias = true
        };
        private readonly SKPaint _vertexPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.LawnGreen,
            IsAntialias = true
        };
        // YENİ: Üçbucaq seçimi üçün
        private readonly SKPaint _pendingVertexPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.Orange, // Müvəqqəti seçilmiş
            IsAntialias = true
        };
        private readonly SKPaint _selectedVertexPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Red,
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

        // ƏSAS RENDER MƏNTİQİ (YENİLƏNMİŞ):
        private void SKCanvasView_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            SKSurface surface = e.Surface;
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.DarkGray);


            if (_viewModel == null) return;

            // === 1. KAMERA TRANSFORMUNU TƏTBİQ ET ===
            canvas.Save();
            canvas.Scale(_viewModel.CameraScale);
            canvas.Translate(_viewModel.CameraOffset.X / _viewModel.CameraScale,
                             _viewModel.CameraOffset.Y / _viewModel.CameraScale);

            // 1. Yüklənmiş Şəkli Çək
            SKBitmap bitmap = _viewModel.LoadedBitmap;
            if (bitmap != null)
            {
                canvas.DrawBitmap(bitmap, 0, 0);
            }

            // 2. Bütün oynaql (Joints) və sümükləri (Bones) çək
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

            // === YENİ (PLAN 3): Mesh-i (Nöqtə və Üçbucaqlar) çək ===

            // 5. Bütün Üçbucaqları (Triangles) çək
            foreach (var triangle in _viewModel.Triangles)
            {
                canvas.DrawLine(triangle.V1.CurrentPosition, triangle.V2.CurrentPosition, _meshLinePaint);
                canvas.DrawLine(triangle.V2.CurrentPosition, triangle.V3.CurrentPosition, _meshLinePaint);
                canvas.DrawLine(triangle.V3.CurrentPosition, triangle.V1.CurrentPosition, _meshLinePaint);
            }

            // 6. Bütün Nöqtələri (Vertices) çək
            foreach (var vertex in _viewModel.Vertices)
            {
                canvas.DrawCircle(vertex.CurrentPosition, 4f, _vertexPaint);
            }

            // 7. YENİ: Üçbucaq üçün gözləyən nöqtələri (Pending) çək
            foreach (var vertex in _viewModel.VertexSelectionForTriangle)
            {
                // Bütün nöqtələrin (yaşıl) üstünə narıncı rəngdə çək
                canvas.DrawCircle(vertex.CurrentPosition, 4f, _pendingVertexPaint);
            }

            // 8. Seçilmiş Nöqtəni (SelectedVertex) çək
            if (_viewModel.SelectedVertex != null)
            {
                // Bu, həm də gözləyən nöqtələrin (narıncı) üstünə çəkiləcək (qırmızı halqa)
                canvas.DrawCircle(_viewModel.SelectedVertex.CurrentPosition, 6f, _selectedVertexPaint);
            }
            // ========================================================


            // === 3. KAMERA TRANSFORMUNU LƏĞV ET ===
            canvas.Restore();
            // ======================================
        }

        private SKPoint GetSkiaScreenPos(MouseEventArgs e)
        {
            Point wpfPos = e.GetPosition(SKCanvasView);
            return new SKPoint((float)wpfPos.X, (float)wpfPos.Y);
        }

        // === YENİLƏNMİŞ ===
        private void SKCanvasView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;
            (sender as UIElement)?.Focus();
            (sender as UIElement)?.CaptureMouse();

            // YENİ: Ctrl düyməsinin vəziyyətini yoxla
            bool isCtrlPressed = Keyboard.Modifiers == ModifierKeys.Control;
            _viewModel.OnCanvasLeftClicked(GetSkiaScreenPos(e), isCtrlPressed);
        }


        // === YENİLƏNMİŞ ===
        private void CanvasBorder_KeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null) return;
            if (e.Key == Key.Delete)
            {
                if (_viewModel.SelectedVertex != null)
                {
                    _viewModel.DeleteSelectedVertex();
                }
                else if (_viewModel.SelectedJoint != null)
                {
                    _viewModel.DeleteSelectedJoint();
                }
                e.Handled = true;
            }
        }


        private void SKCanvasView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;
            (sender as UIElement)?.ReleaseMouseCapture();
            _viewModel.OnCanvasLeftReleased();
        }

        // === YENİLƏNMİŞ ===
        private void SKCanvasView_MouseMove(object sender, MouseEventArgs e)
        {
            if (_viewModel == null) return;

            // YENİ: Ctrl düyməsinin vəziyyətini yoxla
            bool isCtrlPressed = Keyboard.Modifiers == ModifierKeys.Control;
            _viewModel.OnCanvasMouseMoved(GetSkiaScreenPos(e), isCtrlPressed);
        }

        private void SKCanvasView_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.OnCanvasMouseMoved(new SKPoint(-1000, -1000), false); // Ctrl basılmır
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


        // === YENİLƏNMİŞ ===
        private void SKCanvasView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.DeselectCurrentJoint();
            _viewModel.DeselectCurrentVertex(); // Bu metod hər iki vertex seçimini təmizləyir
            e.Handled = true;
        }

        private void SKCanvasView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.CenterCamera((float)e.NewSize.Width, (float)e.NewSize.Height, false);
        }
    }
}