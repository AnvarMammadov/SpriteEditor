using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using SpriteEditor.Data.Story;

namespace SpriteEditor.Converters
{
    public class VariableTypeToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = ConditionVariableName (string) - Seçilmiş dəyişənin adı
            // values[1] = GlobalVariables (List<StoryVariable>) - Bütün dəyişənlər
            // parameter = "Boolean" və ya "Other" (Hansı kontrolu yoxlayırıq?)

            // 1. Məlumatların düzgünlüyünü yoxla
            if (values.Length < 2 || values[0] == null || values[1] == null)
            {
                // Əgər heç nə seçilməyibsə, standart olaraq TextBox ("Other") göstər
                return parameter?.ToString() == "Other" ? Visibility.Visible : Visibility.Collapsed;
            }

            string selectedVarName = values[0].ToString();
            var variables = values[1] as IEnumerable<StoryVariable>;

            if (variables == null) return Visibility.Collapsed;

            // 2. Adına görə dəyişəni tap
            var selectedVariable = variables.FirstOrDefault(v => v.Name == selectedVarName);

            // Tapılmadısa, TextBox ("Other") göstər
            if (selectedVariable == null)
                return parameter?.ToString() == "Other" ? Visibility.Visible : Visibility.Collapsed;

            // 3. Tipinə görə qərar ver
            bool isBoolean = selectedVariable.Type == VariableType.Boolean;
            string targetControl = parameter?.ToString();

            if (targetControl == "Boolean")
            {
                // Əgər Boolean-dırsa, ComboBox görünsün
                return isBoolean ? Visibility.Visible : Visibility.Collapsed;
            }
            else // "Other"
            {
                // Əgər Boolean DEYİL-sə, TextBox görünsün
                return !isBoolean ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
