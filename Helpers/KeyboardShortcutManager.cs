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

                // Filter out invalid keys that are actually modifiers
                if (IsModifierKey(key))
                {
                    return; // Don't process modifier keys as regular keys
                }

                // Don't create gestures for keys without modifiers (except function keys, etc.)
                if (modifiers == ModifierKeys.None && !IsStandaloneKey(key))
                {
                    return;
                }

                try
                {
                    var gesture = new KeyGesture(key, modifiers);

                    if (_shortcuts.TryGetValue(gesture, out var action))
                    {
                        action?.Invoke();
                        e.Handled = true;
                    }
                }
                catch (NotSupportedException)
                {
                    // Ignore invalid gestures (e.g., specific keys without modifiers that KeyGesture doesn't support)
                }
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "KeyboardShortcut");
            }
        }

        /// <summary>
        /// Checks if a key is a modifier key (Ctrl, Alt, Shift, Win)
        /// </summary>
        private static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LWin || key == Key.RWin;
        }

        /// <summary>
        /// Checks if a key can be used standalone without modifiers
        /// </summary>
        private static bool IsStandaloneKey(Key key)
        {
            // Function keys, Escape, Delete, etc. can be standalone
            return (key >= Key.F1 && key <= Key.F24) ||
                   key == Key.Escape ||
                   key == Key.Delete ||
                   key == Key.Tab ||
                   key == Key.Enter ||
                   key == Key.Space;
        }

        public static Dictionary<string, string> GetAllShortcuts()
        {
            var shortcuts = new Dictionary<string, string>
            {
                { "Ctrl + S", "Save current work" },
                { "Ctrl + O", "Open file" },
                { "Ctrl + N", "New project" },
                { "Ctrl + W", "Close current tab" },
                { "Ctrl + Z", "Undo" },
                { "Ctrl + Y", "Redo" },
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





