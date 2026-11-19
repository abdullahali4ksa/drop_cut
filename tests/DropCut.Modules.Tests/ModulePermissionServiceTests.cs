using DropCut.Modules.Permissions.Models;
using DropCut.Modules.Permissions.Services;
using Xunit;

namespace DropCut.Modules.Tests;

public class ModulePermissionServiceTests
{
    [Fact]
    public void Admin_ShouldHaveUnrestrictedAccessToAllFeatures()
    {
        var service = new ModulePermissionService();
        service.RestrictFeature("ConfidentialExport", AppRole.Editor);

        Assert.True(service.IsRoleAuthorized(AppRole.Admin, "ConfidentialExport"));
        Assert.True(service.IsRoleAuthorized(AppRole.Admin, "AnyRandomFeature"));
    }

    [Fact]
    public void RestrictFeature_ShouldDenyUnauthorizedRole()
    {
        var service = new ModulePermissionService();
        service.RestrictFeature("SensitiveRedaction", AppRole.Admin);

        Assert.False(service.IsRoleAuthorized(AppRole.Viewer, "SensitiveRedaction"));
        Assert.False(service.IsRoleAuthorized(AppRole.Editor, "SensitiveRedaction"));
        Assert.True(service.IsRoleAuthorized(AppRole.Admin, "SensitiveRedaction"));
    }
}
