using System.Collections.ObjectModel; // ObservableCollection üçün
using System.IO; // Path üçün
using System.Threading.Tasks; // Asinxron əməliyyatlar üçün (Task)
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SpriteEditor.Services; // Service-i əlavə edirik
using SpriteEditor.ViewModels; // GridLineViewModel üçün

namespace SpriteEditor.ViewModels
{
    public partial class SpriteSlicerViewModel : ObservableObject
    {
        // === Servislər ===
        private readonly ImageService _imageService;

        // === Orijinal Şəkil Məlumatları ===
        [ObservableProperty]
        private BitmapImage _loadedImageSource;

        private string _loadedImagePath; // Kəsmək üçün fayl yolunu yadda saxlamalıyıq

        [ObservableProperty]
        private int _imagePixelWidth;

        [ObservableProperty]
        private int _imagePixelHeight;

        // === Slicer Box Parametrləri ===
        [ObservableProperty]
        private double _slicerX;

        [ObservableProperty]
        private double _slicerY;

        [ObservableProperty]
        private double _slicerWidth;

        [ObservableProperty]
        private double _slicerHeight;

        // === UI Xassələri (Properties) ===
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SliceImageCommand))] // Düymənin aktivliyini yeniləyir
        private bool _isImageLoaded = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SliceImageCommand))]
        private bool _isSlicing = false; // Kəsmə prosesi gedərkən UI-ı bloklamaq üçün

        // === Grid Parametrləri ===
        // Sütun sayı dəyişəndə, 'UpdateGridLines' metodunu çağır
        [ObservableProperty]
        private int _columns = 4;

        // Sətr sayı dəyişəndə, 'UpdateGridLines' metodunu çağır
        [ObservableProperty]
        private int _rows = 4;

        // === UI Xətt Kolleksiyası ===
        // UI-dakı "qırıq-qırıq xətlər" bu kolleksiyaya bağlı olacaq
        public ObservableCollection<GridLineViewModel> GridLines { get; } = new ObservableCollection<GridLineViewModel>();

        // === Constructor ===
        public SpriteSlicerViewModel()
        {
            _imageService = new ImageService();
        }

        // === Metodlar ===

        [RelayCommand]
        private void LoadImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = "Görüntü Faylları (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|Bütün Fayllar (*.*)|*.*";

            if (openDialog.ShowDialog() == true)
            {
                _loadedImagePath = openDialog.FileName; // Yolu yadda saxla

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new System.Uri(_loadedImagePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                LoadedImageSource = bitmap;
                IsImageLoaded = true;

                // Şəklin orijinal ölçülərini götür (Xətlər üçün vacibdir)
                // İNDİ həm də [ObservableProperty] xassələrini yeniləyirik
                ImagePixelWidth = bitmap.PixelWidth;
                ImagePixelHeight = bitmap.PixelHeight;

                // Xətləri yenilə
                UpdateGridLines();

                // === YENİ KOD: Slicer Box-u sıfırla ===
                // Başlanğıcda Slicer Box bütün şəkli əhatə etsin
                SlicerX = 0;
                SlicerY = 0;
                SlicerWidth = bitmap.PixelWidth;
                SlicerHeight = bitmap.PixelHeight;
                // ======================================
            }
        }

        /// <summary>
        /// UI-da göstərilən kəsmə xətlərini Slicer Box-a uyğun yeniləyir.
        /// </summary>
        private void UpdateGridLines()
        {
            GridLines.Clear(); // Köhnə xətləri təmizlə
            if (!IsImageLoaded) return; // Şəkil yoxdursa, heç nə etmə

            // Hücrə eni/hündürlüyünü şəklin yox, Slicer Box-un ölçüsünə görə hesablayırıq
            double cellWidth = (double)SlicerWidth / Columns;
            double cellHeight = (double)SlicerHeight / Rows;

            // 1. Şaquli (Vertical) Xətləri çək
            for (int i = 1; i < Columns; i++)
            {
                // Xəttin X koordinatı SlicerX-dən başlayaraq hesablanır
                double x = SlicerX + (i * cellWidth);

                // Xətt SlicerY-dən başlayır və SlicerY + SlicerHeight-də bitir
                GridLines.Add(new GridLineViewModel { X1 = x, Y1 = SlicerY, X2 = x, Y2 = SlicerY + SlicerHeight });
            }

            // 2. Üfüqi (Horizontal) Xətləri çək
            for (int i = 1; i < Rows; i++)
            {
                // Xəttin Y koordinatı SlicerY-dən başlayaraq hesablanır
                double y = SlicerY + (i * cellHeight);

                // Xətt SlicerX-dən başlayır və SlicerX + SlicerWidth-də bitir
                GridLines.Add(new GridLineViewModel { X1 = SlicerX, Y1 = y, X2 = SlicerX + SlicerWidth, Y2 = y });
            }
        }

        // === Kəsmə Əməliyyatı (Asinxron) ===

        // Bu metod 'CanSliceImage' metodu 'true' qaytardıqda işləyəcək
        [RelayCommand(CanExecute = nameof(CanSliceImage))]
        private async Task SliceImageAsync() // Asinxron (async) edirik ki, UI donmasın
        {
            // 1. İstifadəçidən hara yaddaş saxlayacağını soruş
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Title = "Kəsilmiş spritları hara saxlamaq istəyirsiniz? (Qovluq seçin)";
            saveDialog.FileName = "sprite_0_0.png"; // Nümunə fayl adı
            saveDialog.Filter = "PNG Image (*.png)|*.png";

            if (saveDialog.ShowDialog() == true)
            {
                string outputDirectory = Path.GetDirectoryName(saveDialog.FileName);
                if (outputDirectory == null) return;

                IsSlicing = true; // Proses başladı

                try
                {
                    // 2. Əsas işi (UI-ı dondura biləcək) arxa planda (Task.Run) icra et
                    await Task.Run(() =>
                    {
                        _imageService.SliceSpriteSheet(
                            _loadedImagePath,
                            Columns,
                            Rows,
                            (int)SlicerX,
                            (int)SlicerY,
                            (int)SlicerWidth,
                            (int)SlicerHeight,
                            outputDirectory
                        );
                    });

                    // 3. Bitəndə məlumat ver
                    System.Windows.MessageBox.Show(
                        $"{Columns}x{Rows} ölçüsündə spritlar uğurla kəsildi və '{outputDirectory}' qovluğuna yazıldı.",
                        "Əməliyyat Tamamlandı",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    // Xəta baş verərsə
                    System.Windows.MessageBox.Show(
                        $"Xəta baş verdi: {ex.Message}",
                        "Xəta",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsSlicing = false; // Proses bitdi (həm uğurlu, həm uğursuz halda)
                }
            }
        }

        // 'SliceImageCommand' düyməsinin nə vaxt aktiv olacağını təyin edir
        private bool CanSliceImage()
        {
            return IsImageLoaded && !IsSlicing; // Yalnız şəkil varsa VƏ hal-hazırda kəsmə prosesi getmirsə
        }

        // Sütun/Sətr dəyişəndə avtomatik çağırılacaq (NotifyPropertyChangedFor sayəsində)
        partial void OnColumnsChanged(int value) => UpdateGridLines();
        partial void OnRowsChanged(int value) => UpdateGridLines();

        // YENİ KOD: Slicer Box dəyişəndə avtomatik çağırılacaq
        partial void OnSlicerXChanged(double value) => UpdateGridLines();
        partial void OnSlicerYChanged(double value) => UpdateGridLines();
        partial void OnSlicerWidthChanged(double value) => UpdateGridLines();
        partial void OnSlicerHeightChanged(double value) => UpdateGridLines();
    }
}