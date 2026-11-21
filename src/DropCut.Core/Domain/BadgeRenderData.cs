namespace DropCut.Core.Domain;

/// <summary>
/// Data contract for rasterizing circular numbered step badges into the screenshot bitmap.
/// </summary>
/// <param name="X">Horizontal center coordinate in pixels.</param>
/// <param name="Y">Vertical center coordinate in pixels.</param>
/// <param name="Number">Sequential step number to stamp (1-99).</param>
/// <param name="BackgroundHex">Background hex color code (e.g. #0078D7).</param>
/// <param name="Diameter">Outer diameter of the badge in pixels.</param>
public record BadgeRenderData(double X, double Y, int Number, string BackgroundHex, int Diameter);
