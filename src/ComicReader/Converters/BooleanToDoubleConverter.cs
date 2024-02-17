using System;
using Microsoft.UI.Xaml.Data;

namespace ComicReader.Converters
{
    /// <summary>
    /// Value converter that translates true to 1.0 and false to 0.0.
    /// </summary>
    public class BooleanToDoubleConverter : IValueConverter
    {
        public static double Convert(bool bool_value)
        {
            return bool_value ? 1.0 : 0.0;
        }

        public object Convert(object value, Type target_type, object parameter, string language)
        {
            var boxed_bool = (value as bool?);
            var bool_value = (boxed_bool != null && boxed_bool.Value);
            return BooleanToDoubleConverter.Convert(bool_value);
        }

        public object ConvertBack(object value, Type target_type, object parameter, string language)
        {
            var double_boxed = (value as double?);
            return (double_boxed != null && double_boxed.Value > 0.5);
        }
    }

    /// <summary>
    /// Value converter that translates true to 0.0 and false to 1.0.
    /// </summary>
    public class BooleanToDoubleNegationConverter : IValueConverter
    {
        public object Convert(object value, Type target_type, object parameter, string language)
        {
            var boxed_bool = (value as bool?);
            var bool_value = (boxed_bool != null && boxed_bool.Value);
            return BooleanToDoubleConverter.Convert(!bool_value);
        }

        public object ConvertBack(object value, Type target_type, object parameter, string language)
        {
            var double_boxed = (value as double?);
            return (double_boxed != null && double_boxed.Value <= 0.5);
        }
    }
}
