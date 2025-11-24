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
using System.Windows.Navigation;
using System.Windows.Shapes;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views
{
    /// <summary>
    /// Interaction logic for StoryPlayerWindow.xaml
    /// </summary>
    public partial class StoryPlayerWindow : Window
    {
        public StoryPlayerWindow()
        {
            InitializeComponent();
            this.Closing += StoryPlayerWindow_Closing;
        }


        private void StoryPlayerWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.DataContext is StoryPlayerViewModel vm)
            {
                vm.Cleanup(); // Musiqini dayandır
            }
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (this.DataContext is ViewModels.StoryPlayerViewModel vm)
            {
                // Əgər mətn hələ yazılırsa, tamamla
                vm.CompleteTextCommand.Execute(null);
            }
        }
    }
}
