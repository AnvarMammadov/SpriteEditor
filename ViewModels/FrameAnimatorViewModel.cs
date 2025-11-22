using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SpriteEditor.Data;
using SpriteEditor.Services;
using SpriteEditor.Views;

namespace SpriteEditor.ViewModels
{
    public partial class FrameAnimatorViewModel : ObservableObject
    {
        private readonly ImageService _imageService;
        private DispatcherTimer _timer;

        // --- Properties ---

        public ObservableCollection<FrameData> Frames { get; } = new ObservableCollection<FrameData>();

        [ObservableProperty]
        private FrameData _currentFrame;

        [ObservableProperty]
        private int _fps = 12; // Standart animasiya sürəti

        [ObservableProperty]
        private bool _isLooping = true;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(ExportGifCommand))]
        private bool _hasFrames = false;

        [ObservableProperty]
        private bool _isPlaying = false;

        private int _currentIndex = 0;



        [ObservableProperty]
        private bool _isPingPong = false; // Ping-Pong aktivdirmi?

        private bool _isReversing = false; // Hazırda geriyə gedirik?


        // --- Constructor ---
        public FrameAnimatorViewModel()
        {
            _imageService = new ImageService();
            _timer = new DispatcherTimer();
            _timer.Tick += Timer_Tick;
            UpdateTimerInterval();
        }

        // --- Methods ---

        partial void OnFpsChanged(int value)
        {
            UpdateTimerInterval();
        }

        private void UpdateTimerInterval()
        {
            if (Fps > 0)
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / Fps);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (Frames.Count == 0) return;

            // İstiqamətə görə indeksi artır və ya azalt
            if (IsPingPong && _isReversing)
            {
                _currentIndex--;
            }
            else
            {
                _currentIndex++;
            }

            // Sərhədləri yoxla
            if (_currentIndex >= Frames.Count)
            {
                // Sona çatdıq
                if (IsPingPong)
                {
                    // Ping-Pong: Geriyə dön
                    _isReversing = true;
                    _currentIndex = Math.Max(0, Frames.Count - 2); // Sonuncudan əvvəlki kadr
                }
                else if (IsLooping)
                {
                    // Normal Loop: Başa dön
                    _currentIndex = 0;
                }
                else
                {
                    // Dayan
                    Stop();
                    return;
                }
            }
            else if (_currentIndex < 0)
            {
                // Ping-Pong rejimində geriyə gedib başa çatdıq (0-dan aşağı düşdük)
                _isReversing = false; // İrəli dön
                _currentIndex = 1; // İkinci kadr
            }

            // İndeksi təhlükəsiz şəkildə tətbiq et
            if (_currentIndex >= 0 && _currentIndex < Frames.Count)
            {
                CurrentFrame = Frames[_currentIndex];
            }
        }

        [RelayCommand]
        private void LoadFrames()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Multiselect = true, // Çoxlu seçimə icazə ver
                Filter = "Image Files|*.png;*.jpg;*.bmp;*.webp",
                Title = "Select Animation Frames"
            };

            if (openDialog.ShowDialog() == true)
            {
                // Əvvəlcə dayandır və təmizlə
                Stop();
                Frames.Clear();

                // Fayl adlarına görə sırala (məs: run_01, run_02 ardıcıl olsun)
                var sortedFiles = openDialog.FileNames.OrderBy(f => f).ToList();

                foreach (var file in sortedFiles)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(file);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    Frames.Add(new FrameData
                    {
                        FilePath = file,
                        ImageSource = bitmap,
                        Name = Path.GetFileName(file)
                    });
                }

                if (Frames.Count > 0)
                {
                    CurrentFrame = Frames[0];
                    _currentIndex = 0;
                    HasFrames = true;
                }
            }
        }

        [RelayCommand(CanExecute = nameof(HasFrames))]
        private void Play()
        {
            if (IsPlaying)
            {
                // Pause logic
                _timer.Stop();
                IsPlaying = false;
            }
            else
            {
                // Play logic
                UpdateTimerInterval();
                _timer.Start();
                IsPlaying = true;
            }
        }

        [RelayCommand]
        private void Stop()
        {
            _timer.Stop();
            IsPlaying = false;
            _currentIndex = 0;
            _isReversing = false;
            if (Frames.Count > 0) CurrentFrame = Frames[0];
        }

        [RelayCommand(CanExecute = nameof(HasFrames))]
        private void ExportGif()
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "GIF Image|*.gif",
                FileName = "animation.gif"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // FrameModel-dən fayl yollarını çıxarırıq
                    var paths = Frames.Select(f => f.FilePath).ToList();

                    // 1000 / FPS = bir kadrın ms müddəti
                    int delay = 1000 / Fps;

                    _imageService.CreateGifFromImages(paths, delay, saveDialog.FileName);

                    CustomMessageBox.Show("Str_Msg_GifSaved", "Str_Title_Success",
                        System.Windows.MessageBoxButton.OK, MsgImage.Success);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error: {ex.Message}", "Str_Title_Error",
                        System.Windows.MessageBoxButton.OK, MsgImage.Error);
                }
            }
        }
    }
}
