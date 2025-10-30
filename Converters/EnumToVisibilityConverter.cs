using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SpriteEditor.Converters
{
    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string enumValue = value.ToString();
            string targetValue = parameter.ToString();

            if (enumValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase))
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Xətanı bu sətir verirdi: throw new NotImplementedException();

            // DÜZƏLİŞ:
            // WPF-ə bildiririk ki, bu metod heç nə etməməlidir
            return Binding.DoNothing;
        }
    }
}
