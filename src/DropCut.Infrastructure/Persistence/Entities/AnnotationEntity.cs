using DropCut.Core.Domain;

namespace DropCut.Infrastructure.Persistence.Entities;

/// <summary>
/// Database entity for persistent canvas annotation actions.
/// </summary>
public class AnnotationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SnippetId { get; set; }

    public SnippetEntity? Snippet { get; set; }

    public ToolType Type { get; set; }

    public string StrokeDataJson { get; set; } = string.Empty;

    public int ZIndex { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
