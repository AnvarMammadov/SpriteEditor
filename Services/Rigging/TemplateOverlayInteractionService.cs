using System;
using SkiaSharp;

namespace SpriteEditor.Services.Rigging
{
    /// <summary>
    /// Service for interactive overlay template manipulation.
    /// Handles drag-to-move, corner scaling, rotation, and individual joint adjustment.
    /// </summary>
    public class TemplateOverlayInteractionService
    {
        // Handle types for user interaction
        public enum HandleType
        {
            None,
            Move,              // Center drag - move entire overlay
            ScaleTopLeft,
            ScaleTopRight,
            ScaleBottomLeft,
            ScaleBottomRight,
            Rotate,            // Top center - rotation handle
            Joint              // Individual joint adjustment
        }

        // Interaction state
        public HandleType ActiveHandle { get; private set; } = HandleType.None;
        public SKPoint DragStartPos { get; private set; }
        public SKPoint OverlayStartPos { get; private set; }
        public float OverlayStartScale { get; private set; }
        public float OverlayStartRotation { get; private set; }
        public int SelectedJointIndex { get; private set; } = -1;

        // Configuration
        public float HandleRadius { get; set; } = 12f;  // Visual handle size
        public float RotationHandleOffset { get; set; } = 40f;  // Distance above overlay
        public bool ShowHandles { get; set; } = true;

        /// <summary>
        /// Start interaction - detects which handle was clicked.
        /// </summary>
        public void BeginInteraction(
            SKPoint mousePos,
            SKPoint overlayCenter,
            float overlayScale,
            float overlayRotation,
            SKRect overlayBounds)
        {
            DragStartPos = mousePos;
            OverlayStartPos = overlayCenter;
            OverlayStartScale = overlayScale;
            OverlayStartRotation = overlayRotation;

            // Hit test handles (priority order: rotate > scale > move)
            ActiveHandle = HitTestHandles(mousePos, overlayCenter, overlayScale, overlayBounds);
        }

        /// <summary>
        /// Update interaction based on mouse movement.
        /// Returns updated overlay transform.
        /// </summary>
        public OverlayTransform UpdateInteraction(
            SKPoint currentMousePos,
            SKPoint overlayCenter,
            float overlayScale,
            float overlayRotation)
        {
            if (ActiveHandle == HandleType.None)
                return new OverlayTransform(overlayCenter, overlayScale, overlayRotation);

            SKPoint delta = new SKPoint(
                currentMousePos.X - DragStartPos.X,
                currentMousePos.Y - DragStartPos.Y
            );

            switch (ActiveHandle)
            {
                case HandleType.Move:
                    // Simple translation
                    return new OverlayTransform(
                        new SKPoint(OverlayStartPos.X + delta.X, OverlayStartPos.Y + delta.Y),
                        overlayScale,
                        overlayRotation
                    );

                case HandleType.ScaleTopLeft:
                case HandleType.ScaleTopRight:
                case HandleType.ScaleBottomLeft:
                case HandleType.ScaleBottomRight:
                    // Scale based on distance from center
                    float distStart = Distance(DragStartPos, OverlayStartPos);
                    float distCurrent = Distance(currentMousePos, OverlayStartPos);
                    
                    if (distStart > 1f)
                    {
                        float scaleMultiplier = distCurrent / distStart;
                        float newScale = OverlayStartScale * scaleMultiplier;
                        newScale = Math.Clamp(newScale, 0.1f, 5.0f); // Reasonable limits
                        
                        return new OverlayTransform(overlayCenter, newScale, overlayRotation);
                    }
                    break;

                case HandleType.Rotate:
                    // Calculate rotation angle
                    float angleStart = MathF.Atan2(
                        DragStartPos.Y - OverlayStartPos.Y,
                        DragStartPos.X - OverlayStartPos.X
                    );
                    float angleCurrent = MathF.Atan2(
                        currentMousePos.Y - OverlayStartPos.Y,
                        currentMousePos.X - OverlayStartPos.X
                    );
                    float angleDelta = angleCurrent - angleStart;
                    
                    return new OverlayTransform(
                        overlayCenter,
                        overlayScale,
                        OverlayStartRotation + angleDelta
                    );
            }

            return new OverlayTransform(overlayCenter, overlayScale, overlayRotation);
        }

        /// <summary>
        /// End interaction.
        /// </summary>
        public void EndInteraction()
        {
            ActiveHandle = HandleType.None;
            SelectedJointIndex = -1;
        }

        /// <summary>
        /// Hit test to determine which handle was clicked.
        /// </summary>
        private HandleType HitTestHandles(
            SKPoint mousePos,
            SKPoint overlayCenter,
            float overlayScale,
            SKRect overlayBounds)
        {
            float handleRadiusSq = HandleRadius * HandleRadius;

            // Test rotation handle (top center)
            SKPoint rotateHandlePos = new SKPoint(
                overlayCenter.X,
                overlayCenter.Y - overlayBounds.Height * overlayScale * 0.5f - RotationHandleOffset
            );
            if (DistanceSq(mousePos, rotateHandlePos) < handleRadiusSq)
                return HandleType.Rotate;

            // Test corner scale handles
            float halfWidth = overlayBounds.Width * overlayScale * 0.5f;
            float halfHeight = overlayBounds.Height * overlayScale * 0.5f;

            SKPoint[] corners = new[]
            {
                new SKPoint(overlayCenter.X - halfWidth, overlayCenter.Y - halfHeight),  // Top-left
                new SKPoint(overlayCenter.X + halfWidth, overlayCenter.Y - halfHeight),  // Top-right
                new SKPoint(overlayCenter.X - halfWidth, overlayCenter.Y + halfHeight),  // Bottom-left
                new SKPoint(overlayCenter.X + halfWidth, overlayCenter.Y + halfHeight)   // Bottom-right
            };

            HandleType[] cornerHandles = new[]
            {
                HandleType.ScaleTopLeft,
                HandleType.ScaleTopRight,
                HandleType.ScaleBottomLeft,
                HandleType.ScaleBottomRight
            };

            for (int i = 0; i < corners.Length; i++)
            {
                if (DistanceSq(mousePos, corners[i]) < handleRadiusSq)
                    return cornerHandles[i];
            }

            // Test move handle (center area)
            SKRect centerRect = new SKRect(
                overlayCenter.X - halfWidth,
                overlayCenter.Y - halfHeight,
                overlayCenter.X + halfWidth,
                overlayCenter.Y + halfHeight
            );

            if (centerRect.Contains(mousePos))
                return HandleType.Move;

            return HandleType.None;
        }

        /// <summary>
        /// Get handle positions for rendering.
        /// </summary>
        public HandlePositions GetHandlePositions(
            SKPoint overlayCenter,
            float overlayScale,
            SKRect overlayBounds)
        {
            float halfWidth = overlayBounds.Width * overlayScale * 0.5f;
            float halfHeight = overlayBounds.Height * overlayScale * 0.5f;

            return new HandlePositions
            {
                Center = overlayCenter,
                TopLeft = new SKPoint(overlayCenter.X - halfWidth, overlayCenter.Y - halfHeight),
                TopRight = new SKPoint(overlayCenter.X + halfWidth, overlayCenter.Y - halfHeight),
                BottomLeft = new SKPoint(overlayCenter.X - halfWidth, overlayCenter.Y + halfHeight),
                BottomRight = new SKPoint(overlayCenter.X + halfWidth, overlayCenter.Y + halfHeight),
                Rotate = new SKPoint(overlayCenter.X, overlayCenter.Y - halfHeight - RotationHandleOffset)
            };
        }

        private float Distance(SKPoint a, SKPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private float DistanceSq(SKPoint a, SKPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }

    /// <summary>
    /// Overlay transformation result.
    /// </summary>
    public class OverlayTransform
    {
        public SKPoint Position { get; }
        public float Scale { get; }
        public float Rotation { get; }

        public OverlayTransform(SKPoint position, float scale, float rotation)
        {
            Position = position;
            Scale = scale;
            Rotation = rotation;
        }
    }

    /// <summary>
    /// Visual handle positions for rendering.
    /// </summary>
    public class HandlePositions
    {
        public SKPoint Center { get; set; }
        public SKPoint TopLeft { get; set; }
        public SKPoint TopRight { get; set; }
        public SKPoint BottomLeft { get; set; }
        public SKPoint BottomRight { get; set; }
        public SKPoint Rotate { get; set; }
    }
}
