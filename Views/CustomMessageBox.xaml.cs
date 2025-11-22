using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SpriteEditor.Views
{
    /// <summary>
    /// Custom MessageBox növləri (ikonlar üçün)
    /// </summary>
    public enum MsgImage
    {
        None,
        Info,
        Warning,
        Error,
        Success
    }

    public partial class CustomMessageBox : Window
    {
        // Nəticəni saxlamaq üçün
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        private CustomMessageBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// ƏSAS ÇAĞIRIŞ METODU (Static)
        /// </summary>
        public static MessageBoxResult Show(string messageKeyOrText, string titleKeyOrText = "Info", MessageBoxButton buttons = MessageBoxButton.OK, MsgImage image = MsgImage.Info)
        {
            // 1. Yeni pəncərə yaradın
            var msgBox = new CustomMessageBox();

            // 2. Mətnləri tərcümə edin (Əgər Resursdursa tapacaq, deyilsə olduğu kimi qalacaq)
            msgBox.TxtTitle.Text = GetLocalizedText(titleKeyOrText);
            msgBox.TxtMessage.Text = GetLocalizedText(messageKeyOrText);

            // 3. Düymələri tənzimləyin
            msgBox.SetupButtons(buttons);

            // 4. İkonu tənzimləyin
            msgBox.SetupImage(image);

            // 5. Sahib pəncərəni tapın (Mərkəzdə açılması üçün)
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                msgBox.Owner = Application.Current.MainWindow;
            }

            // 6. Pəncərəni Dialog kimi açın
            msgBox.ShowDialog();

            return msgBox.Result;
        }

        // Resursdan mətni tapmaq üçün köməkçi metod
        private static string GetLocalizedText(string key)
        {
            if (Application.Current.Resources.Contains(key))
            {
                return Application.Current.Resources[key] as string;
            }
            return key; // Resurs tapılmasa, mətni olduğu kimi qaytar
        }

        private void SetupButtons(MessageBoxButton buttons)
        {
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnOk.Focus();
                    break;
                case MessageBoxButton.OKCancel:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnCancel.Focus();
                    break;
                case MessageBoxButton.YesNo:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnYes.Focus();
                    break;
                case MessageBoxButton.YesNoCancel:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnYes.Focus();
                    break;
            }
        }

        private void SetupImage(MsgImage image)
        {
            // Burada ikonları dəyişə bilərsiniz. Hələlik rəngləri dəyişək.
            var accentBrush = Application.Current.Resources["Brush.Accent.Primary"] as SolidColorBrush;

            switch (image)
            {
                case MsgImage.Error:
                    IconPath.Fill = Brushes.Red;
                    // IconPath.Data = ... (Error ikonu varsa bura qoya bilərsiz)
                    break;
                case MsgImage.Warning:
                    IconPath.Fill = Brushes.Orange;
                    break;
                case MsgImage.Success:
                    IconPath.Fill = Brushes.LimeGreen;
                    break;
                case MsgImage.Info:
                default:
                    IconPath.Fill = accentBrush;
                    break;
            }
        }

        // === Hadisələr (Events) ===

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }
    }
}
