using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
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
        // STATE & LAYERS
        // ==========================================
        [ObservableProperty] private ImageSource _fullSvgPreview;
        [ObservableProperty] private bool _isFullSvgMode;
        public ObservableCollection<SvgLayerItem> Layers { get; } = new ObservableCollection<SvgLayerItem>();
        private string _originalSvgHeader = "";
        private string _originalSvgFooter = "</svg>";

        // ==========================================
        // PIVOT POINT SYSTEM
        // ==========================================
        [ObservableProperty] private double _pivotX = 0;
        [ObservableProperty] private double _pivotY = 0;
        [ObservableProperty] private bool _isPivotMode;

        // View-dan çağırılacaq metod
        public void SetPivotFromClick(double x, double y)
        {
            PivotX = x;
            PivotY = y;
        }

        // ==========================================
        // APPEARANCE & TRANSFORMS
        // ==========================================
        [ObservableProperty][NotifyPropertyChangedFor(nameof(GeometryPreview))] private string _rawPathData;
        [ObservableProperty] private string _fillColor = "#EAB308";
        [ObservableProperty] private string _strokeColor = "#00000000";
        [ObservableProperty] private double _strokeThickness = 0;

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

        public List<string> ColorPalette { get; } = new List<string>
        {
            "#000000", "#111827", "#374151", "#6B7280", "#9CA3AF", "#D1D5DB", "#F3F4F6", "#FFFFFF",
            "#EF4444", "#F87171", "#F59E0B", "#FBBF24", "#F97316", "#FB923C",
            "#10B981", "#34D399", "#14B8A6", "#2DD4BF", "#0EA5E9", "#38BDF8",
            "#3B82F6", "#60A5FA", "#6366F1", "#818CF8", "#8B5CF6", "#A78BFA",
            "#A855F7", "#C084FC", "#EC4899", "#F472B6", "#F43F5E", "#FB7185"
        };

        public Geometry GeometryPreview
        {
            get
            {
                if (IsFullSvgMode || string.IsNullOrWhiteSpace(RawPathData)) return null;
                try { return Geometry.Parse(RawPathData); } catch { return null; }
            }
        }

        // ==========================================
        // METHODS
        // ==========================================
        partial void OnRawPathDataChanged(string value)
        {
            Layers.Clear();
            if (string.IsNullOrWhiteSpace(value)) { IsFullSvgMode = false; FullSvgPreview = null; return; }

            if (value.Trim().StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
            {
                IsFullSvgMode = true;
                ParseSvgLayers(value);
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

        private void ParseSvgLayers(string svgContent)
        {
            try
            {
                var doc = XDocument.Parse(svgContent);
                var root = doc.Root;
                string fullRootStr = root.ToString();
                _originalSvgHeader = fullRootStr.Substring(0, fullRootStr.IndexOf('>') + 1);

                foreach (var element in root.Elements())
                {
                    string tagName = element.Name.LocalName;
                    if (tagName == "defs" || tagName == "style" || tagName == "metadata") continue;
                    string layerName = element.Attribute("id")?.Value ?? tagName;

                    // ViewModel-də SvgLayerItem-in OnChanged eventini UpdateFullSvgRender-ə bağlayırıq
                    Layers.Add(new SvgLayerItem(layerName, element.ToString(), UpdateFullSvgRender));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private void UpdateFullSvgRender()
        {
            if (!IsFullSvgMode) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(_originalSvgHeader);
            foreach (var layer in Layers) { if (layer.IsVisible) sb.AppendLine(layer.XmlContent); }
            sb.AppendLine(_originalSvgFooter);

            HtmlOutput = sb.ToString();
            RenderFullSvgPreview(HtmlOutput);
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
            catch { }
        }

        // Change Triggers
        partial void OnFillColorChanged(string value) => GenerateCode();
        partial void OnStrokeColorChanged(string value) => GenerateCode();
        partial void OnStrokeThicknessChanged(double value) => GenerateCode();
        partial void OnScaleXChanged(double value) => GenerateCode();
        partial void OnScaleYChanged(double value) => GenerateCode();
        partial void OnRotationChanged(double value) => GenerateCode();
        partial void OnTranslateXChanged(double value) => GenerateCode();
        partial void OnTranslateYChanged(double value) => GenerateCode();

        // Commands
        [RelayCommand] private void ApplyFillColor(string color) { if (!IsFullSvgMode) FillColor = color; }
        [RelayCommand] private void ApplyStrokeColor(string color) { if (!IsFullSvgMode) StrokeColor = color; }
        [RelayCommand] private void ShowXaml() => IsXamlVisible = true;
        [RelayCommand] private void ShowHtml() => IsXamlVisible = false;
        [RelayCommand] private void ResetTransforms() { ScaleX = 1; ScaleY = 1; Rotation = 0; TranslateX = 0; TranslateY = 0; }

        [RelayCommand]
        private void LoadSvgFile()
        {
            OpenFileDialog openDialog = new OpenFileDialog { Filter = "SVG Files (*.svg)|*.svg" };
            if (openDialog.ShowDialog() == true) RawPathData = File.ReadAllText(openDialog.FileName);
        }

        [RelayCommand]
        private void GenerateCode()
        {
            if (IsFullSvgMode || string.IsNullOrWhiteSpace(RawPathData)) return;
            XamlOutput = $"<Path Data=\"{RawPathData}\" Fill=\"{FillColor}\" Stroke=\"{StrokeColor}\" StrokeThickness=\"{StrokeThickness}\" Stretch=\"Uniform\">\n" +
                         $"  <Path.RenderTransform><TransformGroup>\n" +
                         $"    <ScaleTransform ScaleX=\"{ScaleX:F2}\" ScaleY=\"{ScaleY:F2}\"/>\n" +
                         $"    <RotateTransform Angle=\"{Rotation:F2}\"/>\n" +
                         $"    <TranslateTransform X=\"{TranslateX:F2}\" Y=\"{TranslateY:F2}\"/>\n" +
                         $"  </TransformGroup></Path.RenderTransform>\n</Path>";
            HtmlOutput = $"<svg...><path d=\"{RawPathData}\" fill=\"{FillColor}\" ... /></svg>";
        }

        // ==========================================
        // EXPORT LOGIC (PIVOT + BATCH)
        // ==========================================

        // Single Export
        [RelayCommand]
        private async Task ExportToPng()
        {
            string content = IsFullSvgMode ? HtmlOutput : RawPathData;
            if (string.IsNullOrWhiteSpace(content)) return;

            SaveFileDialog dlg = new SaveFileDialog { Filter = "PNG (*.png)|*.png", FileName = "export.png" };
            if (dlg.ShowDialog() == true)
            {
                await ExportContent(content, dlg.FileName, IsFullSvgMode);
                Application.Current.Dispatcher.Invoke(() => CustomMessageBox.Show("Saved!", "Success", MessageBoxButton.OK, MsgImage.Success));
            }
        }

        // Batch Export
        [RelayCommand]
        private async Task BatchExportLayers()
        {
            var visibleLayers = Layers.Where(l => l.IsVisible).ToList();
            if (!visibleLayers.Any()) return;

            SaveFileDialog dlg = new SaveFileDialog { Filter = "PNG (*.png)|*.png", FileName = "Part.png", Title = "Batch Export Base Name" };
            if (dlg.ShowDialog() == true)
            {
                string dir = Path.GetDirectoryName(dlg.FileName);
                string baseName = Path.GetFileNameWithoutExtension(dlg.FileName);

                await Task.Run(async () =>
                {
                    foreach (var layer in visibleLayers)
                    {
                        string layerSvg = $"{_originalSvgHeader}\n{layer.XmlContent}\n{_originalSvgFooter}";
                        string safeName = string.Join("_", layer.Name.Split(Path.GetInvalidFileNameChars()));
                        string path = Path.Combine(dir, $"{baseName}_{safeName}.png");
                        await ExportContent(layerSvg, path, true);
                    }
                });
                Application.Current.Dispatcher.Invoke(() => CustomMessageBox.Show($"Batch Export Complete!", "Success", MessageBoxButton.OK, MsgImage.Success));
            }
        }

        private async Task ExportContent(string content, string path, bool isFull)
        {
            double r = Rotation; double sx = ScaleX; double sy = ScaleY;
            string f = FillColor; string s = StrokeColor; double st = StrokeThickness;
            // Capture Pivot for JSON
            double px = PivotX; double py = PivotY;

            await Task.Run(() =>
            {
                var settings = new MagickReadSettings { Density = new Density(300), BackgroundColor = MagickColors.Transparent };
                string svg = isFull ? content : $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"500\" height=\"500\" viewBox=\"0 0 100 100\"><path d=\"{content}\" fill=\"{f}\" stroke=\"{s}\" stroke-width=\"{st}\" /></svg>";

                using (var img = new MagickImage(Encoding.UTF8.GetBytes(svg), settings))
                {
                    if (Math.Abs(sx - 1) > 0.01 || Math.Abs(sy - 1) > 0.01)
                        img.Resize(new MagickGeometry((uint)(img.Width * sx), (uint)(img.Height * sy)) { IgnoreAspectRatio = true });

                    if (Math.Abs(r) > 0.01) { img.BackgroundColor = MagickColors.Transparent; img.Rotate(r); }

                    img.Format = MagickFormat.Png;
                    img.Write(path);
                }
            });

            // Write JSON Sidecar for Pivot
            try
            {
                string jsonPath = Path.ChangeExtension(path, ".json");
                string json = $"{{\n  \"file\": \"{Path.GetFileName(path)}\",\n  \"pivot_x\": {px:F1},\n  \"pivot_y\": {py:F1}\n}}";
                File.WriteAllText(jsonPath, json);
            }
            catch { }
        }
    }
}