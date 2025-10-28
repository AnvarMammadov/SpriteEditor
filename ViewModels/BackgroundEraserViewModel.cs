using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SpriteEditor.Services;
using System.Windows.Media;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;
using WpfColor = System.Windows.Media.Color;

namespace SpriteEditor.ViewModels
{
    public partial class BackgroundEraserViewModel : ObservableObject
    {

        // === Windows Shell Notifier (P/Invoke) ===
        // Bu, OpenFileDialog-un dərhal yenilənməsi üçün lazımdır
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const int SHCNE_CREATE = 0x00000002; // 2. BU BLOKU ƏLAVƏ EDİN (Fayl yaradıldı)
        private const int SHCNF_PATH = 0x0001;       //    (dwItem1 bir yoldur)
        // ==========================================

        private readonly ImageService _imageService;

        // === YENİ XASSƏLƏR ===
        // Pipet alətinin başladığı nöqtəni yadda saxlamaq üçün
        public int StartPixelX { get; set; }
        public int StartPixelY { get; set; }
        // =======================


        // === Şəkil Yükləmə Xassələri (SpriteSlicerViewModel-dən kopyalanıb) ===
        [ObservableProperty]
        private BitmapImage _loadedImageSource; // Orijinal şəkil

        private string _loadedImagePath;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor("GeneratePreviewCommand")]// <-- "Async" əlavə edin
        private bool _isImageLoaded = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor("GeneratePreviewCommand")] // <-- "Async" əlavə edin
        [NotifyCanExecuteChangedFor("SaveImageCommand")]
        private bool _isProcessing = false;

        // === Bu Alətə Aid Xassələr ===
        [ObservableProperty]
        private BitmapImage _previewImageSource; // Nəticəni göstərmək üçün

        [ObservableProperty]
        private WpfColor _targetColor = System.Windows.Media.Colors.Green; // Başlanğıc hədəf rəng (məsələn, Chroma Key)

        [ObservableProperty]
        private double _tolerance = 10.0; // 0-100 aralığında həssaslıq

        [ObservableProperty]
        [NotifyCanExecuteChangedFor("SaveImageCommand")]
        private byte[] _lastProcessedData; // Yadda saxlamaq üçün son nəticəni saxla

        public BackgroundEraserViewModel()
        {
            _imageService = new ImageService();
        }

        // === Şəkil Yükləmə Əmri (SpriteSlicerViewModel-dən kopyalanıb) ===
        [RelayCommand]
        public void LoadImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = "Görüntü Faylları (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|Bütün Fayllar (*.*)|*.*";

            if (openDialog.ShowDialog() == true)
            {
                _loadedImagePath = openDialog.FileName;

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new System.Uri(_loadedImagePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                LoadedImageSource = bitmap;
                PreviewImageSource = null; // Köhnə preview-u təmizlə
                _lastProcessedData = null;
                IsImageLoaded = true;
            }
        }

        // === Yeni Əmrlər ===

        [RelayCommand(CanExecute = nameof(CanProcess))]
        public async Task GeneratePreviewAsync()
        {
            if (!CanProcess()) return;

            IsProcessing = true;
            _lastProcessedData = null; // Preview-u sıfırla

            try
            {
                System.Windows.MessageBox.Show(
                            $"Rəngi silməyə başlayıram:\n" +
                            $"Hədəf Rəng: {TargetColor}\n" +
                            $"Həssaslıq: {Tolerance}%",
                            "Diaqnostika");

                //// WPF Rəngini ImageSharp Rənginə çevir
                //var wpfColor = TargetColor; // Bu sətri əlavə edib-etmədiyinizi yoxlayın
                //var sharpColor = new Rgba32(wpfColor.R, wpfColor.G, wpfColor.B, wpfColor.A);

                // Servisi arxa fonda çağır
                byte[] pngData = await Task.Run(() =>
                     _imageService.RemoveBackground(
                         _loadedImagePath,
                         StartPixelX,  // Kliklənən X koordinatı
                         StartPixelY,  // Kliklənən Y koordinatı
                         (float)Tolerance
                     )
                 );

                // Nəticəni (byte massivi) BitmapImage-ə çevir
                var previewBitmap = new BitmapImage();
                using (var ms = new MemoryStream(pngData))
                {
                    previewBitmap.BeginInit();
                    previewBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    previewBitmap.StreamSource = ms;
                    previewBitmap.EndInit();
                }
                previewBitmap.Freeze();

                PreviewImageSource = previewBitmap;
                LastProcessedData = pngData; // Saxlamaq üçün yadda saxla
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Xəta baş verdi: {ex.Message}", "Xəta");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        public void SaveImage()
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.FileName = "processed_image.png";
            saveDialog.Filter = "PNG Image (*.png)|*.png";

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(saveDialog.FileName, LastProcessedData);
                    SHChangeNotify(SHCNE_CREATE, SHCNF_PATH, Marshal.StringToHGlobalAuto(saveDialog.FileName), IntPtr.Zero);
                    System.Windows.MessageBox.Show("Şəkil uğurla yadda saxlandı!", "Uğurlu");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Yadda saxlama zamanı xəta: {ex.Message}", "Xəta");
                }
            }
        }

        public bool CanProcess() => IsImageLoaded && !IsProcessing;
        public bool CanSave() => LastProcessedData != null && !IsProcessing;

        // CanExecute üçün NotifyCanExecuteChangedFor əlavə etmək lazımdır...
        // Sadəlik üçün hələlik belə qalsın.
    }
}
