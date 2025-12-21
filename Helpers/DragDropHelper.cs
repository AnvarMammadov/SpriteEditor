using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace SpriteEditor.Helpers
{
    /// <summary>
    /// Helper class for Drag & Drop functionality
    /// </summary>
    public static class DragDropHelper
    {
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico" };

        public static void EnableDragDrop(FrameworkElement element, Action<string[]> onFilesDropped)
        {
            if (element == null || onFilesDropped == null)
                return;

            element.AllowDrop = true;
            element.DragEnter += (s, e) => OnDragEnter(e);
            element.DragOver += (s, e) => OnDragOver(e);
            element.Drop += (s, e) => OnDrop(e, onFilesDropped);
        }

        private static void OnDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Any(IsImageFile))
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private static void OnDragOver(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Any(IsImageFile))
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private static void OnDrop(DragEventArgs e, Action<string[]> callback)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        var imageFiles = files.Where(IsImageFile).ToArray();
                        if (imageFiles.Length > 0)
                        {
                            callback(imageFiles);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "DragDrop");
                MessageBox.Show(
                    $"Error processing dropped files:\n\n{ex.Message}",
                    "Drag & Drop Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            e.Handled = true;
        }

        public static bool IsImageFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                return ImageExtensions.Contains(extension);
            }
            catch
            {
                return false;
            }
        }

        public static void ShowDragDropIndicator(FrameworkElement element, bool show)
        {
            if (element == null) return;

            try
            {
                if (show)
                {
                    element.Opacity = 0.7;
                }
                else
                {
                    element.Opacity = 1.0;
                }
            }
            catch { }
        }
    }
}





