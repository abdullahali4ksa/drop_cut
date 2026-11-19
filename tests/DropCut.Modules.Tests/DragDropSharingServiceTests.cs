using DropCut.Modules.Workspace.Services;
using Xunit;

namespace DropCut.Modules.Tests;

public class DragDropSharingServiceTests
{
    [Fact]
    public void PrepareDraggablePackage_ShouldReturnValidPackage()
    {
        var service = new DragDropSharingService();
        var snippetId = Guid.NewGuid();
        var bytes = new byte[] { 137, 80, 78, 71 };
        var localPath = "C:\\Pictures\\DropCut\\snip_123.png";

        var result = service.PrepareDraggablePackage(snippetId, bytes, localPath);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(snippetId, result.Value.SnippetId);
        Assert.Equal(localPath, result.Value.LocalFilePath);
        Assert.Equal("snip_123.png", result.Value.SuggestedFileName);
        Assert.Equal(bytes, result.Value.ImageBytes);
    }
}
