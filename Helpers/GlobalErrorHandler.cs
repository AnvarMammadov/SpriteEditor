using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace SpriteEditor.Helpers
{
    /// <summary>
    /// Global error handler for catching unhandled exceptions
    /// </summary>
    public static class GlobalErrorHandler
    {
        private static string _logFilePath;
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            // Setup log file path
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpriteEditorPro",
                "Logs"
            );
            Directory.CreateDirectory(appDataFolder);
            _logFilePath = Path.Combine(appDataFolder, $"error_log_{DateTime.Now:yyyyMMdd}.txt");

            // Hook into all exception sources
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            _isInitialized = true;
            LogInfo("Application started successfully");
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleException(ex, "AppDomain.UnhandledException", isFatal: e.IsTerminating);
            }
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandleException(e.Exception, "DispatcherUnhandledException");
            
            // Mark as handled to prevent app crash
            e.Handled = true;
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleException(e.Exception, "UnobservedTaskException");
            
            // Mark as observed to prevent app crash
            e.SetObserved();
        }

        private static void HandleException(Exception ex, string source, bool isFatal = false)
        {
            try
            {
                // Log to file
                LogError(ex, source);

                // Show user-friendly error dialog
                ShowErrorDialog(ex, isFatal);
            }
            catch
            {
                // Last resort: show basic MessageBox
                MessageBox.Show(
                    $"A critical error occurred:\n\n{ex.Message}\n\nThe application may need to restart.",
                    "Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            if (isFatal)
            {
                Environment.Exit(1);
            }
        }

        private static void ShowErrorDialog(Exception ex, bool isFatal)
        {
            string title = isFatal ? "Critical Error" : "Unexpected Error";
            string message;

            // Create user-friendly error messages based on exception type
            message = ex switch
            {
                FileNotFoundException => "A required file could not be found. Please reinstall the application.",
                UnauthorizedAccessException => "The application does not have permission to access a required resource.",
                OutOfMemoryException => "The system is running low on memory. Please close other applications and try again.",
                InvalidOperationException => "An invalid operation was attempted. Your recent changes may not have been saved.",
                ArgumentException => "Invalid input was provided. Please check your entries and try again.",
                IOException => "A file operation failed. The file may be in use by another program.",
                _ => $"An unexpected error occurred:\n\n{ex.Message}\n\nError Type: {ex.GetType().Name}"
            };

            if (isFatal)
            {
                message += "\n\nThe application will now close.";
            }
            else
            {
                message += "\n\nYou can continue working, but it's recommended to save your work and restart the application.";
            }

            message += $"\n\nError details have been saved to:\n{_logFilePath}";

            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                isFatal ? MessageBoxImage.Error : MessageBoxImage.Warning
            );
        }

        public static void LogError(Exception ex, string context = "")
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine($"ERROR LOGGED: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Context: {context}");
                sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Stack Trace:");
                sb.AppendLine(ex.StackTrace);

                if (ex.InnerException != null)
                {
                    sb.AppendLine("\nInner Exception:");
                    sb.AppendLine($"  Type: {ex.InnerException.GetType().FullName}");
                    sb.AppendLine($"  Message: {ex.InnerException.Message}");
                    sb.AppendLine($"  Stack Trace:");
                    sb.AppendLine($"  {ex.InnerException.StackTrace}");
                }

                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine();

                File.AppendAllText(_logFilePath, sb.ToString());
            }
            catch
            {
                // Silently fail if logging fails
            }
        }

        public static void LogInfo(string message)
        {
            try
            {
                string logEntry = $"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";
                File.AppendAllText(_logFilePath, logEntry);
            }
            catch
            {
                // Silently fail
            }
        }

        public static void LogWarning(string message)
        {
            try
            {
                string logEntry = $"[WARNING] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";
                File.AppendAllText(_logFilePath, logEntry);
            }
            catch
            {
                // Silently fail
            }
        }

        public static string GetLogFilePath() => _logFilePath;

        public static void OpenLogFolder()
        {
            try
            {
                string folder = Path.GetDirectoryName(_logFilePath);
                if (Directory.Exists(folder))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folder);
                }
            }
            catch
            {
                // Silently fail
            }
        }
    }
}

