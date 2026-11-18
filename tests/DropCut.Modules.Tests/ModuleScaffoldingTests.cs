using DropCut.Modules.AuditLog;
using DropCut.Modules.FileVault;
using DropCut.Modules.Permissions;
using DropCut.Modules.Settings;
using DropCut.Modules.Workspace;
using Xunit;

namespace DropCut.Modules.Tests;

public class ModuleScaffoldingTests
{
    [Fact]
    public void AllModules_ShouldExposeValidModuleNames()
    {
        Assert.Equal("Workspace", WorkspaceModule.ModuleName);
        Assert.Equal("FileVault", FileVaultModule.ModuleName);
        Assert.Equal("Permissions", PermissionsModule.ModuleName);
        Assert.Equal("AuditLog", AuditLogModule.ModuleName);
        Assert.Equal("Settings", SettingsModule.ModuleName);
    }
}
