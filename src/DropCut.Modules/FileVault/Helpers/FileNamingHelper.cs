using DropCut.Core.Domain;

namespace DropCut.Modules.FileVault.Helpers;

/// <summary>
/// Utility helper for generating consistent, timestamped filenames for snippets and exports.
/// </summary>
public static class FileNamingHelper
{
    public static string GenerateDefaultFileName(ExportFormat format = ExportFormat.Png)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var extension = GetFileExtension(format);
        return $"DropCut_{timestamp}{extension}";
    }

    public static string SanitizeFileName(string rawName, ExportFormat format = ExportFormat.Png)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(rawName.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return GenerateDefaultFileName(format);
        }

        var extension = GetFileExtension(format);
        if (!sanitized.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            sanitized += extension;
        }

        return sanitized;
    }

    public static string GetFileExtension(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Jpeg => ".jpg",
            ExportFormat.Webp => ".webp",
            ExportFormat.Svg => ".svg",
            ExportFormat.Bmp => ".bmp",
            _ => ".png"
        };
    }
}
