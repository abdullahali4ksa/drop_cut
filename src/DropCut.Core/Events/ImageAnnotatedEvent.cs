using DropCut.Core.Domain;

namespace DropCut.Core.Events;

/// <summary>
/// Domain event published when user modifies, draws, crops, or adds redactions to an image.
/// </summary>
public class ImageAnnotatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    public Guid SnippetId { get; }

    public ToolType AppliedTool { get; }

    public string AnnotationMetadataJson { get; }

    public ImageAnnotatedEvent(Guid snippetId, ToolType appliedTool, string annotationMetadataJson)
    {
        SnippetId = snippetId;
        AppliedTool = appliedTool;
        AnnotationMetadataJson = annotationMetadataJson;
    }
}
