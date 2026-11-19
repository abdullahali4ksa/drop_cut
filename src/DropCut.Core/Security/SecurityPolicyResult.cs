namespace DropCut.Core.Security;

/// <summary>
/// Encapsulates the outcome of a security policy evaluation.
/// </summary>
public class SecurityPolicyResult
{
    public bool IsAllowed { get; }

    public string? DenialReason { get; }

    private SecurityPolicyResult(bool isAllowed, string? denialReason)
    {
        IsAllowed = isAllowed;
        DenialReason = denialReason;
    }

    public static SecurityPolicyResult Allow() => new(true, null);

    public static SecurityPolicyResult Deny(string reason) => new(false, reason);
}
