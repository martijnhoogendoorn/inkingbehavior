using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;

namespace Dev.MartijnHoogendoorn.Inking.Behavior.Converters
{
    public class ListIndexerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            IList<ToggleButton> list = value as IList<ToggleButton>;

            if (list != null && parameter != null)
            {
                var entry = list.FirstOrDefault(p => p.Name == (string)parameter);
                var index = list.IndexOf(entry) + 1;
                Debug.WriteLine("ListIndexerConverter: " + index);
                return index > 0 ? index : 0;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
