namespace DropCut.Core.Security;

/// <summary>
/// Contract for evaluating dynamic security policies against user claims and operations.
/// </summary>
public interface ISecurityPolicy
{
    string PolicyName { get; }

    SecurityPolicyResult Evaluate(IEnumerable<SecurityClaim> claims, string operationName);
}
