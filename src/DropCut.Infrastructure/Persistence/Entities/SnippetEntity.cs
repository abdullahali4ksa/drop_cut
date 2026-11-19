using DropCut.Core.Domain;

namespace DropCut.Infrastructure.Persistence.Entities;

/// <summary>
/// Database entity for indexed snippets.
/// </summary>
public class SnippetEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public CaptureMode CaptureMode { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public double DpiScale { get; set; } = 1.0;

    public bool IsPinned { get; set; }

    public bool IsFavorite { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<AnnotationEntity> Annotations { get; set; } = new();
}
