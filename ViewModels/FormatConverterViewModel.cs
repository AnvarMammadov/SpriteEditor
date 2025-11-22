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
using SpriteEditor.Views;

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
            "PNG",
            "JPG",
            "JPEG",
            "WEBP",
            "BMP",
            "TIFF",
            "TGA",
            "GIF",
            "ICO",
            "AVIF" // Qeyd: Şəkli bu formata çevirmək üçün kitabxana dəstəyi vacibdir
        };

        [ObservableProperty]
        private string _selectedFormat = "JPG";

        public FormatConverterViewModel()
        {
            _imageService = new ImageService();
        }

        // LoadImage metodunu yeniləyin
        [RelayCommand]
        private void LoadImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                // Filtri genişləndirdik
                Filter = "All Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.tga;*.ico;*.avif;*.heic;*.tiff|AVIF Image (*.avif)|*.avif|WebP Image (*.webp)|*.webp|PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg"
            };

            if (openDialog.ShowDialog() == true)
            {
                SourcePath = openDialog.FileName;

                // AVIF-i birbaşa WPF Image elementində göstərmək çətindir.
                // Ona görə də, əgər fayl AVIF-dirsə, onu müvəqqəti PNG-yə çevirib göstərəcəyik.
                string ext = Path.GetExtension(SourcePath).ToLower();

                if (ext == ".avif" || ext == ".heic")
                {
                    // Müvəqqəti preview yarat
                    try
                    {
                        using (var tempImage = new ImageMagick.MagickImage(SourcePath))
                        {
                            // Yaddaşda (MemoryStream) PNG-yə çevir
                            byte[] imageBytes = tempImage.ToByteArray(ImageMagick.MagickFormat.Png);

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
                    catch
                    {
                        // Əgər oxuya bilməsə, boş qalsın
                        PreviewSource = null;
                    }
                }
                else
                {
                    // Standart formatlar üçün birbaşa yüklə
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(SourcePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    PreviewSource = bitmap;
                }

                IsImageLoaded = true;
            }
        }



        [RelayCommand(CanExecute = nameof(CanConvert))]
        private async Task Convert()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            string ext = SelectedFormat.ToLower();

            // AVIF üçün xüsusi yoxlama (əgər kitabxana yoxdursa)
            if (ext == "avif")
            {
                // Burda gələcəkdə Magick.NET inteqrasiyası ola bilər
                // Hələlik xəbərdarlıq edək və ya davam edək
            }

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

                    CustomMessageBox.Show("Str_Msg_SuccessConvert", "Str_Title_Success", MessageBoxButton.OK, MsgImage.Success);
                }
                catch (SixLabors.ImageSharp.UnknownImageFormatException)
                {
                    CustomMessageBox.Show(
     App.GetStr("Str_Msg_ErrFormatNotSupported", SelectedFormat),
     "Str_Title_Warning",
     MessageBoxButton.OK,
     MsgImage.Warning);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(
     App.GetStr("Str_Msg_ErrGeneral", ex.Message),
     "Str_Title_Error",
     MessageBoxButton.OK,
     MsgImage.Error);
                }
            }
        }

        private bool CanConvert() => IsImageLoaded;
    }
}