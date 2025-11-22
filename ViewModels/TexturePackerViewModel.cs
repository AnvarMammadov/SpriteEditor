using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SpriteEditor.Data;
using SpriteEditor.Services;
using SpriteEditor.Views;

namespace SpriteEditor.ViewModels
{
    public partial class TexturePackerViewModel : ObservableObject
    {
        private readonly TexturePackerService _packerService;

        // === Parametrlər ===

        // Atlas ölçüləri üçün siyahı (ComboBox üçün)
        public List<int> AtlasSizes { get; } = new List<int> { 512, 1024, 2048, 4096, 8192 };

        [ObservableProperty]
        private int _selectedWidth = 2048;

        [ObservableProperty]
        private int _selectedHeight = 2048;

        [ObservableProperty]
        private int _padding = 2; // Spritlar arası məsafə

        // === Məlumatlar ===

        // Yüklənmiş faylların siyahısı (sadəcə yollar)
        public ObservableCollection<string> LoadedFiles { get; } = new ObservableCollection<string>();

        // Ekranda göstərmək üçün Atlasın nəticəsi
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveAtlasCommand))]
        private BitmapImage _previewAtlas;

        // Yaddaşa yazmaq üçün Atlasın xam (byte[]) forması
        private byte[] _generatedAtlasBytes;
        private List<PackedSprite> _generatedMap; // JSON üçün

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PackCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveAtlasCommand))]
        private bool _isProcessing;

        public TexturePackerViewModel()
        {
            _packerService = new TexturePackerService();
        }

        // === Əmrlər (Commands) ===

        [RelayCommand]
        private void LoadImages()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image Files|*.png;*.jpg;*.bmp",
                Title = "Select Sprites to Pack"
            };

            if (openDialog.ShowDialog() == true)
            {
                foreach (var file in openDialog.FileNames)
                {
                    // Təkrar yükləmənin qarşısını al
                    if (!LoadedFiles.Contains(file))
                    {
                        LoadedFiles.Add(file);
                    }
                }

                // === DÜZƏLİŞ: Düyməyə xəbər ver ki, siyahı dəyişdi ===
                PackCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        private void ClearList()
        {
            LoadedFiles.Clear();
            PreviewAtlas = null;
            // _generatedAtlasBytes = null; // (private sahəni birbaşa sıfırlaya bilərsən və ya property istifadə edə bilərsən)

            // === DÜZƏLİŞ: Düyməyə xəbər ver ki, siyahı boşaldı ===
            PackCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanPack))]
        private async Task Pack()
        {
            IsProcessing = true;
            try
            {
                // Servisi çağırırıq
                var result = await _packerService.PackImagesAsync(
                    LoadedFiles.ToList(),
                    SelectedWidth,
                    SelectedHeight,
                    Padding);

                _generatedAtlasBytes = result.AtlasImage;
                _generatedMap = result.Map;

                // Byte massivini Ekranda göstərmək üçün BitmapImage-ə çeviririk
                using (var ms = new MemoryStream(_generatedAtlasBytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    PreviewAtlas = bmp;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.Message, "Str_Title_Error", System.Windows.MessageBoxButton.OK, MsgImage.Error);
            }
            finally
            {
                IsProcessing = false;
                // Hər ehtimala qarşı burada da çağırmaq olar, amma yuxarıdakı bəs edir
            }
        }

        private bool CanPack() => LoadedFiles.Count > 0 && !IsProcessing;

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAtlas()
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                FileName = "Atlas_01",
                Filter = "PNG Image|*.png",
                Title = "Save Atlas Image"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // 1. PNG faylını yaz
                    await File.WriteAllBytesAsync(saveDialog.FileName, _generatedAtlasBytes);

                    // 2. JSON faylını hazırla
                    string jsonPath = Path.ChangeExtension(saveDialog.FileName, ".json");
                    string jsonContent = System.Text.Json.JsonSerializer.Serialize(_generatedMap, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                    // 3. JSON faylını yaz
                    await File.WriteAllTextAsync(jsonPath, jsonContent);

                    CustomMessageBox.Show("Str_Msg_SuccessSave", "Str_Title_Success", System.Windows.MessageBoxButton.OK, MsgImage.Success);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(ex.Message, "Str_Title_Error", System.Windows.MessageBoxButton.OK, MsgImage.Error);
                }
            }
        }

        private bool CanSave() => _generatedAtlasBytes != null && !IsProcessing;
    }
}
