using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using SpriteEditor.Data.Story;

namespace SpriteEditor.Converters
{
    public class VariableTypeToOperationsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = TargetVariableName (string)
            // values[1] = GlobalVariables (List)

            if (values.Length < 2 || values[0] == null || values[1] == null)
                return null;

            string varName = values[0].ToString();
            var variables = values[1] as IEnumerable<StoryVariable>;

            if (variables == null) return null;

            var variable = variables.FirstOrDefault(v => v.Name == varName);
            if (variable == null) return Enum.GetValues(typeof(ActionOperation)); // Default hamısını göstər

            // TİPƏ GÖRƏ SİYAHINI QAYTARIRIQ
            switch (variable.Type)
            {
                case VariableType.Boolean:
                    // Boolean üçün: Add/Subtract olmaz! Yalnız Set (=) və Toggle (!)
                    return new List<ActionOperation> { ActionOperation.Set, ActionOperation.Toggle };

                case VariableType.Integer:
                    // Integer üçün: Set (=), Add (+), Subtract (-)
                    return new List<ActionOperation> { ActionOperation.Set, ActionOperation.Add, ActionOperation.Subtract };

                case VariableType.String:
                    // String üçün: Yalnız Set (=)
                    return new List<ActionOperation> { ActionOperation.Set };

                default:
                    return Enum.GetValues(typeof(ActionOperation));
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
