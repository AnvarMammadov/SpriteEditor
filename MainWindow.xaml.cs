using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SpriteEditor.ViewModels;
using SpriteEditor.Helpers;
using SpriteEditor.Helpers.UndoRedo;

namespace SpriteEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
            InitializeKeyboardShortcuts();
        }

        private void InitializeKeyboardShortcuts()
        {
            // Initialize keyboard shortcuts manager
            KeyboardShortcutManager.Initialize(this);

            // Register global shortcuts
            KeyboardShortcutManager.RegisterShortcut(Key.F1, ModifierKeys.None, ShowHelp, "Show Help");
            KeyboardShortcutManager.RegisterShortcut(Key.F11, ModifierKeys.None, ToggleFullscreen, "Toggle Fullscreen");
            KeyboardShortcutManager.RegisterShortcut(Key.Escape, ModifierKeys.None, HandleEscape, "Escape");
            
            // Undo/Redo shortcuts
            KeyboardShortcutManager.RegisterShortcut(Key.Z, ModifierKeys.Control, PerformUndo, "Undo");
            KeyboardShortcutManager.RegisterShortcut(Key.Y, ModifierKeys.Control, PerformRedo, "Redo");
            KeyboardShortcutManager.RegisterShortcut(Key.Z, ModifierKeys.Control | ModifierKeys.Shift, PerformRedo, "Redo (Alt)");
        }

        private void ShowHelp()
        {
            var shortcuts = KeyboardShortcutManager.GetAllShortcuts();
            string helpText = "Keyboard Shortcuts:\n\n";
            foreach (var shortcut in shortcuts)
            {
                helpText += $"{shortcut.Key,-20} - {shortcut.Value}\n";
            }
            
            MessageBox.Show(
                helpText,
                "Keyboard Shortcuts",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void ToggleFullscreen()
        {
            if (WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.None;
            }
            else
            {
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
            }
        }

        private void HandleEscape()
        {
            // Future: Handle escape in different contexts
            // For now, do nothing
        }

        private void PerformUndo()
        {
            try
            {
                UndoRedoManager.Instance.Undo();
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "Undo");
                MessageBox.Show($"Undo failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PerformRedo()
        {
            try
            {
                UndoRedoManager.Instance.Redo();
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "Redo");
                MessageBox.Show($"Redo failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        // Dil seçiləndə
        private void Language_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string langCode)
            {
                (Application.Current as App).ChangeLanguage(langCode);
            }
        }
        // Settings düyməsinə basanda menyunu aç
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                // BU SƏTRİ MÜTLƏQ ƏLAVƏ EDİN:
                // Menyunu açmazdan əvvəl ona deyirik ki, "Sən bu düyməyə (btn) bağlanmalısan"
                btn.ContextMenu.PlacementTarget = btn;

                // Sonra açırıq
                btn.ContextMenu.IsOpen = true;
            }
        }

        // About dialog
        private void About_Click(object sender, RoutedEventArgs e)
        {
            var aboutDialog = new Views.AboutDialog
            {
                Owner = this
            };
            aboutDialog.ShowDialog();
        }
    }
}