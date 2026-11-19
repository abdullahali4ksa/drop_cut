using DropCut.Core.Domain;

namespace DropCut.Modules.Settings.Models;

/// <summary>
/// Represents a supported language in DropCut.
/// </summary>
public class LanguageOption : ValueObject
{
    public string Code { get; }
    public string DisplayName { get; }
    public string NativeName { get; }
    public bool IsRtl { get; }

    public LanguageOption(string code, string displayName, string nativeName, bool isRtl)
    {
        Code = Guard.AgainstNullOrWhiteSpace(code, nameof(code));
        DisplayName = Guard.AgainstNullOrWhiteSpace(displayName, nameof(displayName));
        NativeName = Guard.AgainstNullOrWhiteSpace(nativeName, nameof(nativeName));
        IsRtl = isRtl;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Code;
    }
}
