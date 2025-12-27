using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SpriteEditor.Data;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views.Controls
{
    public partial class AnimationTimelineControl : UserControl
    {
        private AnimationViewModel _viewModel;
        private bool _isDraggingPlayhead = false;
        private bool _isDraggingKeyframe = false;
        private Keyframe _draggedKeyframe = null;

        public AnimationTimelineControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            SizeChanged += (s, e) => RenderTimeline();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = DataContext as AnimationViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                RenderTimeline();
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AnimationViewModel.CurrentTime) ||
                e.PropertyName == nameof(AnimationViewModel.CurrentClip) ||
                e.PropertyName == nameof(AnimationViewModel.TimelineZoom))
            {
                RenderTimeline();
            }
        }

        /// <summary>
        /// Renders the timeline (time ruler, keyframes, playhead).
        /// </summary>
        private void RenderTimeline()
        {
            if (_viewModel?.CurrentClip == null) return;

            TimelineCanvas.Children.Clear();

            float duration = _viewModel.CurrentClip.Duration;
            double canvasWidth = TimelineCanvas.ActualWidth;
            double canvasHeight = TimelineCanvas.ActualHeight;

            if (canvasWidth == 0 || canvasHeight == 0) return;

            // Calculate pixels per second
            double pixelsPerSecond = canvasWidth / duration;

            // Draw time ruler
            DrawTimeRuler(pixelsPerSecond, duration, canvasHeight);

            // Draw keyframe markers
            DrawKeyframes(pixelsPerSecond, canvasHeight);

            // Draw playhead
            DrawPlayhead(pixelsPerSecond, canvasHeight);
        }

        private void DrawTimeRuler(double pixelsPerSecond, float duration, double height)
        {
            // Draw vertical lines every 0.5 seconds
            float timeStep = 0.5f;
            for (float time = 0; time <= duration; time += timeStep)
            {
                double x = time * pixelsPerSecond;

                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    StrokeThickness = 1
                };

                TimelineCanvas.Children.Add(line);

                // Add time label every 1 second
                if (Math.Abs(time % 1.0f) < 0.01f)
                {
                    var label = new TextBlock
                    {
                        Text = $"{time:F1}s",
                        Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                        FontSize = 10
                    };

                    Canvas.SetLeft(label, x + 2);
                    Canvas.SetTop(label, 2);
                    TimelineCanvas.Children.Add(label);
                }
            }
        }

        private void DrawKeyframes(double pixelsPerSecond, double height)
        {
            if (_viewModel.CurrentClip.Keyframes == null) return;

            foreach (var keyframe in _viewModel.CurrentClip.Keyframes)
            {
                double x = keyframe.Time * pixelsPerSecond;

                // Draw diamond shape for keyframe
                var diamond = new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(x, height / 2 - 8),      // Top
                        new Point(x + 6, height / 2),      // Right
                        new Point(x, height / 2 + 8),      // Bottom
                        new Point(x - 6, height / 2)       // Left
                    },
                    Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Green
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    Cursor = Cursors.Hand,
                    Tag = keyframe
                };

                diamond.MouseEnter += (s, e) => diamond.Fill = Brushes.Yellow;
                diamond.MouseLeave += (s, e) => diamond.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));

                TimelineCanvas.Children.Add(diamond);
            }
        }

        private void DrawPlayhead(double pixelsPerSecond, double height)
        {
            double x = _viewModel.CurrentTime * pixelsPerSecond;

            // Playhead line
            var playhead = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = new SolidColorBrush(Color.FromRgb(59, 130, 246)), // Blue
                StrokeThickness = 2
            };

            TimelineCanvas.Children.Add(playhead);

            // Playhead handle (triangle at top)
            var handle = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(x, 0),
                    new Point(x - 8, 12),
                    new Point(x + 8, 12)
                },
                Fill = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                Cursor = Cursors.Hand
            };

            TimelineCanvas.Children.Add(handle);
        }

        // ========================================
        // === MOUSE INTERACTION ===
        // ========================================

        private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel?.CurrentClip == null) return;

            Point mousePos = e.GetPosition(TimelineCanvas);
            double pixelsPerSecond = TimelineCanvas.ActualWidth / _viewModel.CurrentClip.Duration;

            // Check if clicking on keyframe
            var clickedElement = e.OriginalSource as FrameworkElement;
            if (clickedElement?.Tag is Keyframe keyframe)
            {
                _isDraggingKeyframe = true;
                _draggedKeyframe = keyframe;
                _viewModel.SelectedKeyframe = keyframe;
                TimelineCanvas.CaptureMouse();
                return;
            }

            // Otherwise, seek playhead to click position
            float clickedTime = (float)(mousePos.X / pixelsPerSecond);
            clickedTime = Math.Clamp(clickedTime, 0, _viewModel.CurrentClip.Duration);

            _viewModel.SeekCommand.Execute(clickedTime);
            _isDraggingPlayhead = true;
            TimelineCanvas.CaptureMouse();
        }

        private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingPlayhead && !_isDraggingKeyframe) return;
            if (_viewModel?.CurrentClip == null) return;

            Point mousePos = e.GetPosition(TimelineCanvas);
            double pixelsPerSecond = TimelineCanvas.ActualWidth / _viewModel.CurrentClip.Duration;
            float time = (float)(mousePos.X / pixelsPerSecond);
            time = Math.Clamp(time, 0, _viewModel.CurrentClip.Duration);

            if (_isDraggingPlayhead)
            {
                _viewModel.SeekCommand.Execute(time);
            }
            else if (_isDraggingKeyframe && _draggedKeyframe != null)
            {
                // Move keyframe to new time
                _viewModel.CurrentClip.RemoveKeyframeAt(_draggedKeyframe.Time);
                _draggedKeyframe.Time = time;
                _viewModel.CurrentClip.AddKeyframe(_draggedKeyframe);
                RenderTimeline();
            }
        }

        private void TimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPlayhead = false;
            _isDraggingKeyframe = false;
            _draggedKeyframe = null;
            TimelineCanvas.ReleaseMouseCapture();
        }
    }
}
