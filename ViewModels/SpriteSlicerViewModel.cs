using System.Collections.ObjectModel; // ObservableCollection üçün
using System.IO; // Path üçün
using System.Runtime.InteropServices;
using System.Threading.Tasks; // Asinxron əməliyyatlar üçün (Task)
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SpriteEditor.Services; // Service-i əlavə edirik
using SpriteEditor.ViewModels;
using SpriteEditor.Views; // GridLineViewModel üçün

namespace SpriteEditor.ViewModels
{
    public partial class SpriteSlicerViewModel : ObservableObject
    {

        // === Windows Shell Notifier (P/Invoke) ===
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

        // 2. BU BLOKU ƏLAVƏ EDİN
        // Slicer çoxlu fayl yaratdığı üçün birbaşa qovluğa xəbərdarlıq edirik
        private const int SHCNE_UPDATEDIR = 0x00001000; // Qovluq məzmunu dəyişdi
        private const int SHCNF_PATH = 0x0001;          // dwItem1 bir yoldur
        // ==========================================


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
        [NotifyCanExecuteChangedFor(nameof(AutoDetectSpritesCommand))]
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


        // === AVTOMATİK KƏSİM PARAMETRLƏRİ ===

        // Tapılmış spriteları ekranda göstərmək üçün (Qırmızı çərçivələr)
        public ObservableCollection<Int32Rect> DetectedRects { get; } = new ObservableCollection<Int32Rect>();

        [ObservableProperty]
        private bool _useAutoDetection = false; // Grid modu ilə Avto mod arasında keçid




        // === Constructor ===
        public SpriteSlicerViewModel()
        {
            _imageService = new ImageService();
        }

        // === Metodlar ===

        [RelayCommand]
        private void LoadImage()
        {
            string imgFilter = App.GetStr("Str_Filter_Images");
            string allFilter = App.GetStr("Str_Filter_AllFiles");
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = $"{imgFilter} (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|{allFilter} (*.*)|*.*";

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

        // SliceImageAsync metodunu dəyişirik ki, rejimə görə işləsin
        [RelayCommand(CanExecute = nameof(CanSliceImage))]
        private async Task SliceImageAsync()
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Title = "Kəsilmiş spritları hara saxlamaq istəyirsiniz?";
            saveDialog.FileName = "sprite_output.png";

            if (saveDialog.ShowDialog() == true)
            {
                string outputDirectory = Path.GetDirectoryName(saveDialog.FileName);
                IsSlicing = true;

                try
                {
                    await Task.Run(() =>
                    {
                        if (UseAutoDetection)
                        {
                            // === YENİ: Avtomatik tapılanları kəs ===
                            _imageService.SliceByRects(_loadedImagePath, DetectedRects.ToList(), outputDirectory);
                        }
                        else
                        {
                            // === KÖHNƏ: Grid ilə kəs ===
                            _imageService.SliceSpriteSheet(
                                _loadedImagePath, Columns, Rows,
                                (int)SlicerX, (int)SlicerY, (int)SlicerWidth, (int)SlicerHeight,
                                outputDirectory
                            );
                        }
                    });

                    // Qovluğu yenilə (Shell Notify)
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATH, Marshal.StringToHGlobalAuto(outputDirectory), IntPtr.Zero);
                    MessageBox.Show("Uğurla kəsildi!", "Hazırdır");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Xəta: {ex.Message}");
                }
                finally
                {
                    IsSlicing = false;
                }
            }
        }


        // Bu yeni əmrdir
        [RelayCommand(CanExecute = nameof(CanSliceImage))]
        private async Task AutoDetectSpritesAsync()
        {
            if (!IsImageLoaded) return;

            IsSlicing = true; // Düymələri deaktiv et
            DetectedRects.Clear();
            GridLines.Clear();

            try
            {
                // Prosesi işlət
                var rects = await Task.Run(() => _imageService.DetectSprites(_loadedImagePath));

                // Nəticələri əlavə et
                foreach (var r in rects) DetectedRects.Add(r);

                if (rects.Count > 0)
                {
                    UseAutoDetection = true; // Bu dəyişən XAML-da Yaşıl Qutunu gizlədəcək
                    CustomMessageBox.Show(
    App.GetStr("Str_Msg_SpritesFound", rects.Count),
    "Str_Title_Completed",
    MessageBoxButton.OK,
    MsgImage.Info);
                }
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
                // === BU HİSSƏ ÇOX VACİBDİR ===
                IsSlicing = false; // Düymələri yenidən aktivləşdir

                // WPF-ə məcbur edirik ki, düymələrin vəziyyətini yoxlasın
                SliceImageCommand.NotifyCanExecuteChanged();
                AutoDetectSpritesCommand.NotifyCanExecuteChanged();
            }
        }

        // 'SliceImageCommand' düyməsinin nə vaxt aktiv olacağını təyin edir
        private bool CanSliceImage()
        {
            return IsImageLoaded && !IsSlicing; // Yalnız şəkil varsa VƏ hal-hazırda kəsmə prosesi getmirsə
        }

        // Sütun/Sətr dəyişəndə avtomatik çağırılacaq (NotifyPropertyChangedFor sayəsində)
        partial void OnColumnsChanged(int value) { UseAutoDetection = false; UpdateGridLines(); }
        partial void OnRowsChanged(int value) { UseAutoDetection = false; UpdateGridLines(); }

        // YENİ KOD: Slicer Box dəyişəndə avtomatik çağırılacaq
        partial void OnSlicerXChanged(double value) => UpdateGridLines();
        partial void OnSlicerYChanged(double value) => UpdateGridLines();
        partial void OnSlicerWidthChanged(double value) => UpdateGridLines();
        partial void OnSlicerHeightChanged(double value) => UpdateGridLines();
        // Columns/Rows dəyişəndə UseAutoDetection = false etmək yaxşı olar
      
    }
}