using System;
using System.Globalization;
using System.Windows.Data;

namespace Macro.Utils
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string? checkValue = value.ToString();
            string? targetValue = parameter.ToString();
            
            if (checkValue == null || targetValue == null)
                return false;
                
            return checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return System.Windows.Data.Binding.DoNothing;

            bool useValue = (bool)value;
            string? targetValue = parameter.ToString();
            
            if (useValue && targetValue != null)
            {
                try
                {
                    return Enum.Parse(targetType, targetValue);
                }
                catch
                {
                    return System.Windows.Data.Binding.DoNothing;
                }
            }

            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
