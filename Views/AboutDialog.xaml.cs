using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace SpriteEditor.Views
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            LoadVersionInfo();
            LoadLicenseStatus();
        }

        private void LoadVersionInfo()
        {
            try
            {
                // Get assembly version
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                var buildDate = GetBuildDate(Assembly.GetExecutingAssembly());
                
                BuildNumberText.Text = $"{buildDate:yyyy.MM.dd}.{version?.Build ?? 0}";
                
                // Detect platform
                PlatformText.Text = Environment.Is64BitProcess ? "Windows x64" : "Windows x86";
            }
            catch
            {
                // Fallback if reflection fails
                BuildNumberText.Text = "2025.01.15.001";
                PlatformText.Text = "Windows";
            }
        }

        private void LoadLicenseStatus()
        {
            // TODO: Implement actual license checking
            // For now, show trial status
            bool isLicensed = false; // Check from license manager
            int daysRemaining = 30;  // Calculate actual remaining days

            if (isLicensed)
            {
                LicenseStatusText.Text = "LICENSED - Professional Edition";
                LicenseStatusText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Accent.Green");
            }
            else
            {
                LicenseStatusText.Text = $"FREE TRIAL ({daysRemaining} days remaining)";
                LicenseStatusText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Accent.Primary");
            }
        }

        private DateTime GetBuildDate(Assembly assembly)
        {
            try
            {
                var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (attribute != null && DateTime.TryParse(attribute.InformationalVersion, out var date))
                {
                    return date;
                }
            }
            catch { }

            // Fallback to current date
            return DateTime.Now;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void EnterLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open license activation dialog
            MessageBox.Show(
                "License activation feature coming soon!\n\nFor now, you can continue using the full trial version.",
                "Activation",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void Website_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://spriteeditorpro.com");
        }

        private void Docs_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://docs.spriteeditorpro.com");
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://discord.gg/spriteeditor");
        }

        private void ReportBug_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/yourusername/SpriteEditor/issues");
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open URL:\n{url}\n\nError: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}

