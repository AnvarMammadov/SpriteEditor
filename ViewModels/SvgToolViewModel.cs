using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using Microsoft.Win32;
using SpriteEditor.Helpers;
using SpriteEditor.Views;

namespace SpriteEditor.ViewModels
{
    public partial class SvgToolViewModel : ObservableObject
    {
        // ==========================================
        // PREVIEW IMAGES & STATE
        // ==========================================
        [ObservableProperty] private ImageSource _fullSvgPreview;
        [ObservableProperty] private bool _isFullSvgMode;

        // ==========================================
        // DATA & APPEARANCE
        // ==========================================
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GeometryPreview))]
        private string _rawPathData;

        [ObservableProperty] private string _fillColor = "#EAB308"; // Default Yellow
        [ObservableProperty] private string _strokeColor = "#00000000"; // Transparent default
        [ObservableProperty] private double _strokeThickness = 0;

        // ==========================================
        // TRANSFORMS
        // ==========================================
        [ObservableProperty] private double _scaleX = 1.0;
        [ObservableProperty] private double _scaleY = 1.0;
        [ObservableProperty] private double _rotation = 0;
        [ObservableProperty] private double _translateX = 0;
        [ObservableProperty] private double _translateY = 0;

        // ==========================================
        // OUTPUTS
        // ==========================================
        [ObservableProperty] private string _xamlOutput;
        [ObservableProperty] private string _htmlOutput;
        [ObservableProperty] private bool _isXamlVisible = true;

        // ==========================================
        // COLOR PALETTE
        // ==========================================
        public List<string> ColorPalette { get; } = new List<string>
        {
            // Monochrome
            "#000000", "#111827", "#374151", "#6B7280", "#9CA3AF", "#D1D5DB", "#F3F4F6", "#FFFFFF",
            // Red / Orange
            "#EF4444", "#F87171", "#F59E0B", "#FBBF24", "#F97316", "#FB923C",
            // Green / Teal
            "#10B981", "#34D399", "#14B8A6", "#2DD4BF", "#0EA5E9", "#38BDF8",
            // Blue / Indigo
            "#3B82F6", "#60A5FA", "#6366F1", "#818CF8", "#8B5CF6", "#A78BFA",
            // Purple / Pink
            "#A855F7", "#C084FC", "#EC4899", "#F472B6", "#F43F5E", "#FB7185"
        };

        // Rəng tətbiqi (Full SVG rejimində deaktiv edilir)
        [RelayCommand]
        private void ApplyFillColor(string color)
        {
            if (!IsFullSvgMode && !string.IsNullOrEmpty(color)) FillColor = color;
        }

        [RelayCommand]
        private void ApplyStrokeColor(string color)
        {
            if (!IsFullSvgMode && !string.IsNullOrEmpty(color)) StrokeColor = color;
        }

        // Preview üçün Geometry (Yalnız Path modu üçün)
        public Geometry GeometryPreview
        {
            get
            {
                if (IsFullSvgMode || string.IsNullOrWhiteSpace(RawPathData)) return null;
                try { return Geometry.Parse(RawPathData); } catch { return null; }
            }
        }

        // ==========================================================
        // INPUT CHANGE HANDLER
        // ==========================================================
        partial void OnRawPathDataChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                IsFullSvgMode = false;
                FullSvgPreview = null;
                return;
            }

            // <svg> teqi ilə başlayırsa, Full SVG rejiminə keç
            if (value.Trim().StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
            {
                IsFullSvgMode = true;
                RenderFullSvgPreview(value);
                HtmlOutput = value;
                XamlOutput = "";
            }
            else
            {
                IsFullSvgMode = false;
                FullSvgPreview = null;
                GenerateCode();
            }
            OnPropertyChanged(nameof(GeometryPreview));
        }

        // Ekranda göstərmək üçün SVG Render (Preview)
        private void RenderFullSvgPreview(string svgContent)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(svgContent);
                // Preview üçün orta keyfiyyət (performans üçün)
                var settings = new MagickReadSettings { BackgroundColor = MagickColors.Transparent, Density = new Density(96) };

                using (var image = new MagickImage(bytes, settings))
                {
                    image.Format = MagickFormat.Png;
                    byte[] pngBytes = image.ToByteArray();

                    var bitmap = new BitmapImage();
                    using (var stream = new MemoryStream(pngBytes))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                    }
                    bitmap.Freeze();
                    FullSvgPreview = bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SVG Preview Error: {ex.Message}");
            }
        }

        // Dəyişiklikləri izlə və kodu yenilə
        partial void OnFillColorChanged(string value) => GenerateCode();
        partial void OnStrokeColorChanged(string value) => GenerateCode();
        partial void OnStrokeThicknessChanged(double value) => GenerateCode();
        partial void OnScaleXChanged(double value) => GenerateCode();
        partial void OnScaleYChanged(double value) => GenerateCode();
        partial void OnRotationChanged(double value) => GenerateCode();
        partial void OnTranslateXChanged(double value) => GenerateCode();
        partial void OnTranslateYChanged(double value) => GenerateCode();


        // ==========================================
        // COMMANDS
        // ==========================================
        [RelayCommand] private void ShowXaml() => IsXamlVisible = true;
        [RelayCommand] private void ShowHtml() => IsXamlVisible = false;

        [RelayCommand]
        private void ResetTransforms()
        {
            ScaleX = 1.0; ScaleY = 1.0; Rotation = 0; TranslateX = 0; TranslateY = 0;
        }

        [RelayCommand]
        private void LoadSvgFile()
        {
            OpenFileDialog openDialog = new OpenFileDialog { Filter = "SVG Files (*.svg)|*.svg" };
            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string content = File.ReadAllText(openDialog.FileName);
                    RawPathData = content;
                }
                catch (Exception ex) { GlobalErrorHandler.LogError(ex, "LoadSvg"); }
            }
        }

        [RelayCommand]
        private void GenerateCode()
        {
            if (IsFullSvgMode || string.IsNullOrWhiteSpace(RawPathData)) return;

            // XAML Output
            XamlOutput = $"<Path Data=\"{RawPathData}\"\n" +
                         $"      Fill=\"{FillColor}\"\n" +
                         $"      Stroke=\"{StrokeColor}\" StrokeThickness=\"{StrokeThickness}\"\n" +
                         $"      Stretch=\"Uniform\">\n" +
                         $"    <Path.RenderTransform>\n" +
                         $"        <TransformGroup>\n" +
                         $"            <ScaleTransform ScaleX=\"{ScaleX:F2}\" ScaleY=\"{ScaleY:F2}\"/>\n" +
                         $"            <RotateTransform Angle=\"{Rotation:F2}\"/>\n" +
                         $"            <TranslateTransform X=\"{TranslateX:F2}\" Y=\"{TranslateY:F2}\"/>\n" +
                         $"        </TransformGroup>\n" +
                         $"    </Path.RenderTransform>\n" +
                         $"</Path>";

            // HTML Output
            HtmlOutput = $"<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">\n" +
                         $"  <g transform=\"translate({TranslateX},{TranslateY}) rotate({Rotation}) scale({ScaleX},{ScaleY})\">\n" +
                         $"    <path d=\"{RawPathData}\" \n" +
                         $"          fill=\"{FillColor}\" \n" +
                         $"          stroke=\"{StrokeColor}\" stroke-width=\"{StrokeThickness}\" />\n" +
                         $"  </g>\n" +
                         $"</svg>";
        }

        // ==========================================
        // EXPORT LOGIC (DÜZƏLDİLMİŞ HİSSƏ)
        // ==========================================
        [RelayCommand]
        private async Task ExportToPng()
        {
            if (string.IsNullOrWhiteSpace(RawPathData)) return;

            SaveFileDialog saveDialog = new SaveFileDialog { Filter = "PNG Image (*.png)|*.png", FileName = "vector_export.png" };
            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // 1. UI Thread-dən dəyərləri əldə edirik (Thread-safety üçün)
                    double currentRot = Rotation;
                    double currentScaleX = ScaleX;
                    double currentScaleY = ScaleY;
                    string currentFill = FillColor;
                    string currentStroke = StrokeColor;
                    double currentThick = StrokeThickness;
                    bool isFull = IsFullSvgMode;
                    string rawData = RawPathData;

                    // 2. Arxa planda emal edirik
                    await Task.Run(() =>
                    {
                        var settings = new MagickReadSettings
                        {
                            Density = new Density(300), // Çap keyfiyyəti (300 DPI)
                            BackgroundColor = MagickColors.Transparent
                        };

                        string svgToRender;

                        if (isFull)
                        {
                            // Full SVG rejimidirsə, olduğu kimi götürürük
                            svgToRender = rawData;
                        }
                        else
                        {
                            // Path rejimidirsə, onu rənglərlə birlikdə SVG içinə qoyuruq
                            // viewBox-u genişləndiririk ki, fırladanda kənarlar kəsilməsin (təxmini 500x500)
                            svgToRender = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"500\" height=\"500\" viewBox=\"0 0 100 100\">" +
                                          $"<path d=\"{rawData}\" fill=\"{currentFill}\" stroke=\"{currentStroke}\" stroke-width=\"{currentThick}\" />" +
                                          $"</svg>";
                        }

                        using (var image = new MagickImage(Encoding.UTF8.GetBytes(svgToRender), settings))
                        {
                            // A) SCALE (Böyütmə/Kiçiltmə)
                            // Orijinal ölçünü alıb əmsala vururuq
                            if (Math.Abs(currentScaleX - 1.0) > 0.01 || Math.Abs(currentScaleY - 1.0) > 0.01)
                            {
                                uint newW = (uint)(image.Width * currentScaleX);
                                uint newH = (uint)(image.Height * currentScaleY);
                                // Aspect Ratio qorunmadan dəqiq ölçü verir
                                image.Resize(new MagickGeometry(newW, newH) { IgnoreAspectRatio = true });
                            }

                            // B) ROTATION (Döndərmə)
                            // Magick.NET dönmə zamanı kətanı avtomatik böyüdür
                            if (Math.Abs(currentRot) > 0.01)
                            {
                                image.BackgroundColor = MagickColors.Transparent;
                                image.Rotate(currentRot);
                            }

                            // C) YADDAŞA YAZ
                            image.Format = MagickFormat.Png;
                            image.Write(saveDialog.FileName);
                        }
                    });

                    // Bitdikdən sonra mesaj
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CustomMessageBox.Show("Image exported successfully!", "Success", MessageBoxButton.OK, MsgImage.Success);
                    });
                }
                catch (Exception ex)
                {
                    GlobalErrorHandler.LogError(ex, "ExportPng");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CustomMessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MsgImage.Error);
                    });
                }
            }
        }
    }
}