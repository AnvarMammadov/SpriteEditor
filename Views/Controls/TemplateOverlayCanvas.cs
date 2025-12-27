using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SkiaSharp;
using SpriteEditor.Data;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views.Controls
{
    /// <summary>
    /// Simple overlay canvas that renders template skeleton on top of sprite.
    /// PROTOTYPE: No drag/drop yet, just visualization + scale slider.
    /// </summary>
    public partial class TemplateOverlayCanvas : UserControl
    {
        public static readonly DependencyProperty TemplateProperty =
            DependencyProperty.Register(nameof(Template), typeof(RigTemplate), typeof(TemplateOverlayCanvas),
                new PropertyMetadata(null, OnTemplateChanged));

        public static readonly DependencyProperty SpriteBoundsProperty =
            DependencyProperty.Register(nameof(SpriteBounds), typeof(SKRect), typeof(TemplateOverlayCanvas),
                new PropertyMetadata(SKRect.Empty, OnSpriteBoundsChanged));

        public static readonly DependencyProperty ScaleFactorProperty =
            DependencyProperty.Register(nameof(ScaleFactor), typeof(double), typeof(TemplateOverlayCanvas),
                new PropertyMetadata(1.0, OnScaleFactorChanged));

        public RigTemplate Template
        {
            get => (RigTemplate)GetValue(TemplateProperty);
            set => SetValue(TemplateProperty, value);
        }

        public SKRect SpriteBounds
        {
            get => (SKRect)GetValue(SpriteBoundsProperty);
            set => SetValue(SpriteBoundsProperty, value);
        }

        public double ScaleFactor
        {
            get => (double)GetValue(ScaleFactorProperty);
            set => SetValue(ScaleFactorProperty, value);
        }

        private Canvas _renderCanvas;

        public TemplateOverlayCanvas()
        {
            _renderCanvas = new Canvas
            {
                Background = Brushes.Transparent,
                IsHitTestVisible = false // Allow clicks to pass through
            };

            Content = _renderCanvas;
        }

        private static void OnTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TemplateOverlayCanvas canvas)
                canvas.RenderOverlay();
        }

        private static void OnSpriteBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TemplateOverlayCanvas canvas)
                canvas.RenderOverlay();
        }

        private static void OnScaleFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TemplateOverlayCanvas canvas)
                canvas.RenderOverlay();
        }

        private void RenderOverlay()
        {
            _renderCanvas.Children.Clear();

            if (Template == null || SpriteBounds.Width <= 0 || SpriteBounds.Height <= 0)
                return;

            // Calculate scaled bounds
            float centerX = SpriteBounds.MidX;
            float centerY = SpriteBounds.MidY;
            float scaledWidth = SpriteBounds.Width * (float)ScaleFactor;
            float scaledHeight = SpriteBounds.Height * (float)ScaleFactor;

            // Map template joints to pixel positions
            var jointPositions = new Dictionary<string, Point>();
            foreach (var templateJoint in Template.Joints)
            {
                float pixelX = centerX + (templateJoint.NormalizedPosition.X - 0.5f) * scaledWidth;
                float pixelY = centerY + (templateJoint.NormalizedPosition.Y - 0.5f) * scaledHeight;
                jointPositions[templateJoint.Name] = new Point(pixelX, pixelY);
            }

            // Draw bones (lines between parent-child joints)
            foreach (var templateJoint in Template.Joints)
            {
                if (!string.IsNullOrEmpty(templateJoint.ParentName) &&
                    jointPositions.TryGetValue(templateJoint.Name, out var childPos) &&
                    jointPositions.TryGetValue(templateJoint.ParentName, out var parentPos))
                {
                    var line = new Line
                    {
                        X1 = parentPos.X,
                        Y1 = parentPos.Y,
                        X2 = childPos.X,
                        Y2 = childPos.Y,
                        Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 215, 0)), // Semi-transparent gold
                        StrokeThickness = 3
                    };
                    _renderCanvas.Children.Add(line);
                }
            }

            // Draw joints (circles)
            foreach (var (name, position) in jointPositions)
            {
                var circle = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = new SolidColorBrush(Color.FromArgb(220, 16, 185, 129)), // Semi-transparent green
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };

                Canvas.SetLeft(circle, position.X - 6);
                Canvas.SetTop(circle, position.Y - 6);

                _renderCanvas.Children.Add(circle);

                // Add joint name label
                var label = new TextBlock
                {
                    Text = name,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0))
                };

                Canvas.SetLeft(label, position.X + 8);
                Canvas.SetTop(label, position.Y - 5);

                _renderCanvas.Children.Add(label);
            }
        }
    }
}
