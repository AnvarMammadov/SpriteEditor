using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SixLabors.ImageSharp.PixelFormats;
using SpriteEditor.Helpers;
using SpriteEditor.Services;
using SpriteEditor.Views;
using WpfColor = System.Windows.Media.Color;

namespace SpriteEditor.ViewModels
{
    /// <summary>
    /// Tool modes for Background Eraser
    /// </summary>
    public enum EraserToolMode
    {
        Pipette,        // Color picker
        ManualEraser    // Brush eraser
    }

    /// <summary>
    /// Professional Background Eraser ViewModel
    /// </summary>
    public partial class BackgroundEraserViewModel : ObservableObject
    {
        // ====================================================================
        // SERVICES
        // ====================================================================

        private readonly BackgroundRemovalService _bgRemovalService;

        // Windows Shell Notifier
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
        private const int SHCNE_CREATE = 0x00000002;
        private const int SHCNF_PATH = 0x0001;

        // ====================================================================
        // PROPERTIES
        // ====================================================================

        // Image Loading
        [ObservableProperty]
        private WriteableBitmap _loadedImageSource;

        private string _loadedImagePath;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ProcessCommand))]
        [NotifyCanExecuteChangedFor(nameof(RefreshImageCommand))]
        private bool _isImageLoaded = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ProcessCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveImageCommand))]
        [NotifyCanExecuteChangedFor(nameof(InvertCommand))]
        [NotifyCanExecuteChangedFor(nameof(ApplyToSourceCommand))]
        private bool _isProcessing = false;

        // Preview
        [ObservableProperty]
        private BitmapImage _previewImageSource;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveImageCommand))]
        [NotifyCanExecuteChangedFor(nameof(InvertCommand))]
        [NotifyCanExecuteChangedFor(nameof(ApplyToSourceCommand))]
        private byte[] _lastProcessedData;

        // Tool Mode
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowManualEraserOptions))]
        private EraserToolMode _currentToolMode = EraserToolMode.Pipette;

        public bool ShowManualEraserOptions => CurrentToolMode == EraserToolMode.ManualEraser;

        [ObservableProperty]
        private int _brushSize = 20;

        // Removal Method
        public ObservableCollection<string> RemovalMethods { get; } = new ObservableCollection<string>
        {
            "Flood Fill (Magic Wand)",
            "Color Range (Global)",
            "Chroma Key (Green/Blue Screen)",
            "Refine Edges"
        };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowFloodFillOptions))]
        [NotifyPropertyChangedFor(nameof(ShowColorRangeOptions))]
        [NotifyPropertyChangedFor(nameof(ShowChromaKeyOptions))]
        [NotifyPropertyChangedFor(nameof(ShowRefineEdgesOptions))]
        private string _selectedMethod = "Flood Fill (Magic Wand)";

        // Visibility Helpers
        public bool ShowFloodFillOptions => SelectedMethod == "Flood Fill (Magic Wand)";
        public bool ShowColorRangeOptions => SelectedMethod == "Color Range (Global)";
        public bool ShowChromaKeyOptions => SelectedMethod == "Chroma Key (Green/Blue Screen)";
        public bool ShowRefineEdgesOptions => SelectedMethod == "Refine Edges";

        // Color Selection
        [ObservableProperty]
        private WpfColor _targetColor = Colors.Green;

        public int StartPixelX { get; set; }
        public int StartPixelY { get; set; }

        // Tolerance
        [ObservableProperty]
        private double _tolerance = 10.0;

        // Edge Options
        [ObservableProperty]
        private int _featherRadius = 0;

        [ObservableProperty]
        private int _smoothRadius = 0;

        // Chroma Key
        public ObservableCollection<string> ChromaColors { get; } = new ObservableCollection<string>
        {
            "Green", "Blue", "Red"
        };

        [ObservableProperty]
        private string _selectedChromaColor = "Green";

        [ObservableProperty]
        private double _spillSuppression = 50.0;

        // Preview Background
        public ObservableCollection<string> PreviewBackgrounds { get; } = new ObservableCollection<string>
        {
            "Transparent (Checkerboard)",
            "White",
            "Black",
            "Custom Color"
        };

        [ObservableProperty]
        private string _selectedPreviewBackground = "Transparent (Checkerboard)";

        [ObservableProperty]
        private WpfColor _customBackgroundColor = Colors.Gray;

        // Batch Processing
        public ObservableCollection<string> BatchFiles { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        private string _batchStatus = "";

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public BackgroundEraserViewModel()
        {
            _bgRemovalService = new BackgroundRemovalService();
        }

        // ====================================================================
        // LOAD IMAGE
        // ====================================================================

        [RelayCommand]
        public void LoadImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files|*.*"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    _loadedImagePath = openDialog.FileName;

                    byte[] fileBytes = File.ReadAllBytes(_loadedImagePath);
                    BitmapImage tempBitmap = new BitmapImage();

                    using (var ms = new MemoryStream(fileBytes))
                    {
                        tempBitmap.BeginInit();
                        tempBitmap.CacheOption = BitmapCacheOption.OnLoad;
                        tempBitmap.StreamSource = ms;
                        tempBitmap.EndInit();
                    }
                    tempBitmap.Freeze();

                    FormatConvertedBitmap formattedBitmap = new FormatConvertedBitmap(
                        tempBitmap,
                        PixelFormats.Bgra32,
                        null,
                        0);

                    LoadedImageSource = new WriteableBitmap(formattedBitmap);
                    PreviewImageSource = null;
                    LastProcessedData = null;
                    IsImageLoaded = true;
                }
                catch (Exception ex)
                {
                    GlobalErrorHandler.LogError(ex, "LoadImage");
                    CustomMessageBox.Show(
                        $"Failed to load image:\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MsgImage.Error);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanRefreshImage))]
        private void RefreshImage()
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(_loadedImagePath);
                BitmapImage tempBitmap = new BitmapImage();

                using (var ms = new MemoryStream(fileBytes))
                {
                    tempBitmap.BeginInit();
                    tempBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    tempBitmap.StreamSource = ms;
                    tempBitmap.EndInit();
                }
                tempBitmap.Freeze();

                FormatConvertedBitmap formattedBitmap = new FormatConvertedBitmap(tempBitmap, PixelFormats.Bgra32, null, 0);
                LoadedImageSource = new WriteableBitmap(formattedBitmap);
                PreviewImageSource = null;
                LastProcessedData = null;
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "RefreshImage");
                CustomMessageBox.Show(
                    $"Failed to refresh image:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MsgImage.Error);
                IsImageLoaded = false;
            }
        }

        private bool CanRefreshImage() => IsImageLoaded && !IsProcessing;

        // ====================================================================
        // PROCESS IMAGE
        // ====================================================================

        [RelayCommand(CanExecute = nameof(CanProcess))]
        public async Task Process()
        {
            if (!CanProcess()) return;

            IsProcessing = true;
            LastProcessedData = null;

            try
            {
                byte[] currentImageData = GetPngBytesFromWriteableBitmap(LoadedImageSource);
                if (currentImageData == null)
                    throw new Exception("Failed to get image data!");

                byte[] resultData = null;

                await Task.Run(() =>
                {
                    switch (SelectedMethod)
                    {
                        case "Flood Fill (Magic Wand)":
                            resultData = _bgRemovalService.RemoveBackgroundFloodFill(
                                currentImageData,
                                StartPixelX,
                                StartPixelY,
                                (float)Tolerance,
                                FeatherRadius);
                            break;

                        case "Color Range (Global)":
                            var targetRgba = new Rgba32(TargetColor.R, TargetColor.G, TargetColor.B, TargetColor.A);
                            resultData = _bgRemovalService.RemoveBackgroundColorRange(
                                currentImageData,
                                targetRgba,
                                (float)Tolerance);
                            break;

                        case "Chroma Key (Green/Blue Screen)":
                            var chromaColor = SelectedChromaColor switch
                            {
                                "Green" => ChromaKeyColor.Green,
                                "Blue" => ChromaKeyColor.Blue,
                                "Red" => ChromaKeyColor.Red,
                                _ => ChromaKeyColor.Green
                            };
                            resultData = _bgRemovalService.RemoveBackgroundChromaKey(
                                currentImageData,
                                chromaColor,
                                (float)Tolerance,
                                (float)(SpillSuppression / 100.0));
                            break;

                        case "Refine Edges":
                            if (LastProcessedData == null)
                            {
                                throw new Exception("Please process the image first before refining edges!");
                            }
                            resultData = _bgRemovalService.RefineEdges(
                                LastProcessedData,
                                SmoothRadius,
                                FeatherRadius);
                            break;
                    }
                });

                if (resultData != null)
                {
                    var previewBitmap = new BitmapImage();
                    using (var ms = new MemoryStream(resultData))
                    {
                        previewBitmap.BeginInit();
                        previewBitmap.CacheOption = BitmapCacheOption.OnLoad;
                        previewBitmap.StreamSource = ms;
                        previewBitmap.EndInit();
                    }
                    previewBitmap.Freeze();

                    PreviewImageSource = previewBitmap;
                    LastProcessedData = resultData;
                }
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "Process");
                CustomMessageBox.Show(
                    $"Processing failed:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MsgImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private bool CanProcess() => IsImageLoaded && !IsProcessing;

        // ====================================================================
        // APPLY TO SOURCE (Real-time editing)
        // ====================================================================

        [RelayCommand(CanExecute = nameof(CanApplyToSource))]
        public void ApplyToSource()
        {
            if (LastProcessedData == null) return;

            try
            {
                // Convert processed data to WriteableBitmap
                BitmapImage tempBitmap = new BitmapImage();
                using (var ms = new MemoryStream(LastProcessedData))
                {
                    tempBitmap.BeginInit();
                    tempBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    tempBitmap.StreamSource = ms;
                    tempBitmap.EndInit();
                }
                tempBitmap.Freeze();

                FormatConvertedBitmap formattedBitmap = new FormatConvertedBitmap(
                    tempBitmap,
                    PixelFormats.Bgra32,
                    null,
                    0);

                LoadedImageSource = new WriteableBitmap(formattedBitmap);
                PreviewImageSource = null;
                LastProcessedData = null;

                CustomMessageBox.Show(
                    "Result applied to source! You can now continue editing.",
                    "Success",
                    MessageBoxButton.OK,
                    MsgImage.Success);
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "ApplyToSource");
                CustomMessageBox.Show(
                    $"Failed to apply result:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MsgImage.Error);
            }
        }

        private bool CanApplyToSource() => LastProcessedData != null && !IsProcessing;

        // ====================================================================
        // INVERT SELECTION
        // ====================================================================

        [RelayCommand(CanExecute = nameof(CanInvert))]
        public async Task Invert()
        {
            if (LastProcessedData == null) return;

            IsProcessing = true;

            try
            {
                byte[] invertedData = await Task.Run(() =>
                    _bgRemovalService.InvertSelection(LastProcessedData));

                var previewBitmap = new BitmapImage();
                using (var ms = new MemoryStream(invertedData))
                {
                    previewBitmap.BeginInit();
                    previewBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    previewBitmap.StreamSource = ms;
                    previewBitmap.EndInit();
                }
                previewBitmap.Freeze();

                PreviewImageSource = previewBitmap;
                LastProcessedData = invertedData;
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "Invert");
                CustomMessageBox.Show(
                    $"Invert failed:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MsgImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private bool CanInvert() => LastProcessedData != null && !IsProcessing;

        // ====================================================================
        // SAVE IMAGE
        // ====================================================================

        [RelayCommand(CanExecute = nameof(CanSave))]
        public void SaveImage()
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                FileName = "background_removed.png",
                Filter = "PNG Image (*.png)|*.png"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(saveDialog.FileName, LastProcessedData);
                    SHChangeNotify(SHCNE_CREATE, SHCNF_PATH, Marshal.StringToHGlobalAuto(saveDialog.FileName), IntPtr.Zero);
                    CustomMessageBox.Show(
                        $"Image saved successfully!\n\nSaved to: {saveDialog.FileName}",
                        "Success",
                        MessageBoxButton.OK,
                        MsgImage.Success);
                }
                catch (Exception ex)
                {
                    GlobalErrorHandler.LogError(ex, "SaveImage");
                    CustomMessageBox.Show(
                        $"Failed to save image:\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MsgImage.Error);
                }
            }
        }

        private bool CanSave() => LastProcessedData != null && !IsProcessing;

        // ====================================================================
        // BATCH PROCESSING
        // ====================================================================

        [RelayCommand]
        private void AddBatchFiles()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.webp"
            };

            if (openDialog.ShowDialog() == true)
            {
                foreach (var file in openDialog.FileNames)
                {
                    if (!BatchFiles.Contains(file))
                    {
                        BatchFiles.Add(file);
                    }
                }
                BatchStatus = $"{BatchFiles.Count} files ready";
            }
        }

        [RelayCommand]
        private void ClearBatchFiles()
        {
            BatchFiles.Clear();
            BatchStatus = "";
        }

        [RelayCommand(CanExecute = nameof(CanBatchProcess))]
        private async Task BatchProcess()
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Select output folder (file name will be ignored)",
                FileName = "Select Folder",
                Filter = "Folder|*.none"
            };

            if (saveDialog.ShowDialog() == true)
            {
                string outputFolder = Path.GetDirectoryName(saveDialog.FileName);
                IsProcessing = true;
                int successCount = 0;
                int failCount = 0;

                try
                {
                    foreach (var file in BatchFiles)
                    {
                        try
                        {
                            byte[] fileData = File.ReadAllBytes(file);
                            byte[] resultData = null;

                            await Task.Run(() =>
                            {
                                // Use Color Range method for batch (fastest)
                                var targetRgba = new Rgba32(TargetColor.R, TargetColor.G, TargetColor.B, TargetColor.A);
                                resultData = _bgRemovalService.RemoveBackgroundColorRange(
                                    fileData,
                                    targetRgba,
                                    (float)Tolerance);
                            });

                            string fileName = Path.GetFileNameWithoutExtension(file);
                            string outputPath = Path.Combine(outputFolder, $"{fileName}_nobg.png");
                            File.WriteAllBytes(outputPath, resultData);
                            successCount++;

                            BatchStatus = $"Processing... {successCount}/{BatchFiles.Count}";
                        }
                        catch (Exception ex)
                        {
                            GlobalErrorHandler.LogError(ex, $"BatchProcess: {file}");
                            failCount++;
                        }
                    }

                    CustomMessageBox.Show(
                        $"Batch processing complete!\n\nSuccess: {successCount}\nFailed: {failCount}",
                        "Batch Processing",
                        MessageBoxButton.OK,
                        successCount == BatchFiles.Count ? MsgImage.Success : MsgImage.Warning);

                    BatchFiles.Clear();
                    BatchStatus = "";
                }
                finally
                {
                    IsProcessing = false;
                }
            }
        }

        private bool CanBatchProcess() => BatchFiles.Count > 0 && !IsProcessing;

        // ====================================================================
        // HELPER METHODS
        // ====================================================================

        private byte[] GetPngBytesFromWriteableBitmap(WriteableBitmap wb)
        {
            if (wb == null) return null;

            try
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(wb));
                using (MemoryStream ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            Tolerance = 10.0;
            FeatherRadius = 0;
            SmoothRadius = 0;
            SpillSuppression = 50.0;
            SelectedMethod = "Flood Fill (Magic Wand)";
            SelectedChromaColor = "Green";
            BrushSize = 20;
            CurrentToolMode = EraserToolMode.Pipette;
        }
    }
}
