namespace DropCut.Core.Domain;

/// <summary>
/// Represents the outcome of an operation containing a typed return value.
/// </summary>
/// <typeparam name="T">The type of the encapsulated value.</typeparam>
public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string? errorMessage)
        : base(isSuccess, errorMessage)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null);

    public static new Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
}
