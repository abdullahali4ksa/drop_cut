namespace DropCut.Core.Security;

/// <summary>
/// Represents a security capability or attribute granted to a user session.
/// </summary>
public record SecurityClaim(string Type, string Value);
