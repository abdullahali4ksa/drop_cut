using CommunityToolkit.Mvvm.ComponentModel;
using DropCut.Modules.Workspace.ViewModels;

namespace DropCut.Modules.Shell.Models;

/// <summary>
/// Model representing an individual tab item within the multi-tab shell layout.
/// </summary>
public class ShellTabModel : ObservableObject
{
    private string _title = "Snip 1";
    private bool _isPinned;
    private bool _isFavorite;
    private bool _isClosable = true;
    private string _autoSaveStatus = string.Empty;
    private byte[]? _imageBytes;
    private string? _imagePath;
    private int _imageWidth = 1920;
    private int _imageHeight = 1080;

    public Guid Id { get; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetProperty(ref _isPinned, value))
            {
                IsClosable = !value;
            }
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public bool IsClosable
    {
        get => _isClosable;
        set => SetProperty(ref _isClosable, value);
    }

    public string AutoSaveStatus
    {
        get => _autoSaveStatus;
        set => SetProperty(ref _autoSaveStatus, value);
    }

    public byte[]? ImageBytes
    {
        get => _imageBytes;
        set
        {
            if (SetProperty(ref _imageBytes, value))
            {
                OnPropertyChanged(nameof(HasImage));
            }
        }
    }

    public bool HasImage => _imageBytes is not null && _imageBytes.Length > 0;

    public string? ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public int ImageWidth
    {
        get => _imageWidth;
        set => SetProperty(ref _imageWidth, value);
    }

    public int ImageHeight
    {
        get => _imageHeight;
        set => SetProperty(ref _imageHeight, value);
    }

    public Guid? SnippetId { get; set; }

    public WorkspaceViewModel? Workspace { get; set; }
}
