using DropCut.Core.Domain;
using DropCut.Core.Security;

namespace DropCut.Core.AI.Security;

/// <summary>
/// Enforces boundary constraints and safety verification on AI-driven automation commands.
/// </summary>
public class AISecurityPolicy : ISecurityPolicy
{
    public string PolicyName => "AISecurityPolicy";

    public SecurityPolicyResult Evaluate(IEnumerable<SecurityClaim> claims, string operationName)
    {
        Guard.AgainstNullOrWhiteSpace(operationName, nameof(operationName));

        // Prohibit direct unverified disk wiping or raw process execution
        if (operationName.StartsWith("System.Delete", StringComparison.OrdinalIgnoreCase) ||
            operationName.StartsWith("System.ExecuteRaw", StringComparison.OrdinalIgnoreCase))
        {
            return SecurityPolicyResult.Deny($"Operation '{operationName}' violates AI safety constraints.");
        }

        return SecurityPolicyResult.Allow();
    }
}
