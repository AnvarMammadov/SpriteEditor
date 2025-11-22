using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace SpriteEditor.Converters
{
    public class BooleanToCursorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Məntiq: IsVertical (bool)
            // true  -> Şaquli xəttdir -> Sola/Sağa çəkmək lazımdır -> Cursors.SizeWE
            // false -> Üfüqi xəttdir  -> Yuxarı/Aşağı çəkmək lazımdır -> Cursors.SizeNS

            if (value is bool isVertical && isVertical)
            {
                return Cursors.SizeWE;
            }

            return Cursors.SizeNS;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
