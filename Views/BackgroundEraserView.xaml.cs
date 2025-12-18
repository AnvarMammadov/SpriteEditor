using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views
{
    public partial class BackgroundEraserView : UserControl
    {
        private BackgroundEraserViewModel _viewModel;
        private bool _isErasing = false;

        public BackgroundEraserView()
        {
            InitializeComponent();

            this.DataContextChanged += (sender, e) =>
            {
                _viewModel = e.NewValue as BackgroundEraserViewModel;
            };
        }

        /// <summary>
        /// Handle mouse click on original image
        /// </summary>
        private void OriginalImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null || _viewModel.LoadedImageSource == null) return;

            // Check tool mode
            if (_viewModel.CurrentToolMode == EraserToolMode.Pipette)
            {
                // Pipette mode - select color
                SelectColor(sender, e);
            }
            else if (_viewModel.CurrentToolMode == EraserToolMode.ManualEraser)
            {
                // Manual Eraser mode - start erasing
                _isErasing = true;
                EraseAtPoint(sender, e);
                (sender as IInputElement)?.CaptureMouse();
            }
        }

        /// <summary>
        /// Handle mouse move for manual eraser
        /// </summary>
        private void OriginalImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isErasing || _viewModel.CurrentToolMode != EraserToolMode.ManualEraser)
                return;

            EraseAtPoint(sender, e);
        }

        /// <summary>
        /// Handle mouse release
        /// </summary>
        private void OriginalImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isErasing = false;
            (sender as IInputElement)?.ReleaseMouseCapture();
        }

        /// <summary>
        /// Select color from image (Pipette tool)
        /// </summary>
        private void SelectColor(object sender, MouseButtonEventArgs e)
        {
            var imageControl = sender as Image;
            var bitmapSource = imageControl.Source as BitmapSource;
            if (bitmapSource == null) return;

            // Get click position
            Point clickPos = e.GetPosition(imageControl);
            
            // Convert to pixel coordinates
            int pixelX = (int)(clickPos.X / imageControl.ActualWidth * bitmapSource.PixelWidth);
            int pixelY = (int)(clickPos.Y / imageControl.ActualHeight * bitmapSource.PixelHeight);

            // Bounds check
            if (pixelX < 0 || pixelX >= bitmapSource.PixelWidth ||
                pixelY < 0 || pixelY >= bitmapSource.PixelHeight)
            {
                return;
            }

            // Get pixel color
            try
            {
                CroppedBitmap cb = new CroppedBitmap(bitmapSource, new Int32Rect(pixelX, pixelY, 1, 1));
                byte[] pixels = new byte[4];
                cb.CopyPixels(pixels, 4, 0);

                // Update ViewModel
                _viewModel.TargetColor = Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
                _viewModel.StartPixelX = pixelX;
                _viewModel.StartPixelY = pixelY;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Click error: {ex.Message}");
            }
        }

        /// <summary>
        /// Erase pixels at mouse position (Manual Eraser tool)
        /// </summary>
        private void EraseAtPoint(object sender, MouseEventArgs e)
        {
            var imageControl = sender as Image;
            var wb = _viewModel.LoadedImageSource as WriteableBitmap;
            if (wb == null) return;

            // Get click position
            Point clickPos = e.GetPosition(imageControl);
            
            // Convert to pixel coordinates
            int pixelX = (int)(clickPos.X / imageControl.ActualWidth * wb.PixelWidth);
            int pixelY = (int)(clickPos.Y / imageControl.ActualHeight * wb.PixelHeight);

            // Get brush size
            int brushSize = _viewModel.BrushSize;
            int halfBrush = brushSize / 2;

            // Calculate erase area
            int startX = Math.Max(0, pixelX - halfBrush);
            int startY = Math.Max(0, pixelY - halfBrush);
            int endX = Math.Min(wb.PixelWidth, pixelX + halfBrush);
            int endY = Math.Min(wb.PixelHeight, pixelY + halfBrush);

            int width = endX - startX;
            int height = endY - startY;

            if (width <= 0 || height <= 0) return;

            // Create transparent pixel data
            int bytesPerPixel = wb.Format.BitsPerPixel / 8;
            int stride = width * bytesPerPixel;
            byte[] transparentData = new byte[height * stride];

            // Write transparent pixels
            try
            {
                wb.Lock();
                Int32Rect rect = new Int32Rect(startX, startY, width, height);
                wb.WritePixels(rect, transparentData, stride, 0);
            }
            finally
            {
                wb.Unlock();
            }
        }
    }
}
