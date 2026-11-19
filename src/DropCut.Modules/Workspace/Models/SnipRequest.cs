using DropCut.Core.Domain;

namespace DropCut.Modules.Workspace.Models;

/// <summary>
/// Data model representing a requested screen capture.
/// </summary>
public class SnipRequest
{
    public SnipRequest()
    {
    }

    public SnipRequest(Guid id, CaptureMode mode, int delaySeconds = 0, PixelBounds? bounds = null, double dpiScale = 1.0)
    {
        Id = id;
        Mode = mode;
        DelaySeconds = delaySeconds;
        Bounds = bounds;
        DpiScale = dpiScale;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public CaptureMode Mode { get; set; } = CaptureMode.Rectangular;

    public int DelaySeconds { get; set; } = 0;

    public PixelBounds? Bounds { get; set; }

    public double DpiScale { get; set; } = 1.0;
}
