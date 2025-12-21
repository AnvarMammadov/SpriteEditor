using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // ObservableCollection üçün
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq; // XML Parse üçün vacibdir
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
        // LAYERS SYSTEM (YENİ)
        // ==========================================
        // Layların siyahısı (View-da ListBox-a bağlanacaq)
        public ObservableCollection<SvgLayerItem> Layers { get; } = new ObservableCollection<SvgLayerItem>();

        // Orijinal SVG başlığı (viewBox, width, height qorumaq üçün)
        private string _originalSvgHeader = "";
        private string _originalSvgFooter = "</svg>";

        // ==========================================
        // DATA & APPEARANCE
        // ==========================================
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GeometryPreview))]
        private string _rawPathData;

        [ObservableProperty] private string _fillColor = "#EAB308";
        [ObservableProperty] private string _strokeColor = "#00000000";
        [ObservableProperty] private double _strokeThickness = 0;

        // Transforms
        [ObservableProperty] private double _scaleX = 1.0;
        [ObservableProperty] private double _scaleY = 1.0;
        [ObservableProperty] private double _rotation = 0;
        [ObservableProperty] private double _translateX = 0;
        [ObservableProperty] private double _translateY = 0;

        // Outputs
        [ObservableProperty] private string _xamlOutput;
        [ObservableProperty] private string _htmlOutput;
        [ObservableProperty] private bool _isXamlVisible = true;

        // Color Palette
        public List<string> ColorPalette { get; } = new List<string>
        {
            "#000000", "#111827", "#374151", "#6B7280", "#9CA3AF", "#D1D5DB", "#F3F4F6", "#FFFFFF",
            "#EF4444", "#F87171", "#F59E0B", "#FBBF24", "#F97316", "#FB923C",
            "#10B981", "#34D399", "#14B8A6", "#2DD4BF", "#0EA5E9", "#38BDF8",
            "#3B82F6", "#60A5FA", "#6366F1", "#818CF8", "#8B5CF6", "#A78BFA",
            "#A855F7", "#C084FC", "#EC4899", "#F472B6", "#F43F5E", "#FB7185"
        };

        [RelayCommand] private void ApplyFillColor(string color) { if (!IsFullSvgMode && !string.IsNullOrEmpty(color)) FillColor = color; }
        [RelayCommand] private void ApplyStrokeColor(string color) { if (!IsFullSvgMode && !string.IsNullOrEmpty(color)) StrokeColor = color; }

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
            Layers.Clear(); // Köhnə layları təmizlə

            if (string.IsNullOrWhiteSpace(value))
            {
                IsFullSvgMode = false;
                FullSvgPreview = null;
                return;
            }

            if (value.Trim().StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
            {
                IsFullSvgMode = true;

                // 1. SVG-ni analiz et və laylara böl
                ParseSvgLayers(value);

                // 2. İlkin render
                UpdateFullSvgRender();

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

        // ----------------------------------------------------------
        // LAYER PARSING LOGIC (YENİ)
        // ----------------------------------------------------------
        private void ParseSvgLayers(string svgContent)
        {
            try
            {
                var doc = XDocument.Parse(svgContent);
                var root = doc.Root;

                // Headeri saxlayırıq (bütün atributları ilə birlikdə)
                // Məsələn: <svg width="200" viewBox="0 0 100 100" ...>
                string fullRootStr = root.ToString();
                int firstCloseTag = fullRootStr.IndexOf('>');
                _originalSvgHeader = fullRootStr.Substring(0, firstCloseTag + 1);

                XNamespace ns = root.GetDefaultNamespace();

                // Birbaşa uşaqları (path, g, rect, circle, polygon) tapırıq
                // Jacksmith tərzi oyunlarda adətən hər hissə bir Group (<g>) və ya Path olur.
                foreach (var element in root.Elements())
                {
                    string tagName = element.Name.LocalName;

                    // Yalnız qrafik elementləri götürək
                    if (tagName == "defs" || tagName == "style" || tagName == "metadata") continue;

                    // Adını tapmaq (id="blade" -> "blade")
                    string layerName = element.Attribute("id")?.Value ?? tagName;

                    // Elementin bütün XML-ni saxla
                    string content = element.ToString();

                    // Lay yarad və siyahıya at
                    var layerItem = new SvgLayerItem(layerName, content, OnLayerVisibilityChanged);
                    Layers.Add(layerItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing layers: {ex.Message}");
            }
        }

        // Hər hansı bir layın checkbox-u dəyişəndə bu işə düşür
        private void OnLayerVisibilityChanged()
        {
            UpdateFullSvgRender();
        }

        // Görünən laylardan yeni SVG mətni düzəldir və render edir
        private void UpdateFullSvgRender()
        {
            if (!IsFullSvgMode) return;

            // 1. SVG-ni yenidən quraşdır
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(_originalSvgHeader); // <svg ...>

            foreach (var layer in Layers)
            {
                if (layer.IsVisible)
                {
                    sb.AppendLine(layer.XmlContent);
                }
            }

            sb.AppendLine(_originalSvgFooter); // </svg>

            // 2. Render et
            string reconstructedSvg = sb.ToString();
            HtmlOutput = reconstructedSvg; // Kodu da yenilə
            RenderFullSvgPreview(reconstructedSvg);
        }

        private void RenderFullSvgPreview(string svgContent)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(svgContent);
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
                System.Diagnostics.Debug.WriteLine($"Preview Error: {ex.Message}");
            }
        }

        // ... (Digər metodlar eynilə qalır)
        partial void OnFillColorChanged(string value) => GenerateCode();
        partial void OnStrokeColorChanged(string value) => GenerateCode();
        partial void OnStrokeThicknessChanged(double value) => GenerateCode();
        partial void OnScaleXChanged(double value) => GenerateCode();
        partial void OnScaleYChanged(double value) => GenerateCode();
        partial void OnRotationChanged(double value) => GenerateCode();
        partial void OnTranslateXChanged(double value) => GenerateCode();
        partial void OnTranslateYChanged(double value) => GenerateCode();

        [RelayCommand] private void ShowXaml() => IsXamlVisible = true;
        [RelayCommand] private void ShowHtml() => IsXamlVisible = false;
        [RelayCommand] private void ResetTransforms() { ScaleX = 1.0; ScaleY = 1.0; Rotation = 0; TranslateX = 0; TranslateY = 0; }

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

            HtmlOutput = $"<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">\n" +
                         $"  <g transform=\"translate({TranslateX},{TranslateY}) rotate({Rotation}) scale({ScaleX},{ScaleY})\">\n" +
                         $"    <path d=\"{RawPathData}\" \n" +
                         $"          fill=\"{FillColor}\" \n" +
                         $"          stroke=\"{StrokeColor}\" stroke-width=\"{StrokeThickness}\" />\n" +
                         $"  </g>\n" +
                         $"</svg>";
        }

        [RelayCommand]
        private async Task ExportToPng()
        {
            // RawPathData yox, hal-hazırda görünən (reconstructed) SVG-ni istifadə etmək lazımdır
            // Əgər Full Mode-dursa HtmlOutput-da bizim ən son renderimiz var.
            string contentToExport = IsFullSvgMode ? HtmlOutput : RawPathData;

            if (string.IsNullOrWhiteSpace(contentToExport)) return;

            SaveFileDialog saveDialog = new SaveFileDialog { Filter = "PNG Image (*.png)|*.png", FileName = "part_export.png" };
            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    double currentRot = Rotation;
                    double currentScaleX = ScaleX;
                    double currentScaleY = ScaleY;
                    string currentFill = FillColor;
                    string currentStroke = StrokeColor;
                    double currentThick = StrokeThickness;
                    bool isFull = IsFullSvgMode;

                    await Task.Run(() =>
                    {
                        var settings = new MagickReadSettings { Density = new Density(300), BackgroundColor = MagickColors.Transparent };

                        string svgToRender;
                        if (isFull)
                        {
                            svgToRender = contentToExport; // Artıq süzgəcdən keçmiş SVG
                        }
                        else
                        {
                            svgToRender = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"500\" height=\"500\" viewBox=\"0 0 100 100\">" +
                                          $"<path d=\"{contentToExport}\" fill=\"{currentFill}\" stroke=\"{currentStroke}\" stroke-width=\"{currentThick}\" />" +
                                          $"</svg>";
                        }

                        using (var image = new MagickImage(Encoding.UTF8.GetBytes(svgToRender), settings))
                        {
                            if (Math.Abs(currentScaleX - 1.0) > 0.01 || Math.Abs(currentScaleY - 1.0) > 0.01)
                            {
                                uint newW = (uint)(image.Width * currentScaleX);
                                uint newH = (uint)(image.Height * currentScaleY);
                                image.Resize(new MagickGeometry(newW, newH) { IgnoreAspectRatio = true });
                            }

                            if (Math.Abs(currentRot) > 0.01)
                            {
                                image.BackgroundColor = MagickColors.Transparent;
                                image.Rotate(currentRot);
                            }

                            image.Format = MagickFormat.Png;
                            image.Write(saveDialog.FileName);
                        }
                    });

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CustomMessageBox.Show("Image/Layer exported successfully!", "Success", MessageBoxButton.OK, MsgImage.Success);
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