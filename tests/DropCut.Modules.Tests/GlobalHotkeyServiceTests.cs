using DropCut.Infrastructure.Win32;
using Xunit;

namespace DropCut.Modules.Tests;

public class GlobalHotkeyServiceTests
{
    [Fact]
    public void GlobalHotkeyService_StartAndStop_OperatesSafelyWithoutExceptions()
    {
        using var service = new GlobalHotkeyService();

        // Should not throw even in test runner environment
        service.StartListening();
        service.StopListening();
    }

    [Fact]
    public void GlobalHotkeyService_MultipleDisposeCalls_HandlesSafely()
    {
        var service = new GlobalHotkeyService();
        service.StartListening();

        service.Dispose();
        service.Dispose(); // idempotent
    }
}
