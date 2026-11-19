using DropCut.Infrastructure.Win32;
using Xunit;

namespace DropCut.Modules.Tests;

public class ScreenCaptureServiceTests
{
    [Fact]
    public void CropBitmap_WithInvalidOrEmptyData_ReturnsEmptyArray()
    {
        var service = new ScreenCaptureService();

        var resultEmpty = service.CropBitmap(Array.Empty<byte>(), 0, 0, 100, 100);
        var resultNull = service.CropBitmap(null!, 0, 0, 100, 100);

        Assert.Empty(resultEmpty);
        Assert.Empty(resultNull);
    }

    [Fact]
    public async Task CropBitmap_WithSyntheticBmp_ReturnsCroppedSubRegionSafely()
    {
        var service = new ScreenCaptureService();

        // Capture a region or generate fallback BMP
        var sourceBmp = await service.CaptureRegionAsync(0, 0, 200, 200);

        Assert.NotNull(sourceBmp);
        Assert.True(sourceBmp.Length > 54);

        // Crop 50x50 region
        var croppedBmp = service.CropBitmap(sourceBmp, 10, 10, 50, 50);

        Assert.NotNull(croppedBmp);
        Assert.True(croppedBmp.Length > 54);

        // Verify dimensions in header
        var croppedWidth = BitConverter.ToInt32(croppedBmp, 18);
        var croppedHeight = Math.Abs(BitConverter.ToInt32(croppedBmp, 22));

        Assert.Equal(50, croppedWidth);
        Assert.Equal(50, croppedHeight);
    }

    [Fact]
    public async Task CropBitmap_WithOutOfBoundsCoordinates_ClampsSafelyWithoutCrashing()
    {
        var service = new ScreenCaptureService();
        var sourceBmp = await service.CaptureRegionAsync(0, 0, 100, 100);

        // Intentionally crop beyond the boundaries
        var cropped = service.CropBitmap(sourceBmp, 90, 90, 50, 50);

        Assert.NotNull(cropped);
        Assert.True(cropped.Length > 54);

        var croppedWidth = BitConverter.ToInt32(cropped, 18);
        var croppedHeight = Math.Abs(BitConverter.ToInt32(cropped, 22));

        Assert.True(croppedWidth <= 50);
        Assert.True(croppedHeight <= 50);
    }
}
