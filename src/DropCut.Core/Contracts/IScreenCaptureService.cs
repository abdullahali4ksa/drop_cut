namespace DropCut.Core.Contracts;

/// <summary>
/// Contract for capturing physical desktop screen contents, window bounds, and display regions.
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Captures a specific rectangular region of the desktop screen in BMP/PNG format.
    /// </summary>
    Task<byte[]> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures the entire primary screen desktop in BMP/PNG format.
    /// </summary>
    Task<byte[]> CapturePrimaryScreenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures the currently active/foreground application window.
    /// </summary>
    Task<byte[]> CaptureActiveWindowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crops a rectangular sub-region from an existing 32-bit BMP byte stream in memory.
    /// </summary>
    byte[] CropBitmap(byte[] sourceBmpBytes, int x, int y, int width, int height);
}
