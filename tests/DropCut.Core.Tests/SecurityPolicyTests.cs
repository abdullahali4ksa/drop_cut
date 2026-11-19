using DropCut.Core.AI.Security;
using DropCut.Core.Security;
using Xunit;

namespace DropCut.Core.Tests;

public class SecurityPolicyTests
{
    [Fact]
    public void AISecurityPolicy_WhenSafeOperation_ShouldAllow()
    {
        var policy = new AISecurityPolicy();
        var claims = new List<SecurityClaim> { new("Role", "Admin") };

        var result = policy.Evaluate(claims, "Image.EnhanceContrast");

        Assert.True(result.IsAllowed);
        Assert.Null(result.DenialReason);
    }

    [Fact]
    public void AISecurityPolicy_WhenDangerousOperation_ShouldDeny()
    {
        var policy = new AISecurityPolicy();
        var claims = new List<SecurityClaim>();

        var result = policy.Evaluate(claims, "System.DeleteDisk");

        Assert.False(result.IsAllowed);
        Assert.Contains("violates AI safety constraints", result.DenialReason);
    }
}
