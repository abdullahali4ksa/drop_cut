using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DropCut.App.Shared.Converters;

/// <summary>
/// Converts raw image bytes (BMP/PNG) into a WinUI 3 BitmapImage for in-canvas presentation.
/// </summary>
public class BytesToBitmapImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is byte[] bytes && bytes.Length > 0)
        {
            try
            {
                var stream = new InMemoryRandomAccessStream();
                using (var netStream = stream.AsStreamForWrite())
                {
                    netStream.Write(bytes, 0, bytes.Length);
                    netStream.Flush();
                }

                stream.Seek(0);
                var bitmapImage = new BitmapImage();
                bitmapImage.SetSource(stream);
                return bitmapImage;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
