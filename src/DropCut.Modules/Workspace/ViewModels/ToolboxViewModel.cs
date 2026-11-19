using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DropCut.Core.Domain;

namespace DropCut.Modules.Workspace.ViewModels;

/// <summary>
/// Presentation ViewModel for the annotation tools, palette, and redaction settings.
/// </summary>
public partial class ToolboxViewModel : ObservableObject
{
    [ObservableProperty]
    private ToolType _activeTool = ToolType.Pen;

    [ObservableProperty]
    private string _activeColorHex = "#E81123";

    [ObservableProperty]
    private double _strokeThickness = 3.0;

    [ObservableProperty]
    private double _blurRadius = 12.0;

    [RelayCommand]
    public void SelectTool(ToolType tool)
    {
        ActiveTool = tool;
    }

    [RelayCommand]
    public void SetColor(string colorHex)
    {
        ActiveColorHex = colorHex;
    }
}
