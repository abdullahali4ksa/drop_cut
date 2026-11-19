namespace DropCut.Core.Domain;

/// <summary>
/// Defensive guard clauses enforcing domain invariants and preventing null reference crashes.
/// </summary>
public static class Guard
{
    public static T AgainstNull<T>(T? argument, string parameterName) where T : class
    {
        if (argument is null)
        {
            throw new ArgumentNullException(parameterName, $"Required parameter '{parameterName}' cannot be null.");
        }

        return argument;
    }

    public static string AgainstNullOrWhiteSpace(string? argument, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException($"Required parameter '{parameterName}' cannot be null, empty, or whitespace.", parameterName);
        }

        return argument;
    }

    public static int AgainstNegativeOrZero(int argument, string parameterName)
    {
        if (argument <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, argument, $"Parameter '{parameterName}' must be greater than zero.");
        }

        return argument;
    }
}
