using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Dev.MartijnHoogendoorn.Inking.Behavior.Converters
{
    public class SizeToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null && value is Size)
            {
                return ((Size)value).Height;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value != null)
            {
                double val;
                if (double.TryParse(value.ToString(), out val))
                {
                    return new Size(val, val);
                }
            }

            return null;
        }
    }
}
