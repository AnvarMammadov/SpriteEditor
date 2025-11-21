using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SpriteEditor.Services;

namespace SpriteEditor.ViewModels
{
    public partial class FormatConverterViewModel : ObservableObject
    {
        private readonly ImageService _imageService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
        private bool _isImageLoaded;

        [ObservableProperty]
        private string _sourcePath;

        [ObservableProperty]
        private BitmapImage _previewSource;

        // Dəstəklənən formatlar
        public ObservableCollection<string> AvailableFormats { get; } = new ObservableCollection<string>
        {
            "PNG", "JPG", "BMP", "WEBP", "TGA", "ICO"
        };

        [ObservableProperty]
        private string _selectedFormat = "JPG";

        public FormatConverterViewModel()
        {
            _imageService = new ImageService();
        }

        [RelayCommand]
        private void LoadImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Bütün Şəkillər|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.tga;*.ico"
            };

            if (openDialog.ShowDialog() == true)
            {
                SourcePath = openDialog.FileName;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(SourcePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                PreviewSource = bitmap;
                IsImageLoaded = true;
            }
        }

        [RelayCommand(CanExecute = nameof(CanConvert))]
        private async Task Convert()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            string ext = SelectedFormat.ToLower();
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                FileName = $"{Path.GetFileNameWithoutExtension(SourcePath)}_converted.{ext}",
                Filter = $"{SelectedFormat} Image (*.{ext})|*.{ext}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        _imageService.ConvertImageFormat(SourcePath, saveDialog.FileName);
                    });

                    MessageBox.Show("Konvertasiya uğurla tamamlandı!", "Uğurlu", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Xəta baş verdi: {ex.Message}", "Xəta", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool CanConvert() => IsImageLoaded;
    }
}