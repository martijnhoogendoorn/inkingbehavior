using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Data;
using Windows.Web.Http;

namespace Dev.MartijnHoogendoorn.Inking.Behavior.Converters
{
    public class UriToRandomAccessStreamReferenceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null && !string.IsNullOrWhiteSpace(value as string))
            {
                var uri = new Uri(WebUtility.UrlDecode((string)value));

                var result = RandomAccessStreamReference.CreateFromUri(uri);

                return result;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
