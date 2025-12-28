using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkiaSharp;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views
{
    public partial class SpriteSlicerView : UserControl
    {
        // Drag state for grid mode
        private Point? _dragStartPos = null;
        private object _draggedElement = null;  // GridLineViewModel, "SlicerRect", "SlicerRight", "SlicerBottom"
        private double _slicerStartX;
        private double _slicerStartY;
        private double _slicerStartWidth;
        private double _slicerStartHeight;

        public SpriteSlicerView()
        {
            InitializeComponent();
        }

        // === GRID MODE DRAG EVENTS ===

        private void OnGridCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as SpriteSlicerViewModel;
            if (vm == null || !vm.IsImageLoaded) return;
            if (vm.CurrentMode != SpriteSlicerViewModel.SlicerMode.Grid) return;

            var canvas = sender as Canvas;
            if (canvas == null) return;

            Point clickPos = e.GetPosition(canvas);

            // Check if clicking on a grid line
            foreach (var line in vm.GridLines)
            {
                if (IsNearLine(clickPos, line))
                {
                    _draggedElement = line;
                    _dragStartPos = clickPos;
                    canvas.CaptureMouse();
                    e.Handled = true;
                    return;
                }
            }

            // Check if clicking on slicer rectangle or handles
            double handleSize = 12; // Match XAML thumb size
            SKRect slicerRect = new SKRect(
                (float)vm.SlicerX,
                (float)vm.SlicerY,
                (float)(vm.SlicerX + vm.SlicerWidth),
                (float)(vm.SlicerY + vm.SlicerHeight)
            );

            // Check resize handles
            if (IsNearPoint(clickPos, new Point(slicerRect.Right, slicerRect.Top), handleSize))
            {
                _draggedElement = "SlicerTopRight";
                _dragStartPos = clickPos;
                _slicerStartX = vm.SlicerX;
                _slicerStartY = vm.SlicerY;
                _slicerStartWidth = vm.SlicerWidth;
                _slicerStartHeight = vm.SlicerHeight;
                canvas.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (IsNearPoint(clickPos, new Point(slicerRect.Right, slicerRect.Bottom), handleSize))
            {
                _draggedElement = "SlicerBottomRight";
                _dragStartPos = clickPos;
                _slicerStartWidth = vm.SlicerWidth;
                _slicerStartHeight = vm.SlicerHeight;
                canvas.CaptureMouse();
                e.Handled = true;
                return;
            }

            // Check if clicking inside slicer rectangle (for move)
            if (slicerRect.Contains((float)clickPos.X, (float)clickPos.Y))
            {
                _draggedElement = "SlicerRect";
                _dragStartPos = clickPos;
                _slicerStartX = vm.SlicerX;
                _slicerStartY = vm.SlicerY;
                canvas.CaptureMouse();
                e.Handled = true;
                return;
            }
        }

        private void OnGridCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragStartPos.HasValue || _draggedElement == null) return;

            var vm = DataContext as SpriteSlicerViewModel;
            if (vm == null) return;

            var canvas = sender as Canvas;
            if (canvas == null) return;

            Point currentPos = e.GetPosition(canvas);
            double deltaX = currentPos.X - _dragStartPos.Value.X;
            double deltaY = currentPos.Y - _dragStartPos.Value.Y;

            if (_draggedElement is GridLineViewModel line)
            {
                // Drag grid line
                if (line.IsVertical)
                {
                    line.X1 = line.X2 = line.X1 + deltaX;
                }
                else
                {
                    line.Y1 = line.Y2 = line.Y1 + deltaY;
                }
                _dragStartPos = currentPos;
            }
            else if (_draggedElement is string handleName)
            {
                switch (handleName)
                {
                    case "SlicerRect":
                        // Move entire slicer rectangle
                        vm.SlicerX = _slicerStartX + deltaX;
                        vm.SlicerY = _slicerStartY + deltaY;
                        break;

                    case "SlicerTopRight":
                        // Resize from top-right corner
                        vm.SlicerWidth = _slicerStartWidth + deltaX;
                        vm.SlicerHeight = _slicerStartHeight - deltaY;
                        vm.SlicerY = _slicerStartY + deltaY;
                        break;

                    case "SlicerBottomRight":
                        // Resize from bottom-right corner
                        vm.SlicerWidth = _slicerStartWidth + deltaX;
                        vm.SlicerHeight = _slicerStartHeight + deltaY;
                        break;
                }
            }

            e.Handled = true;
        }

        private void OnGridCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedElement != null)
            {
                var canvas = sender as Canvas;
                canvas?.ReleaseMouseCapture();

                _draggedElement = null;
                _dragStartPos = null;
                e.Handled = true;
            }
        }

        private bool IsNearLine(Point point, GridLineViewModel line)
        {
            double threshold = 8; // Hit test threshold in pixels

            if (line.IsVertical)
            {
                // Check if point is near vertical line
                return Math.Abs(point.X - line.X1) < threshold &&
                       point.Y >= Math.Min(line.Y1, line.Y2) - threshold &&
                       point.Y <= Math.Max(line.Y1, line.Y2) + threshold;
            }
            else
            {
                // Check if point is near horizontal line
                return Math.Abs(point.Y - line.Y1) < threshold &&
                       point.X >= Math.Min(line.X1, line.X2) - threshold &&
                       point.X <= Math.Max(line.X1, line.X2) + threshold;
            }
        }

        private bool IsNearPoint(Point p1, Point p2, double threshold)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return (dx * dx + dy * dy) < (threshold * threshold);
        }

        // === PEN TOOL EVENTS ===

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as SpriteSlicerViewModel;
            if (vm == null || !vm.IsImageLoaded) return;

            if (vm.CurrentMode == SpriteSlicerViewModel.SlicerMode.PolygonPen)
            {
                Point clickPoint = e.GetPosition((IInputElement)sender);

                if (e.ChangedButton == MouseButton.Left)
                {
                    vm.CurrentDrawingPoints.Add(clickPoint);
                    vm.UpdatePreviewLine(clickPoint);
                }
                else if (e.ChangedButton == MouseButton.Right)
                {
                    if (vm.AddCurrentPolygonToListCommand.CanExecute(null))
                    {
                        vm.AddCurrentPolygonToListCommand.Execute(null);
                    }
                }
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            var vm = DataContext as SpriteSlicerViewModel;
            if (vm != null && vm.CurrentMode == SpriteSlicerViewModel.SlicerMode.PolygonPen)
            {
                var point = e.GetPosition((IInputElement)sender);
                vm.UpdatePreviewLine(point);
            }
        }
    }
}