using DropCut.Core.Domain;

namespace DropCut.Core.Contracts;

/// <summary>
/// Optical Character Recognition service for extracting readable text from captured image bytes.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Analyzes the input image bytes and extracts any recognized text content.
    /// </summary>
    Task<Result<string>> ExtractTextAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
