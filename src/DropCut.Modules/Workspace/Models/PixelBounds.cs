using DropCut.Core.Domain;

namespace DropCut.Modules.Workspace.Models;

/// <summary>
/// Value object representing 2D pixel coordinates and dimensions.
/// </summary>
public class PixelBounds : ValueObject
{
    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public PixelBounds(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return X;
        yield return Y;
        yield return Width;
        yield return Height;
    }
}
