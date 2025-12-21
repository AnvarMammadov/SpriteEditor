using System;
using System.Collections.Generic;
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
        // DATA & APPEARANCE
        // ==========================================
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GeometryPreview))]
        private string _rawPathData;

        [ObservableProperty] private string _fillColor = "#EAB308"; // Default Yellow
        [ObservableProperty] private string _strokeColor = "#00000000"; // Transparent default
        [ObservableProperty] private double _strokeThickness = 0;

        // ==========================================
        // TRANSFORMS (Editor Style)
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

        [ObservableProperty] private bool _isXamlVisible = true; // Default XAML

        // ==========================================
        // COLOR PALETTE (Professional Tailwind-like)
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

        // Rəng seçimi əmrləri
        [RelayCommand]
        private void ApplyFillColor(string color)
        {
            if (!string.IsNullOrEmpty(color)) FillColor = color;
        }

        [RelayCommand]
        private void ApplyStrokeColor(string color)
        {
            if (!string.IsNullOrEmpty(color)) StrokeColor = color;
        }

        public Geometry GeometryPreview
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RawPathData)) return null;
                try { return Geometry.Parse(RawPathData); } catch { return null; }
            }
        }




        // Dəyişiklik olanda kodu yenilə
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

        // View dəyişmə əmrləri
        [RelayCommand]
        private void ShowXaml() => IsXamlVisible = true;

        [RelayCommand]
        private void ShowHtml() => IsXamlVisible = false;


        [RelayCommand]
        private void ResetTransforms()
        {
            ScaleX = 1.0;
            ScaleY = 1.0;
            Rotation = 0;
            TranslateX = 0;
            TranslateY = 0;
        }

        [RelayCommand]
        private void LoadSvgFile()
        {
            OpenFileDialog openDialog = new OpenFileDialog { Filter = "SVG Files (*.svg)|*.svg" };
            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string xmlContent = File.ReadAllText(openDialog.FileName);
                    var doc = XDocument.Parse(xmlContent);
                    XNamespace ns = "http://www.w3.org/2000/svg";
                    var pathElement = doc.Descendants(ns + "path").FirstOrDefault();

                    if (pathElement?.Attribute("d") != null)
                    {
                        RawPathData = pathElement.Attribute("d").Value;
                        // Rəngləri də oxumağa cəhd edə bilərik (sadəlik üçün hələlik saxlayıram)
                        GenerateCode();
                    }
                }
                catch (Exception ex) { GlobalErrorHandler.LogError(ex, "LoadSvg"); }
            }
        }

        [RelayCommand]
        private void GenerateCode()
        {
            if (string.IsNullOrWhiteSpace(RawPathData)) return;

            // XAML Generation (RenderTransform istifadə edərək)
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

            // HTML/SVG Generation (Group Transform)
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
            if (string.IsNullOrWhiteSpace(RawPathData)) return;

            SaveFileDialog saveDialog = new SaveFileDialog { Filter = "PNG Image (*.png)|*.png", FileName = "vector_design.png" };
            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // SVG-ni Transformlarla birlikdə yaradırıq
                    string tempSvg = Path.GetTempFileName() + ".svg";

                    // ViewBox-u genişləndiririk ki, sürüşdürmə (translate) zamanı kəsilməsin
                    string svgContent = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"2048\" height=\"2048\" viewBox=\"-50 -50 200 200\">\n" +
                                        $"  <g transform=\"translate({TranslateX + 50},{TranslateY + 50}) rotate({Rotation}) scale({ScaleX},{ScaleY})\">\n" +
                                        $"      <path d=\"{RawPathData}\" fill=\"{FillColor}\" stroke=\"{StrokeColor}\" stroke-width=\"{StrokeThickness}\" />\n" +
                                        $"  </g>\n" +
                                        $"</svg>";

                    File.WriteAllText(tempSvg, svgContent);

                    await Task.Run(() =>
                    {
                        var settings = new MagickReadSettings { Density = new Density(300), BackgroundColor = MagickColors.Transparent };
                        using (var image = new MagickImage(tempSvg, settings))
                        {
                            image.Format = MagickFormat.Png;
                            image.Write(saveDialog.FileName);
                        }
                    });
                    if (File.Exists(tempSvg)) File.Delete(tempSvg);
                }
                catch (Exception ex) { GlobalErrorHandler.LogError(ex, "ExportPng"); }
            }
        }
    }
}
