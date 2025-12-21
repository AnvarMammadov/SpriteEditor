using CommunityToolkit.Mvvm.ComponentModel;

namespace SpriteEditor.ViewModels
{
    public partial class SvgLayerItem : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _xmlContent;

        [ObservableProperty]
        private bool _isVisible = true;

        // Callback function to notify parent
        private readonly System.Action _onVisibilityChanged;

        public SvgLayerItem(string name, string xmlContent, System.Action onVisibilityChanged)
        {
            _name = name;
            _xmlContent = xmlContent;
            _onVisibilityChanged = onVisibilityChanged;
        }

        // Generated method implementation
        partial void OnIsVisibleChanged(bool value)
        {
            _onVisibilityChanged?.Invoke();
        }
    }
}