using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace fflauncher.Helpers
{
    public class EndpointAddressConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var address = values[0]?.ToString();
            var endpoint = values[1]?.ToString();
            var mode = values[2]?.ToString();

            if (string.Equals(mode, "online", StringComparison.OrdinalIgnoreCase))
                return endpoint ?? address ?? "";

            return address ?? endpoint ?? "";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
