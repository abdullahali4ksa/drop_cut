using DropCut.Core.Domain;
using Xunit;

namespace DropCut.App.Tests;

public class AppScaffoldingTests
{
    [Fact]
    public void CoreReference_ShouldBeAccessible()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
    }
}
