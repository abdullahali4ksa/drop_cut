using DropCut.Core.Domain;
using Xunit;

namespace DropCut.Core.Tests;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_ShouldCreateFailedResultWithErrorMessage()
    {
        var error = "Something went wrong";
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.ErrorMessage);
    }

    [Fact]
    public void TypedResult_Success_ShouldContainValue()
    {
        var value = 42;
        var result = Result<int>.Success(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }
}
