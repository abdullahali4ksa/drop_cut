using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using DropCut.Core.Contracts;
using DropCut.Core.Domain;

namespace DropCut.App.Services;

/// <summary>
/// Platform OCR service using built-in Windows 10/11 Windows.Media.Ocr engine
/// to extract text from captured screenshots without external packages.
/// </summary>
public class WindowsOcrService : IOcrService
{
    public async Task<Result<string>> ExtractTextAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return Result<string>.Failure("Image data is empty.");
        }

        try
        {
            var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (ocrEngine == null)
            {
                return Result<string>.Failure("Windows OCR is not available for current language profiles.");
            }

            var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(imageBytes);
                await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
                writer.DetachStream();
            }

            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask(cancellationToken).ConfigureAwait(false);

            var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken).ConfigureAwait(false);
            var text = ocrResult?.Text ?? string.Empty;

            return Result<string>.Success(text);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"OCR extraction failed: {ex.Message}");
        }
    }
}
