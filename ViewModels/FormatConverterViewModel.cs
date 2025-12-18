using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using Microsoft.Win32;
using SpriteEditor.Helpers;
using SpriteEditor.Views;

namespace SpriteEditor.ViewModels
{
    /// <summary>
    /// Professional Format Converter - Supports all major image formats
    /// </summary>
    public partial class FormatConverterViewModel : ObservableObject
    {
        // ====================================================================
        // PROPERTIES
        // ====================================================================

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
        [NotifyCanExecuteChangedFor(nameof(BatchConvertCommand))]
        private bool _isImageLoaded;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
        private bool _isProcessing;

        [ObservableProperty]
        private string _sourcePath;

        [ObservableProperty]
        private BitmapImage _previewSource;

        [ObservableProperty]
        private string _sourceInfo = "No image loaded";

        // Format Settings
        public ObservableCollection<string> AvailableFormats { get; } = new ObservableCollection<string>
        {
            "PNG", "JPG", "JPEG", "WEBP", "BMP", "TIFF", "TGA", "GIF", "ICO", "AVIF", "HEIC", "DDS", "PSD"
        };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowQualitySlider))]
        [NotifyPropertyChangedFor(nameof(ShowIcoSizes))]
        [NotifyPropertyChangedFor(nameof(ShowGifOptions))]
        private string _selectedFormat = "PNG";

        // Quality & Compression
        [ObservableProperty]
        private int _quality = 90; // For JPG/WebP (0-100)

        [ObservableProperty]
        private int _compressionLevel = 6; // For PNG (0-9)

        // Resize Options
        [ObservableProperty]
        private bool _enableResize;

        [ObservableProperty]
        private int _resizeWidth = 1024;

        [ObservableProperty]
        private int _resizeHeight = 1024;

        [ObservableProperty]
        private bool _maintainAspectRatio = true;

        // ICO-specific
        public ObservableCollection<int> IcoSizes { get; } = new ObservableCollection<int> { 16, 32, 48, 64, 128, 256 };
        
        [ObservableProperty]
        private List<int> _selectedIcoSizes = new List<int> { 16, 32, 48, 256 };

        // Advanced Options
        [ObservableProperty]
        private bool _stripMetadata = false;

        [ObservableProperty]
        private bool _optimizeForWeb = true;

        // Batch Conversion
        public ObservableCollection<string> BatchFiles { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        private string _batchStatus = "";

        // UI Visibility Helpers
        public bool ShowQualitySlider => SelectedFormat?.ToUpper() is "JPG" or "JPEG" or "WEBP" or "AVIF";
        public bool ShowIcoSizes => SelectedFormat?.ToUpper() == "ICO";
        public bool ShowGifOptions => SelectedFormat?.ToUpper() == "GIF";

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public FormatConverterViewModel()
        {
            // Initialize with defaults
        }

        // ====================================================================
        // LOAD IMAGE
        // ====================================================================

        [RelayCommand]
        private void LoadImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "All Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.tga;*.ico;*.avif;*.heic;*.tiff;*.gif;*.dds;*.psd|" +
                         "PNG (*.png)|*.png|" +
                         "JPEG (*.jpg,*.jpeg)|*.jpg;*.jpeg|" +
                         "WebP (*.webp)|*.webp|" +
                         "AVIF (*.avif)|*.avif|" +
                         "All Files (*.*)|*.*"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    SourcePath = openDialog.FileName;
                    LoadImagePreview(SourcePath);
                    IsImageLoaded = true;

                    // Get image info
                    using (var image = new MagickImage(SourcePath))
                    {
                        SourceInfo = $"{image.Width}x{image.Height} | {image.Format} | {FormatBytes(image.ToByteArray().Length)}";
                        
                        // Auto-set resize dimensions
                        ResizeWidth = (int)image.Width;
                        ResizeHeight = (int)image.Height;
                    }
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

        private void LoadImagePreview(string path)
        {
            try
            {
                // Use Magick.NET to load any format
                using (var magickImage = new MagickImage(path))
                {
                    // Limit preview size for performance
                    if (magickImage.Width > 1920 || magickImage.Height > 1080)
                    {
                        magickImage.Resize(new MagickGeometry(1920, 1080) { Greater = true });
                    }

                    byte[] imageBytes = magickImage.ToByteArray(MagickFormat.Png);

                    var bitmap = new BitmapImage();
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                    }
                    bitmap.Freeze();
                    PreviewSource = bitmap;
                }
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "LoadImagePreview");
                PreviewSource = null;
            }
        }

        // ====================================================================
        // SINGLE FILE CONVERSION
        // ====================================================================

        [RelayCommand(CanExecute = nameof(CanConvert))]
        private async Task Convert()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            string ext = SelectedFormat.ToLower().Replace("jpeg", "jpg");

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                FileName = $"{Path.GetFileNameWithoutExtension(SourcePath)}_converted.{ext}",
                Filter = $"{SelectedFormat} Image (*.{ext})|*.{ext}|All Files (*.*)|*.*"
            };

            if (saveDialog.ShowDialog() == true)
            {
                IsProcessing = true;
                try
                {
                    await Task.Run(() =>
                    {
                        ConvertImageFile(SourcePath, saveDialog.FileName, SelectedFormat);
                    });

                    CustomMessageBox.Show(
                        $"Image converted successfully!\n\nSaved to: {saveDialog.FileName}",
                        "Success",
                        MessageBoxButton.OK,
                        MsgImage.Success);
                }
                catch (Exception ex)
                {
                    GlobalErrorHandler.LogError(ex, "Convert");
                    CustomMessageBox.Show(
                        $"Conversion failed:\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MsgImage.Error);
                }
                finally
                {
                    IsProcessing = false;
                }
            }
        }

        private bool CanConvert() => IsImageLoaded && !IsProcessing;

        // ====================================================================
        // BATCH CONVERSION
        // ====================================================================

        [RelayCommand]
        private void AddBatchFiles()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "All Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.tga;*.ico;*.avif;*.heic;*.tiff;*.gif"
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
                BatchStatus = $"{BatchFiles.Count} files ready for conversion";
            }
        }

        [RelayCommand]
        private void ClearBatchFiles()
        {
            BatchFiles.Clear();
            BatchStatus = "";
        }

        [RelayCommand(CanExecute = nameof(CanBatchConvert))]
        private async Task BatchConvert()
        {
            // Use first file's directory as default output location
            if (BatchFiles.Count == 0) return;

            string defaultDir = Path.GetDirectoryName(BatchFiles[0]) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            // Ask user to select output folder using SaveFileDialog workaround
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Select output folder (file name will be ignored)",
                FileName = "Select Folder", // Default file name
                Filter = "Folder|*.none", // Dummy filter
                InitialDirectory = defaultDir
            };

            if (saveDialog.ShowDialog() == true)
            {
                string outputFolder = Path.GetDirectoryName(saveDialog.FileName) ?? defaultDir;

                IsProcessing = true;
                int successCount = 0;
                int failCount = 0;

                try
                {
                    await Task.Run(() =>
                    {
                        foreach (var file in BatchFiles)
                        {
                            try
                            {
                                string fileName = Path.GetFileNameWithoutExtension(file);
                                string ext = SelectedFormat.ToLower().Replace("jpeg", "jpg");
                                string outputPath = Path.Combine(outputFolder, $"{fileName}_converted.{ext}");

                                ConvertImageFile(file, outputPath, SelectedFormat);
                                successCount++;

                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    BatchStatus = $"Converting... {successCount}/{BatchFiles.Count}";
                                });
                            }
                            catch (Exception ex)
                            {
                                GlobalErrorHandler.LogError(ex, $"BatchConvert: {file}");
                                failCount++;
                            }
                        }
                    });

                    CustomMessageBox.Show(
                        $"Batch conversion complete!\n\nSuccess: {successCount}\nFailed: {failCount}",
                        "Batch Conversion",
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

        private bool CanBatchConvert() => BatchFiles.Count > 0 && !IsProcessing;

        // ====================================================================
        // CORE CONVERSION LOGIC
        // ====================================================================

        private void ConvertImageFile(string sourcePath, string outputPath, string targetFormat)
        {
            using (var image = new MagickImage(sourcePath))
            {
                // Apply resize if enabled
                if (EnableResize && (ResizeWidth > 0 || ResizeHeight > 0))
                {
                    var geometry = new MagickGeometry((uint)ResizeWidth, (uint)ResizeHeight)
                    {
                        IgnoreAspectRatio = !MaintainAspectRatio
                    };
                    image.Resize(geometry);
                }

                // Strip metadata if requested
                if (StripMetadata)
                {
                    image.Strip();
                }

                // Format-specific settings
                switch (targetFormat.ToUpper())
                {
                    case "JPG":
                    case "JPEG":
                        image.Format = MagickFormat.Jpeg;
                        image.Quality = (uint)Quality;
                        if (OptimizeForWeb)
                        {
                            image.Settings.SetDefine(MagickFormat.Jpeg, "dct-method", "float");
                        }
                        break;

                    case "PNG":
                        image.Format = MagickFormat.Png;
                        image.Quality = (uint)(100 - (CompressionLevel * 10)); // Map 0-9 to quality
                        if (OptimizeForWeb)
                        {
                            image.Settings.SetDefine(MagickFormat.Png, "compression-level", CompressionLevel.ToString());
                        }
                        break;

                    case "WEBP":
                        image.Format = MagickFormat.WebP;
                        image.Quality = (uint)Quality;
                        image.Settings.SetDefine(MagickFormat.WebP, "method", "6"); // Best quality
                        break;

                    case "AVIF":
                        image.Format = MagickFormat.Avif;
                        image.Quality = (uint)Quality;
                        break;

                    case "HEIC":
                        image.Format = MagickFormat.Heic;
                        image.Quality = (uint)Quality;
                        break;

                    case "BMP":
                        image.Format = MagickFormat.Bmp;
                        break;

                    case "TIFF":
                        image.Format = MagickFormat.Tiff;
                        image.Settings.SetDefine(MagickFormat.Tiff, "compression", "lzw");
                        break;

                    case "TGA":
                        image.Format = MagickFormat.Tga;
                        break;

                    case "GIF":
                        image.Format = MagickFormat.Gif;
                        break;

                    case "ICO":
                        CreateIcoFile(image, outputPath);
                        return; // ICO handled separately

                    case "DDS":
                        image.Format = MagickFormat.Dds;
                        break;

                    case "PSD":
                        image.Format = MagickFormat.Psd;
                        break;

                    default:
                        throw new NotSupportedException($"Format '{targetFormat}' is not supported.");
                }

                // Save the image
                image.Write(outputPath);
            }
        }

        private void CreateIcoFile(MagickImage sourceImage, string outputPath)
        {
            using (var collection = new MagickImageCollection())
            {
                // Create multiple sizes for ICO
                var sizes = SelectedIcoSizes ?? new List<int> { 16, 32, 48, 256 };

                foreach (var size in sizes.OrderByDescending(s => s))
                {
                    var clone = sourceImage.Clone();
                    clone.Resize((uint)size, (uint)size);
                    clone.Format = MagickFormat.Ico;
                    collection.Add(clone);
                }

                collection.Write(outputPath);
            }
        }

        // ====================================================================
        // HELPER METHODS
        // ====================================================================

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            Quality = 90;
            CompressionLevel = 6;
            EnableResize = false;
            MaintainAspectRatio = true;
            StripMetadata = false;
            OptimizeForWeb = true;
        }
    }
}
