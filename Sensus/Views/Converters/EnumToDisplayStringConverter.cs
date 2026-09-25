using System.Globalization;
using System.Windows.Data;
using Sensus.Extensions;

namespace Sensus.Views.Converters
{
    public sealed class EnumToDisplayStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Enum enumValue
                ? enumValue.ToDisplayString()
                : value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
