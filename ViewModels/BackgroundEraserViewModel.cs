using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SixLabors.ImageSharp.PixelFormats;
using SpriteEditor.Services;
using SpriteEditor.Views;
using WpfColor = System.Windows.Media.Color;

namespace SpriteEditor.ViewModels
{

    // === YENİ ===
    // Hansı alətin aktiv olduğunu təyin etmək üçün
    public enum EraserToolMode
    {
        Pipet,
        ManualEraser
    }
    // ============

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
        private WriteableBitmap _loadedImageSource;

        private string _loadedImagePath;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor("GeneratePreviewCommand")]
        [NotifyCanExecuteChangedFor("RefreshImageCommand")]
        private bool _isImageLoaded = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor("GeneratePreviewCommand")]
        [NotifyCanExecuteChangedFor("SaveImageCommand")]
        [NotifyCanExecuteChangedFor("RefreshImageCommand")]
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


        // === YENİ XASSƏLƏR ===
        [ObservableProperty]
        private EraserToolMode _currentToolMode = EraserToolMode.Pipet; // Başlanğıcda pipet aktiv olsun

        [ObservableProperty]
        private int _brushSize = 20; // Manual silgi üçün fırça ölçüsü
        // ======================


        public BackgroundEraserViewModel()
        {
            _imageService = new ImageService();
        }

        // === Şəkil Yükləmə Əmri (SpriteSlicerViewModel-dən kopyalanıb) ===
        [RelayCommand]
        public void LoadImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = "Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|Bütün Fayllar (*.*)|*.*";

            if (openDialog.ShowDialog() == true)
            {
                _loadedImagePath = openDialog.FileName;

                // === DƏYİŞİKLİK: WriteableBitmap Yükləməsi ===
                BitmapImage tempBitmap = new BitmapImage();
                try
                {
                    // Cache problemini həll etmək üçün StreamSource istifadə edirik
                    byte[] fileBytes = File.ReadAllBytes(_loadedImagePath);
                    using (var ms = new MemoryStream(fileBytes))
                    {
                        tempBitmap.BeginInit();
                        tempBitmap.CacheOption = BitmapCacheOption.OnLoad;
                        tempBitmap.StreamSource = ms;
                        tempBitmap.EndInit();
                    }
                    tempBitmap.Freeze();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(
     App.GetStr("Str_Msg_ErrLoadImage", ex.Message),
     "Str_Title_Error",
     MessageBoxButton.OK,
     MsgImage.Error);
                    return;
                }

                // Şəkli redaktə üçün BGRA32 formatına çeviririk (Şəffaflıq üçün vacibdir)
                FormatConvertedBitmap formattedBitmap = new FormatConvertedBitmap(
                    tempBitmap,
                    PixelFormats.Bgra32, // Alfa kanalı olan format
                    null,
                    0);

                // Nəhayət, WriteableBitmap yaradırıq
                WriteableBitmap wb = new WriteableBitmap(formattedBitmap);

                LoadedImageSource = wb; // <-- Əsas xassəyə mənimsədirik

                PreviewImageSource = null;
                _lastProcessedData = null;
                IsImageLoaded = true;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRefreshImage))]
        private void RefreshImage()
        {
            // ... (Bu metod da WriteableBitmap qaytarmalıdır) ...
            try
            {
                // Eynilə LoadImage kimi...
                BitmapImage tempBitmap = new BitmapImage();
                byte[] fileBytes = File.ReadAllBytes(_loadedImagePath);
                using (var ms = new MemoryStream(fileBytes))
                {
                    tempBitmap.BeginInit();
                    tempBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    tempBitmap.StreamSource = ms;
                    tempBitmap.EndInit();
                }
                tempBitmap.Freeze();

                FormatConvertedBitmap formattedBitmap = new FormatConvertedBitmap(tempBitmap, PixelFormats.Bgra32, null, 0);
                WriteableBitmap wb = new WriteableBitmap(formattedBitmap);

                LoadedImageSource = wb; // Yeniləndi

                PreviewImageSource = null;
                _lastProcessedData = null;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
     App.GetStr("Str_Msg_ErrLoadImage", ex.Message),
     "Str_Title_Error",
     MessageBoxButton.OK,
     MsgImage.Error);
                IsImageLoaded = false;
                LoadedImageSource = null;
                _loadedImagePath = null;
            }
        }

        // ... (ReloadImageFromFilePath metodunu silə bilərsiniz, artıq RefreshImage özü edir) ...
        // ... (CanRefreshImage metodu olduğu kimi qalır) ...
        // === DÜZƏLİŞ: Bu metod əskik idi ===
        private bool CanRefreshImage()
        {
            // Yalnız bir şəkil yüklənibsə VƏ emal prosesi getmirsə
            return IsImageLoaded && !IsProcessing;
        }
        // ==================================


        // === Yeni Əmrlər ===

        // === PREVIEW ƏMRİ (DÜZƏLDİLMİŞ) ===
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

                // === DÜZƏLİŞ: Mənbəni WriteableBitmap-dən al ===
                // Hazırkı redaktə edilmiş şəkli PNG byte massivinə çevir
                byte[] currentImageData = GetPngBytesFromWriteableBitmap(LoadedImageSource);
                if (currentImageData == null)
                    throw new Exception("Editing Image data is not found!");

                // 2. Servisə _loadedImagePath YOX, bu YENİ byte[] massivini göndər
                byte[] pngData = await Task.Run(() =>
                     _imageService.RemoveBackground(
                         currentImageData, // <-- DƏYİŞİKLİK
                         StartPixelX,
                         StartPixelY,
                         (float)Tolerance
                     )
                 );
                // === KODUN SONU ===

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
                CustomMessageBox.Show(
     App.GetStr("Str_Msg_ErrGeneral", ex.Message),
     "Str_Title_Error",
     MessageBoxButton.OK,
     MsgImage.Error);
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
                    CustomMessageBox.Show("Str_Msg_SuccessSave", "Str_Title_Success", MessageBoxButton.OK, MsgImage.Success);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(
     App.GetStr("Str_Msg_ErrSaveImage", ex.Message),
     "Str_Title_Error",
     MessageBoxButton.OK,
     MsgImage.Error);
                }
            }
        }

        // === YENİ KÖMƏKÇİ METOD ===
        // WriteableBitmap-in hazırkı vəziyyətini PNG formatında byte[] kimi qaytarır
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


        public bool CanProcess() => IsImageLoaded && !IsProcessing;
        public bool CanSave() => LastProcessedData != null && !IsProcessing;

        // CanExecute üçün NotifyCanExecuteChangedFor əlavə etmək lazımdır...
        // Sadəlik üçün hələlik belə qalsın.
    }
}
