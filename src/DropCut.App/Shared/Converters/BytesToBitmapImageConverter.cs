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
                var bitmapImage = new BitmapImage();
                var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(bytes);
                    writer.StoreAsync().AsTask().GetAwaiter().GetResult();
                    writer.DetachStream();
                }

                stream.Seek(0);
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
