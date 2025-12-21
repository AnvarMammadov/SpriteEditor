using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace SpriteEditor.Helpers
{
    /// <summary>
    /// Manages keyboard shortcuts across the application
    /// </summary>
    public static class KeyboardShortcutManager
    {
        private static Dictionary<KeyGesture, Action> _shortcuts = new();

        public static void Initialize(Window mainWindow)
        {
            mainWindow.PreviewKeyDown += OnKeyDown;
        }

        public static void RegisterShortcut(Key key, ModifierKeys modifiers, Action action, string description = "")
        {
            var gesture = new KeyGesture(key, modifiers);
            _shortcuts[gesture] = action;
        }

        public static void RegisterShortcut(Key key, Action action, string description = "")
        {
            RegisterShortcut(key, ModifierKeys.None, action, description);
        }

        public static void UnregisterShortcut(Key key, ModifierKeys modifiers)
        {
            var gesture = new KeyGesture(key, modifiers);
            if (_shortcuts.ContainsKey(gesture))
            {
                _shortcuts.Remove(gesture);
            }
        }

        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                var modifiers = Keyboard.Modifiers;
                var key = e.Key == Key.System ? e.SystemKey : e.Key;

                var gesture = new KeyGesture(key, modifiers);

                if (_shortcuts.TryGetValue(gesture, out var action))
                {
                    action?.Invoke();
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "KeyboardShortcut");
            }
        }

        public static Dictionary<string, string> GetAllShortcuts()
        {
            var shortcuts = new Dictionary<string, string>
            {
                { "Ctrl + S", "Save current work" },
                { "Ctrl + O", "Open file" },
                { "Ctrl + N", "New project" },
                { "Ctrl + W", "Close current tab" },
                { "Ctrl + Z", "Undo (coming soon)" },
                { "Ctrl + Y", "Redo (coming soon)" },
                { "Ctrl + C", "Copy" },
                { "Ctrl + V", "Paste" },
                { "Ctrl + X", "Cut" },
                { "Del / Delete", "Delete selected item" },
                { "F1", "Help" },
                { "F5", "Refresh / Reload" },
                { "F11", "Toggle fullscreen" },
                { "Esc", "Cancel / Close dialog" },
                { "Ctrl + Plus", "Zoom in" },
                { "Ctrl + Minus", "Zoom out" },
                { "Ctrl + 0", "Reset zoom" },
                { "Space + Drag", "Pan canvas" },
            };

            return shortcuts;
        }
    }
}





