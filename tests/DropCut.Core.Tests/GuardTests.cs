using DropCut.Core.Domain;
using Xunit;

namespace DropCut.Core.Tests;

public class GuardTests
{
    [Fact]
    public void AgainstNull_WhenNull_ShouldThrowArgumentNullException()
    {
        object? nullObj = null;
        Assert.Throws<ArgumentNullException>(() => Guard.AgainstNull(nullObj, "nullObj"));
    }

    [Fact]
    public void AgainstNullOrWhiteSpace_WhenEmpty_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Guard.AgainstNullOrWhiteSpace("   ", "name"));
    }

    [Fact]
    public void AgainstNegativeOrZero_WhenPositive_ShouldReturnValue()
    {
        var val = Guard.AgainstNegativeOrZero(100, "val");
        Assert.Equal(100, val);
    }
}
