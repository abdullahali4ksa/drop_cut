using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DropCut.Core.Domain;
using DropCut.Modules.Workspace.Contracts;
using DropCut.Modules.Workspace.Models;

namespace DropCut.Modules.Workspace.ViewModels;

/// <summary>
/// Presentation ViewModel orchestrating the canvas viewport, zoom, tool dispatch, and drag-and-drop triggers.
/// </summary>
public partial class WorkspaceViewModel : ObservableObject
{
    private readonly ISnappingEngine _snappingEngine;
    private readonly IAnnotationCanvasService _canvasService;
    private readonly IDragDropSharingService _dragDropService;

    [ObservableProperty]
    private Guid _activeSnippetId = Guid.NewGuid();

    [ObservableProperty]
    private double _zoomFactor = 1.0;

    [ObservableProperty]
    private bool _isSnippingActive;

    [ObservableProperty]
    private bool _isHandCursorActive;

    public ToolboxViewModel Toolbox { get; } = new();

    public WorkspaceViewModel(
        ISnappingEngine snappingEngine,
        IAnnotationCanvasService canvasService,
        IDragDropSharingService dragDropService)
    {
        _snappingEngine = Guard.AgainstNull(snappingEngine, nameof(snappingEngine));
        _canvasService = Guard.AgainstNull(canvasService, nameof(canvasService));
        _dragDropService = Guard.AgainstNull(dragDropService, nameof(dragDropService));
    }

    [RelayCommand]
    public async Task TriggerSnipAsync(CaptureMode mode)
    {
        IsSnippingActive = true;
        try
        {
            var request = new SnipRequest { Id = ActiveSnippetId, Mode = mode };
            var result = await _snappingEngine.CaptureDesktopAsync(request);
            if (result.IsSuccess && result.Value is not null)
            {
                _canvasService.SetBaseImage(ActiveSnippetId, result.Value, 1920, 1080);
            }
        }
        finally
        {
            IsSnippingActive = false;
        }
    }

    [RelayCommand]
    public async Task UndoAsync()
    {
        await _canvasService.UndoAsync(ActiveSnippetId);
    }

    [RelayCommand]
    public async Task RedoAsync()
    {
        await _canvasService.RedoAsync(ActiveSnippetId);
    }

    [RelayCommand]
    public void SetHandCursor(bool active)
    {
        IsHandCursorActive = active;
    }
}
