using DropCut.Infrastructure.FileSystem;
using Xunit;

namespace DropCut.Modules.Tests;

public class SafeFileStorageServiceTests
{
    [Fact]
    public async Task WriteAtomicAsync_ShouldCreateFileAndReadSafely()
    {
        var service = new SafeFileStorageService();
        var tempFile = Path.Combine(Path.GetTempPath(), $"dropcut_test_{Guid.NewGuid()}.dat");

        try
        {
            var testData = new byte[] { 1, 2, 3, 4, 5, 42 };
            await service.WriteAtomicAsync(tempFile, testData);

            Assert.True(File.Exists(tempFile));

            var readBytes = await service.ReadSafeAsync(tempFile);
            Assert.Equal(testData, readBytes);
        }
        finally
        {
            service.DeleteSafe(tempFile);
        }
    }
}
