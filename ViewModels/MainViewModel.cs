using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpriteEditor.ViewModels;
namespace SpriteEditor.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // 1. Aktiv görünüşü (View) saxlamaq üçün xassə
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        // 2. Digər ViewModel-lərin instanslarını saxlayaq
        private readonly HomeViewModel _homeViewModel;
        private readonly SpriteSlicerViewModel _spriteSlicerViewModel;
        // Gələcəkdə: private readonly ManualCropViewModel _manualCropViewModel;
        private readonly BackgroundEraserViewModel _backgroundEraserViewModel;

        private readonly RiggingViewModel _riggingViewModel;

        private readonly FormatConverterViewModel _formatConverterViewModel;

        private readonly FrameAnimatorViewModel _frameAnimatorViewModel;

        public MainViewModel()
        {
            // Bütün səhifələri yaddaşda yaradırıq
            _homeViewModel = new HomeViewModel();
            _spriteSlicerViewModel = new SpriteSlicerViewModel();
            _backgroundEraserViewModel = new BackgroundEraserViewModel();
            _riggingViewModel = new RiggingViewModel();
            _formatConverterViewModel = new FormatConverterViewModel();
            _frameAnimatorViewModel = new FrameAnimatorViewModel();

            // Proqram açıldıqda "Home" səhifəsini göstər
            _currentViewModel = _homeViewModel;
        }

        // 3. Naviqasiya üçün Əmrlər (Commands)
        [RelayCommand]
        private void GoToHome()
        {
            CurrentViewModel = _homeViewModel;
        }

        [RelayCommand]
        private void GoToSpriteSlicer()
        {
            CurrentViewModel = _spriteSlicerViewModel;
        }

        [RelayCommand] // YENİ
        private void GoToBackgroundEraser()
        {
            CurrentViewModel = _backgroundEraserViewModel;
        }

        [RelayCommand]
        private void GoToRigging()
        {
            CurrentViewModel = _riggingViewModel;
        }

        [RelayCommand]
        private void GoToFormatConverter()
        {
            CurrentViewModel = _formatConverterViewModel;
        }

        [RelayCommand]
        private void GoToFrameAnimator()
        {
            CurrentViewModel = _frameAnimatorViewModel;
        }


        // Gələcəkdə bura yeni əmrlər əlavə edəcəksiniz:
    }
}
