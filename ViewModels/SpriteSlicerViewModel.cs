using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using SpriteEditor.Data;
using SpriteEditor.Services;
using SpriteEditor.Views;

namespace SpriteEditor.ViewModels
{
    public class DetectedSpriteItem : ObservableObject
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public DetectedSpriteItem(int x, int y, int w, int h)
        {
            X = x; Y = y; Width = w; Height = h;
        }
    }

    public partial class SpriteSlicerViewModel : ObservableObject
    {
        public enum SlicerMode { Grid, AutoDetect, PolygonPen }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
        private const int SHCNE_UPDATEDIR = 0x00001000;
        private const int SHCNF_PATH = 0x0001;

        private readonly ImageService _imageService;

        [ObservableProperty] private BitmapImage _loadedImageSource;
        private string _loadedImagePath;
        [ObservableProperty] private int _imagePixelWidth;
        [ObservableProperty] private int _imagePixelHeight;

        [ObservableProperty] private double _slicerX;
        [ObservableProperty] private double _slicerY;
        [ObservableProperty] private double _slicerWidth;
        [ObservableProperty] private double _slicerHeight;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SliceImageCommand))]
        [NotifyCanExecuteChangedFor(nameof(AutoDetectSpritesCommand))]
        [NotifyCanExecuteChangedFor(nameof(ExportAllPartsCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddCurrentPolygonToListCommand))]
        private bool _isImageLoaded = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SliceImageCommand))]
        [NotifyCanExecuteChangedFor(nameof(ExportAllPartsCommand))]
        private bool _isSlicing = false;

        [ObservableProperty] private int _columns = 4;
        [ObservableProperty] private int _rows = 4;

        public ObservableCollection<GridLineViewModel> GridLines { get; } = new ObservableCollection<GridLineViewModel>();
        public ObservableCollection<DetectedSpriteItem> DetectedRects { get; } = new ObservableCollection<DetectedSpriteItem>();

        [ObservableProperty] private bool _useAutoDetection = false;
        [ObservableProperty] private SlicerMode _currentMode = SlicerMode.Grid;

        [ObservableProperty] private PointCollection _visualDrawingPoints = new PointCollection();

        // --- PREVIEW LINE (Rubber Band) PROPS ---
        [ObservableProperty] private Visibility _previewLineVisibility = Visibility.Collapsed;
        [ObservableProperty] private double _previewX1;
        [ObservableProperty] private double _previewY1;
        [ObservableProperty] private double _previewX2;
        [ObservableProperty] private double _previewY2;
        // ----------------------------------------

        public ObservableCollection<Point> CurrentDrawingPoints { get; } = new ObservableCollection<Point>();
        public ObservableCollection<SlicePart> ExtractedParts { get; } = new ObservableCollection<SlicePart>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteSelectedPartCommand))]
        private SlicePart _selectedPart;

        public SpriteSlicerViewModel()
        {
            _imageService = new ImageService();
            CurrentDrawingPoints.CollectionChanged += CurrentDrawingPoints_CollectionChanged;
        }

        private void CurrentDrawingPoints_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            VisualDrawingPoints = new PointCollection(CurrentDrawingPoints);
            // Nöqtə əlavə olunan kimi Preview Line başlanğıcını yenilə
            if (CurrentDrawingPoints.Count > 0)
            {
                var last = CurrentDrawingPoints.Last();
                PreviewX1 = last.X;
                PreviewY1 = last.Y;
                // PreviewX2/Y2 MouseMove ilə yenilənəcək
            }
        }

        // MouseMove hadisəsindən çağırılacaq metod
        public void UpdatePreviewLine(Point mousePos)
        {
            if (CurrentDrawingPoints.Count > 0)
            {
                var lastPoint = CurrentDrawingPoints.Last();
                PreviewX1 = lastPoint.X;
                PreviewY1 = lastPoint.Y;
                PreviewX2 = mousePos.X;
                PreviewY2 = mousePos.Y;
                PreviewLineVisibility = Visibility.Visible;
            }
            else
            {
                PreviewLineVisibility = Visibility.Collapsed;
            }
        }

        [RelayCommand]
        private void LoadImage()
        {
            // Resources yoxdursa string title = "Select Image";
            OpenFileDialog openDialog = new OpenFileDialog { Filter = "Images (*.png;*.jpg)|*.png;*.jpg" };

            if (openDialog.ShowDialog() == true)
            {
                _loadedImagePath = openDialog.FileName;
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_loadedImagePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                LoadedImageSource = bitmap;
                IsImageLoaded = true;
                ImagePixelWidth = bitmap.PixelWidth;
                ImagePixelHeight = bitmap.PixelHeight;

                SlicerX = 0; SlicerY = 0;
                SlicerWidth = bitmap.PixelWidth; SlicerHeight = bitmap.PixelHeight;
                SetMode(CurrentMode);
            }
        }

        private void UpdateGridLines()
        {
            GridLines.Clear();
            if (!IsImageLoaded || CurrentMode != SlicerMode.Grid) return;
            double cellWidth = SlicerWidth / Columns;
            double cellHeight = SlicerHeight / Rows;
            for (int i = 1; i < Columns; i++) { double x = SlicerX + (i * cellWidth); GridLines.Add(new GridLineViewModel { X1 = x, Y1 = SlicerY, X2 = x, Y2 = SlicerY + SlicerHeight, IsVertical = true }); }
            for (int i = 1; i < Rows; i++) { double y = SlicerY + (i * cellHeight); GridLines.Add(new GridLineViewModel { X1 = SlicerX, Y1 = y, X2 = SlicerX + SlicerWidth, Y2 = y, IsVertical = false }); }
        }

        [RelayCommand]
        private void SetMode(SlicerMode mode)
        {
            CurrentMode = mode;
            UseAutoDetection = (mode == SlicerMode.AutoDetect);

            // Təmizlik işləri
            GridLines.Clear();
            DetectedRects.Clear();
            CurrentDrawingPoints.Clear();
            PreviewLineVisibility = Visibility.Collapsed; // Xətti gizlət

            if (mode == SlicerMode.Grid) UpdateGridLines();
        }

        [RelayCommand(CanExecute = nameof(CanAddPolygon))]
        private void AddCurrentPolygonToList()
        {
            if (CurrentDrawingPoints.Count < 3) return;
            var newPart = new SlicePart { Name = $"Part_{ExtractedParts.Count + 1}", Points = new ObservableCollection<Point>(CurrentDrawingPoints), IsSelected = true };
            if (SelectedPart != null) SelectedPart.IsSelected = false;
            ExtractedParts.Add(newPart);
            SelectedPart = newPart;

            CurrentDrawingPoints.Clear();
            PreviewLineVisibility = Visibility.Collapsed; // Xətti gizlət

            ExportAllPartsCommand.NotifyCanExecuteChanged();
        }
        private bool CanAddPolygon() => IsImageLoaded && CurrentDrawingPoints.Count >= 3;

        [RelayCommand(CanExecute = nameof(CanDeletePart))]
        private void DeleteSelectedPart()
        {
            if (SelectedPart != null) { ExtractedParts.Remove(SelectedPart); SelectedPart = null; ExportAllPartsCommand.NotifyCanExecuteChanged(); }
        }
        private bool CanDeletePart() => SelectedPart != null;

        [RelayCommand(CanExecute = nameof(CanSliceImage))]
        private async Task SliceImageAsync()
        {
            SaveFileDialog dlg = new SaveFileDialog { FileName = "sprite_output.png" };
            if (dlg.ShowDialog() == true)
            {
                IsSlicing = true;
                string dir = Path.GetDirectoryName(dlg.FileName);
                await Task.Run(() =>
                {
                    if (UseAutoDetection)
                    {
                        var rects = DetectedRects.Select(r => new Int32Rect(r.X, r.Y, r.Width, r.Height)).ToList();
                        _imageService.SliceByRects(_loadedImagePath, rects, dir);
                    }
                    else
                    {
                        var rects = GenerateRectsFromGridLines();
                        _imageService.SliceByRects(_loadedImagePath, rects, dir);
                    }
                });
                NotifyShell(dir);
                IsSlicing = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExportPolygons))]
        private async Task ExportAllPartsAsync()
        {
            SaveFileDialog dlg = new SaveFileDialog { FileName = "Part.png" };
            if (dlg.ShowDialog() == true)
            {
                IsSlicing = true;
                string dir = Path.GetDirectoryName(dlg.FileName);
                await Task.Run(() => _imageService.SlicePolygons(_loadedImagePath, ExtractedParts, dir));
                NotifyShell(dir);
                IsSlicing = false;
            }
        }
        private bool CanExportPolygons() => IsImageLoaded && !IsSlicing && ExtractedParts.Count > 0;
        private bool CanSliceImage() => IsImageLoaded && !IsSlicing;

        [RelayCommand(CanExecute = nameof(CanSliceImage))]
        private async Task AutoDetectSpritesAsync()
        {
            if (!IsImageLoaded) return;
            IsSlicing = true;
            // Burada təmizləməyə ehtiyac yoxdur, SetMode aşağıda edəcək
            try
            {
                var rects = await Task.Run(() => _imageService.DetectSprites(_loadedImagePath));

                if (rects.Count > 0)
                {
                    // DÜZƏLİŞ: Əvvəl rejimi dəyişirik ki, DetectedRects təmizlənsin
                    SetMode(SlicerMode.AutoDetect);

                    // Sonra yeniləri əlavə edirik
                    foreach (var r in rects)
                        DetectedRects.Add(new DetectedSpriteItem(r.X, r.Y, r.Width, r.Height));

                    CustomMessageBox.Show($"{rects.Count} sprites found!", "Success", MessageBoxButton.OK, MsgImage.Success);
                }
                else
                {
                    CustomMessageBox.Show("No sprites detected.", "Info", MessageBoxButton.OK, MsgImage.Info);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Detection Error: {ex.Message}", "Error", MessageBoxButton.OK, MsgImage.Error);
            }
            finally
            {
                IsSlicing = false;
                SliceImageCommand.NotifyCanExecuteChanged();
                AutoDetectSpritesCommand.NotifyCanExecuteChanged();
            }
        }

        private List<Int32Rect> GenerateRectsFromGridLines()
        {
            var xCoords = new List<double> { SlicerX }; xCoords.AddRange(GridLines.Where(l => l.IsVertical).Select(l => l.X1)); xCoords.Add(SlicerX + SlicerWidth); xCoords.Sort();
            var yCoords = new List<double> { SlicerY }; yCoords.AddRange(GridLines.Where(l => !l.IsVertical).Select(l => l.Y1)); yCoords.Add(SlicerY + SlicerHeight); yCoords.Sort();
            var rects = new List<Int32Rect>();
            for (int y = 0; y < yCoords.Count - 1; y++)
                for (int x = 0; x < xCoords.Count - 1; x++)
                {
                    double w = xCoords[x + 1] - xCoords[x]; double h = yCoords[y + 1] - yCoords[y];
                    if (w > 0 && h > 0) rects.Add(new Int32Rect((int)xCoords[x], (int)yCoords[y], (int)w, (int)h));
                }
            return rects;
        }

        private void NotifyShell(string path) => SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATH, Marshal.StringToHGlobalAuto(path), IntPtr.Zero);

        partial void OnColumnsChanged(int value) { if (CurrentMode == SlicerMode.Grid) UpdateGridLines(); }
        partial void OnRowsChanged(int value) { if (CurrentMode == SlicerMode.Grid) UpdateGridLines(); }
        partial void OnSlicerXChanged(double value) { if (CurrentMode == SlicerMode.Grid) UpdateGridLines(); }
        partial void OnSlicerYChanged(double value) { if (CurrentMode == SlicerMode.Grid) UpdateGridLines(); }
        partial void OnSlicerWidthChanged(double value) { if (CurrentMode == SlicerMode.Grid) UpdateGridLines(); }
        partial void OnSlicerHeightChanged(double value) { if (CurrentMode == SlicerMode.Grid) UpdateGridLines(); }
        partial void OnCurrentModeChanged(SlicerMode value) => SetMode(value);
    }
}