namespace DropCut.Core.Domain;

/// <summary>
/// Value object representing raw vector stroke coordinates and color for bitmap rasterization.
/// </summary>
public record StrokeRenderData(
    IReadOnlyList<(double X, double Y)> Points,
    string ColorHex,
    int Thickness,
    bool IsHighlighter = false);
