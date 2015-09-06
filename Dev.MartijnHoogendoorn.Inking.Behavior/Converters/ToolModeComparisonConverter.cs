using Dev.MartijnHoogendoorn.Inking.Behavior.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Dev.MartijnHoogendoorn.Inking.Behavior.Converters
{
    public class ToolModeComparisonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null && parameter != null)
            {
                InkingToolMode comparisonToolMode;
                return Enum.TryParse<InkingToolMode>((string)parameter, out comparisonToolMode) && comparisonToolMode == (InkingToolMode)value;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (!string.IsNullOrWhiteSpace(parameter as string))
            {
                InkingToolMode selectedToolMode;
                if(Enum.TryParse<InkingToolMode>((string)parameter, out selectedToolMode))
                {
                    return selectedToolMode;
                }
            }

            return null;
        }
    }
}
