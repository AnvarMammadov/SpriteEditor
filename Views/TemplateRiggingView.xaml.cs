using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views
{
    public partial class TemplateRiggingView : UserControl
    {
        private TemplateRiggingViewModel ViewModel => DataContext as TemplateRiggingViewModel;

        // Paint objects
        private readonly SKPaint _bonePaint = new SKPaint { Color = new SKColor(234, 179, 8), StrokeWidth = 3, IsAntialias = true };
        private readonly SKPaint _jointPaint = new SKPaint { Color = SKColors.Red, IsAntialias = true, Style = SKPaintStyle.Fill };
        private readonly SKPaint _anchorPaint = new SKPaint { Color = SKColors.Cyan, IsAntialias = true, Style = SKPaintStyle.Fill };
        private readonly SKPaint _meshPaint = new SKPaint { Color = new SKColor(255, 255, 255, 30), Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _meshEdgePaint = new SKPaint { Color = new SKColor(100, 200, 255, 100), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
        
        // Handle paints
        private readonly SKPaint _handleFillPaint = new SKPaint { Color = new SKColor(59, 130, 246), IsAntialias = true, Style = SKPaintStyle.Fill };
        private readonly SKPaint _handleStrokePaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
        private readonly SKPaint _rotateHandlePaint = new SKPaint { Color = new SKColor(139, 92, 246), IsAntialias = true, Style = SKPaintStyle.Fill };
        private readonly SKPaint _borderLinePaint = new SKPaint { Color = new SKColor(59, 130, 246, 150), StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke, PathEffect = SKPathEffect.CreateDash(new float[] { 10, 5 }, 0) };

        // Overlay paints (reusable)
        private readonly SKPaint _overlayBonePaint = new SKPaint { StrokeWidth = 3, IsAntialias = true };
        private readonly SKPaint _overlayJointPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        private readonly SKPaint _overlaySelectedJointPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        private readonly SKPaint _overlayJointStrokePaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
        private readonly SKPaint _overlayMeshFillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        private readonly SKPaint _overlayMeshEdgePaint = new SKPaint { StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
        private readonly SKPaint _overlayVertexPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        
        // Texture Mesh Paint
        private SKPaint _texturePaint;

        // Camera
        private bool _isPanning = false;
        private SKPoint _lastPanPos = SKPoint.Empty;

        public TemplateRiggingView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.RequestRedraw += (s, args) => SKCanvasView.InvalidateVisual();
            }
        }

        private void SKCanvasView_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (ViewModel == null) return;

            int width = e.Info.Width;
            int height = e.Info.Height;

            // Apply camera transform
            canvas.Save();
            canvas.Scale(ViewModel.CameraScale);
            canvas.Translate(ViewModel.CameraOffset.X / ViewModel.CameraScale,
                           ViewModel.CameraOffset.Y / ViewModel.CameraScale);

            // Render sprite
            if (ViewModel.LoadedSprite != null)
            {
                float scale = Math.Min((float)width / ViewModel.LoadedSprite.Width,
                                      (float)height / ViewModel.LoadedSprite.Height) * 0.8f;
                float offsetX = (width - ViewModel.LoadedSprite.Width * scale) / 2f;
                float offsetY = (height - ViewModel.LoadedSprite.Height * scale) / 2f;

                canvas.Save();
                canvas.Translate(offsetX, offsetY);
                canvas.Scale(scale, scale);

                // Draw sprite or deformed mesh
                if (ViewModel.IsTemplateBound && ViewModel.Vertices.Count > 0)
                {
                    // Render DEFORMED MESH (This makes the sprite bend!)
                    RenderMesh(canvas);
                }
                else
                {
                    // Draw static sprite
                    canvas.DrawBitmap(ViewModel.LoadedSprite, SKPoint.Empty);
                }

                // Render skeleton (bones + joints)
                if (ViewModel.Joints.Count > 0)
                {
                    RenderSkeleton(canvas, scale);
                }

                // Draw template overlay if visible
                if (ViewModel.IsOverlayVisible && ViewModel.SelectedTemplate != null)
                {
                    RenderTemplateOverlay(canvas);
                    
                    // Draw interactive handles
                    if (ViewModel.ShowOverlayHandles)
                    {
                        RenderOverlayHandles(canvas);
                    }
                }

                canvas.Restore();
            }

            canvas.Restore(); // Camera transform
        }

        private void RenderSkeleton(SKCanvas canvas, float scale)
        {
            // Draw bones
            foreach (var joint in ViewModel.Joints)
            {
                if (joint.Parent != null)
                {
                    canvas.DrawLine(joint.Parent.Position, joint.Position, _bonePaint);
                }
            }

            // Draw joints (all use same paint - no anchor distinction in IK system)
            foreach (var joint in ViewModel.Joints)
            {
                canvas.DrawCircle(joint.Position, 5 / scale, _jointPaint);
            }
        }

        private void RenderMesh(SKCanvas canvas)
        {
            if (ViewModel.LoadedSprite == null) return;

            // Initialize texture paint if needed (or if sprite changed)
            if (_texturePaint == null || _texturePaint.Shader == null)
            {
                _texturePaint = new SKPaint
                {
                    IsAntialias = true,
                    FilterQuality = SKFilterQuality.Medium // Smooth scaling
                };
                
                // Create shader from the sprite
                // TileMode.Clamp ensures edges don't repeat weirdly
                _texturePaint.Shader = SKShader.CreateBitmap(
                    ViewModel.LoadedSprite, 
                    SKShaderTileMode.Clamp, 
                    SKShaderTileMode.Clamp);
            }

            // Prepare SkiaSharp data structures for DrawVertices
            // PERFORMANCE NOTE: In production, consider caching arrays if vertex count is huge.
            // For 2D sprites (usually < 500 verts), doing this every frame 60fps is fine on GPU.
            
            var positions = ViewModel.Vertices.Select(v => v.CurrentPosition).ToArray();
            var texCoords = ViewModel.Vertices.Select(v => v.TextureCoordinate).ToArray();
            
            // Convert triangle list to indices array
            var indices = new List<ushort>();
            foreach (var tri in ViewModel.Triangles)
            {
                indices.Add((ushort)tri.V1.Id);
                indices.Add((ushort)tri.V2.Id);
                indices.Add((ushort)tri.V3.Id);
            }

            // Draw the textured mesh!
            // SKVertexMode.Triangles means every 3 indices form a triangle
            using (var vertices = SKVertices.CreateCopy(
                SKVertexMode.Triangles, 
                positions, 
                texCoords, 
                null, // No colors (using texture)
                indices.ToArray()))
            {
                canvas.DrawVertices(vertices, SKBlendMode.Modulate, _texturePaint);
            }

            // Optional: Draw wireframe overlay for debugging if needed (commented out for clean look)
            // canvas.DrawVertices(vertices, SKBlendMode.SrcOver, _meshEdgePaint);
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null) return;

            Point wpfPos = e.GetPosition(SKCanvasView);
            SKPoint worldPos = ScreenToWorld(new SKPoint((float)wpfPos.X, (float)wpfPos.Y));

            ViewModel.OnCanvasLeftClicked(worldPos);
            SKCanvasView.InvalidateVisual();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (ViewModel == null) return;

            Point wpfPos = e.GetPosition(SKCanvasView);
            SKPoint currentPos = new SKPoint((float)wpfPos.X, (float)wpfPos.Y);

            // Handle panning
            if (_isPanning && e.MiddleButton == MouseButtonState.Pressed)
            {
                SKPoint delta = new SKPoint(currentPos.X - _lastPanPos.X, currentPos.Y - _lastPanPos.Y);
                ViewModel.CameraOffset = new SKPoint(ViewModel.CameraOffset.X + delta.X, ViewModel.CameraOffset.Y + delta.Y);
                _lastPanPos = currentPos;
                SKCanvasView.InvalidateVisual();
                return;
            }

            // Normal mouse move
            SKPoint worldPos = ScreenToWorld(currentPos);
            ViewModel.OnCanvasMouseMoved(worldPos);
            SKCanvasView.InvalidateVisual();
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null) return;
            ViewModel.OnCanvasLeftReleased();
            SKCanvasView.InvalidateVisual();
        }

        private SKPoint ScreenToWorld(SKPoint screenPos)
        {
            if (ViewModel?.LoadedSprite == null)
                return screenPos;

            int width = (int)SKCanvasView.ActualWidth;
            int height = (int)SKCanvasView.ActualHeight;

            float scale = Math.Min((float)width / ViewModel.LoadedSprite.Width,
                                  (float)height / ViewModel.LoadedSprite.Height) * 0.8f;
            float offsetX = (width - ViewModel.LoadedSprite.Width * scale) / 2f;
            float offsetY = (height - ViewModel.LoadedSprite.Height * scale) / 2f;

            return new SKPoint(
                (screenPos.X - offsetX) / scale,
                (screenPos.Y - offsetY) / scale
            );
        }

        private void RenderTemplateOverlay(SKCanvas canvas)
        {
            if (ViewModel.SelectedTemplate == null || ViewModel.LoadedSprite == null) return;

            // PERFORMANCE: Reuse paint objects, just update colors based on opacity
            byte opacity = (byte)(255 * ViewModel.OverlayOpacity);
            _overlayBonePaint.Color = new SKColor(234, 179, 8, opacity);
            _overlayJointPaint.Color = new SKColor(234, 179, 8, opacity);
            _overlaySelectedJointPaint.Color = new SKColor(59, 130, 246, opacity);
            _overlayMeshFillPaint.Color = new SKColor(100, 200, 255, (byte)(80 * ViewModel.OverlayOpacity));
            _overlayMeshEdgePaint.Color = new SKColor(100, 200, 255, (byte)(180 * ViewModel.OverlayOpacity));
            _overlayVertexPaint.Color = new SKColor(59, 130, 246, (byte)(200 * ViewModel.OverlayOpacity));

            // === RENDER FROM OVERLAY JOINTS (User-adjusted positions) ===
            if (ViewModel.OverlayJoints.Count > 0)
            {
                // Draw bones
                foreach (var joint in ViewModel.OverlayJoints)
                {
                    if (joint.Parent != null)
                    {
                        canvas.DrawLine(joint.Parent.Position, joint.Position, _overlayBonePaint);
                    }
                }

                // Draw joints
                foreach (var joint in ViewModel.OverlayJoints)
                {
                    bool isSelected = (joint == ViewModel.SelectedOverlayJoint);
                    var fillPaint = isSelected ? _overlaySelectedJointPaint : _overlayJointPaint;
                    
                    // Draw larger circle for selected joint
                    float radius = isSelected ? 8f : 6f;
                    canvas.DrawCircle(joint.Position, radius, fillPaint);
                    canvas.DrawCircle(joint.Position, radius, _overlayJointStrokePaint);
                    
                    // Draw joint name
                    if (!string.IsNullOrEmpty(joint.Name))
                    {
                        var textPaint = new SKPaint
                        {
                            Color = new SKColor(255, 255, 255, (byte)(200 * ViewModel.OverlayOpacity)),
                            TextSize = 10,
                            IsAntialias = true
                        };
                        canvas.DrawText(joint.Name, joint.Position.X + 10, joint.Position.Y - 5, textPaint);
                    }
                }
            }
            
            // PERFORMANCE: Mesh rendering removed from overlay - not needed during fitting!
            // Skeleton + joints are sufficient for template positioning
        }

        private SKRectI DetectSpriteBounds(SKBitmap sprite)
        {
            int minX = sprite.Width;
            int minY = sprite.Height;
            int maxX = 0;
            int maxY = 0;

            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    var pixel = sprite.GetPixel(x, y);
                    if (pixel.Alpha > 10)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (minX > maxX || minY > maxY)
                return new SKRectI(0, 0, sprite.Width, sprite.Height);

            return new SKRectI(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Render interactive overlay manipulation handles (drag, scale, rotate).
        /// </summary>
        private void RenderOverlayHandles(SKCanvas canvas)
        {
            var handles = ViewModel.GetOverlayHandles();
            if (handles == null) return;

            float handleRadius = 10f;

            // Draw bounding box (dashed border)
            using (var path = new SKPath())
            {
                path.MoveTo(handles.TopLeft);
                path.LineTo(handles.TopRight);
                path.LineTo(handles.BottomRight);
                path.LineTo(handles.BottomLeft);
                path.Close();
                canvas.DrawPath(path, _borderLinePaint);
            }

            // Draw connection line to rotation handle
            canvas.DrawLine(
                new SKPoint(handles.Center.X, handles.TopLeft.Y),
                handles.Rotate,
                _borderLinePaint
            );

            // Draw corner scale handles
            DrawHandle(canvas, handles.TopLeft, handleRadius, _handleFillPaint, _handleStrokePaint, isSquare: true);
            DrawHandle(canvas, handles.TopRight, handleRadius, _handleFillPaint, _handleStrokePaint, isSquare: true);
            DrawHandle(canvas, handles.BottomLeft, handleRadius, _handleFillPaint, _handleStrokePaint, isSquare: true);
            DrawHandle(canvas, handles.BottomRight, handleRadius, _handleFillPaint, _handleStrokePaint, isSquare: true);

            // Draw rotation handle (circular, purple)
            DrawHandle(canvas, handles.Rotate, handleRadius * 1.2f, _rotateHandlePaint, _handleStrokePaint, isSquare: false);

            // Draw rotation icon (small arc)
            using (var arcPath = new SKPath())
            {
                arcPath.AddArc(
                    new SKRect(handles.Rotate.X - 5, handles.Rotate.Y - 5, handles.Rotate.X + 5, handles.Rotate.Y + 5),
                    -30, 240
                );
                canvas.DrawPath(arcPath, _handleStrokePaint);
            }
        }

        /// <summary>
        /// Draw a single handle (square or circle).
        /// </summary>
        private void DrawHandle(SKCanvas canvas, SKPoint position, float size, SKPaint fillPaint, SKPaint strokePaint, bool isSquare)
        {
            if (isSquare)
            {
                canvas.DrawRect(position.X - size / 2, position.Y - size / 2, size, size, fillPaint);
                canvas.DrawRect(position.X - size / 2, position.Y - size / 2, size, size, strokePaint);
            }
            else
            {
                canvas.DrawCircle(position, size / 2, fillPaint);
                canvas.DrawCircle(position, size / 2, strokePaint);
            }
        }

        // Camera controls
        // Camera controls
        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel == null) return;

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Zoom
                float zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
                ViewModel.CameraScale *= zoomFactor;
                ViewModel.CameraScale = Math.Clamp(ViewModel.CameraScale, 0.1f, 10f);

                SKCanvasView.InvalidateVisual();
                e.Handled = true;
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _lastPanPos = new SKPoint((float)e.GetPosition(SKCanvasView).X, (float)e.GetPosition(SKCanvasView).Y);
                SKCanvasView.CaptureMouse();
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Released)
            {
                _isPanning = false;
                SKCanvasView.ReleaseMouseCapture();
            }
        }
    }
}
