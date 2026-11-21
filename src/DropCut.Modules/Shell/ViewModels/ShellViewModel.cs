using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using DropCut.Core.Events;
using DropCut.Modules.Settings.Contracts;
using DropCut.Modules.Shell.Models;
using DropCut.Modules.Workspace.Contracts;
using DropCut.Modules.Workspace.Models;
using DropCut.Modules.Workspace.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DropCut.Modules.Shell.ViewModels;

/// <summary>
/// Presentation ViewModel governing the multi-tab layout, screen capture trigger,
/// Windows Snipping Tool feature toolbar, and dynamic RTL/LTR flow direction of the Shell window.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILocalizationService _localizationService;
    private readonly SynchronizationContext? _syncContext;
    private int _tabCounter = 0;

    private ShellTabModel? _selectedTab;
    private bool _isRtl;
    private string _appTitle = "DropCut";
    private CaptureMode _selectedMode = CaptureMode.Fullscreen;
    private bool _isVideoMode;
    private int _delaySeconds;
    private ToolType _activeTool = ToolType.Hand;
    private string _penColor = "#FF0000";
    private bool _isCapturing;

    public ObservableCollection<ShellTabModel> Tabs { get; } = new();

    public ShellTabModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value) && value != null)
            {
                value.IsDrawingMode = (_activeTool != ToolType.Hand && _activeTool != ToolType.Select);
            }
        }
    }

    public bool IsRtl
    {
        get => _isRtl;
        set => SetProperty(ref _isRtl, value);
    }

    public string AppTitle
    {
        get => _appTitle;
        set => SetProperty(ref _appTitle, value);
    }

    public CaptureMode SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }

    public bool IsVideoMode
    {
        get => _isVideoMode;
        set => SetProperty(ref _isVideoMode, value);
    }

    public int DelaySeconds
    {
        get => _delaySeconds;
        set => SetProperty(ref _delaySeconds, value);
    }

    public ToolType ActiveTool
    {
        get => _activeTool;
        set
        {
            if (SetProperty(ref _activeTool, value) && SelectedTab != null)
            {
                SelectedTab.IsDrawingMode = (value != ToolType.Hand && value != ToolType.Select);
            }
        }
    }

    public string PenColor
    {
        get => _penColor;
        set => SetProperty(ref _penColor, value);
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        set => SetProperty(ref _isCapturing, value);
    }

    public ShellViewModel(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        ILocalizationService localizationService)
    {
        _serviceProvider = Guard.AgainstNull(serviceProvider, nameof(serviceProvider));
        _eventBus = Guard.AgainstNull(eventBus, nameof(eventBus));
        _localizationService = Guard.AgainstNull(localizationService, nameof(localizationService));

        _isRtl = _localizationService.IsRtl;
        _syncContext = SynchronizationContext.Current;

        _eventBus.Subscribe<LanguageChangedEvent>((e, ct) =>
        {
            IsRtl = e.IsRtl;
            UpdateTabTitles();
            return Task.CompletedTask;
        });

        // Initialize with default studio tab
        AddNewTab();
    }

    private void RunOnUi(Action action)
    {
        if (_syncContext != null && SynchronizationContext.Current != _syncContext)
        {
            _syncContext.Send(_ =>
            {
                try { action(); } catch { }
            }, null);
        }
        else
        {
            action();
        }
    }

    [RelayCommand]
    public void AddNewTab()
    {
        _tabCounter++;
        var workspaceVm = _serviceProvider.GetService<WorkspaceViewModel>();

        var tab = new ShellTabModel
        {
            Title = _localizationService.GetString("Tab_NewSnippet", _tabCounter),
            Workspace = workspaceVm,
            AutoSaveStatus = _localizationService.GetString("Status_Ready"),
            SharePillText = _localizationService.GetString("Action_DragDropPill"),
            SharePillToolTip = _localizationService.GetString("Action_DragDropPillToolTip"),
            CropApplyText = _localizationService.GetString("Crop_Apply"),
            CropCancelText = _localizationService.GetString("Crop_Cancel"),
            BlurApplyText = _localizationService.GetString("Blur_Apply"),
            BlurCancelText = _localizationService.GetString("Blur_Cancel"),
            LandingSnipHint = _localizationService.GetString("Landing_SnipHint"),
            LandingDropHint = _localizationService.GetString("Landing_DropHint"),
            LandingHotkeyHint = _localizationService.GetString("Landing_HotkeyHint")
        };

        RunOnUi(() =>
        {
            Tabs.Add(tab);
            SelectedTab = tab;
            ActiveTool = ToolType.Hand;
        });
    }

    [RelayCommand]
    public async Task CaptureScreenAsync()
    {
        if (IsCapturing)
        {
            return;
        }

        IsCapturing = true;
        try
        {
            var snappingEngine = _serviceProvider.GetService<ISnappingEngine>();
            byte[]? imageBytes = null;
            var width = 1920;
            var height = 1080;

            if (snappingEngine is not null)
            {
                var request = new SnipRequest(Guid.NewGuid(), SelectedMode, DelaySeconds, null);
                var captureResult = await snappingEngine.CaptureDesktopAsync(request);
                if (captureResult.IsSuccess && captureResult.Value is not null)
                {
                    imageBytes = captureResult.Value;
                }
            }

            _tabCounter++;
            var workspaceVm = _serviceProvider.GetService<WorkspaceViewModel>();

            var newTab = new ShellTabModel
            {
                Title = _localizationService.GetString("Tab_NewSnippet", _tabCounter),
                Workspace = workspaceVm,
                ImageBytes = imageBytes,
                ImageWidth = width,
                ImageHeight = height,
                AutoSaveStatus = _localizationService.GetString("Status_Saved")
            };

            RunOnUi(() =>
            {
                Tabs.Add(newTab);
                SelectedTab = newTab;
            });
        }
        finally
        {
            IsCapturing = false;
        }
    }

    [RelayCommand]
    public void SetCaptureMode(string modeName)
    {
        if (Enum.TryParse<CaptureMode>(modeName, true, out var mode))
        {
            SelectedMode = mode;
        }
    }

    [RelayCommand]
    public void SetActiveTool(string toolName)
    {
        if (Enum.TryParse<ToolType>(toolName, true, out var tool))
        {
            ActiveTool = tool;
        }
    }

    [RelayCommand]
    public void ToggleVideoMode()
    {
        IsVideoMode = !IsVideoMode;
    }

    [RelayCommand]
    public void SetDelay(int seconds)
    {
        DelaySeconds = seconds;
    }

    [RelayCommand]
    public void CloseTab(ShellTabModel? tab)
    {
        if (tab is null || tab.IsPinned)
        {
            return;
        }

        RunOnUi(() =>
        {
            var index = Tabs.IndexOf(tab);
            if (index >= 0)
            {
                Tabs.RemoveAt(index);

                if (SelectedTab == tab)
                {
                    SelectedTab = Tabs.Count > 0
                        ? Tabs[Math.Clamp(index - 1, 0, Tabs.Count - 1)]
                        : null;
                }
            }
        });
    }

    [RelayCommand]
    public void CloseOtherTabs(ShellTabModel? currentTab)
    {
        if (currentTab is null)
        {
            return;
        }

        RunOnUi(() =>
        {
            var toRemove = Tabs.Where(t => t != currentTab && !t.IsPinned).ToList();
            foreach (var t in toRemove)
            {
                Tabs.Remove(t);
            }

            SelectedTab = currentTab;
        });
    }

    [RelayCommand]
    public void CloseTabsToTheRight(ShellTabModel? currentTab)
    {
        if (currentTab is null)
        {
            return;
        }

        RunOnUi(() =>
        {
            var currentIndex = Tabs.IndexOf(currentTab);
            if (currentIndex < 0)
            {
                return;
            }

            var toRemove = Tabs.Skip(currentIndex + 1).Where(t => !t.IsPinned).ToList();
            foreach (var t in toRemove)
            {
                Tabs.Remove(t);
            }
        });
    }

    [RelayCommand]
    public void CloseAllTabs()
    {
        RunOnUi(() =>
        {
            var toRemove = Tabs.Where(t => !t.IsPinned).ToList();
            foreach (var t in toRemove)
            {
                Tabs.Remove(t);
            }

            SelectedTab = Tabs.FirstOrDefault();
        });
    }

    [RelayCommand]
    public void TogglePin(ShellTabModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        tab.IsPinned = !tab.IsPinned;
    }

    [RelayCommand]
    public void ToggleFavorite(ShellTabModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        tab.IsFavorite = !tab.IsFavorite;
    }

    private void UpdateTabTitles()
    {
        var pillText = _localizationService.GetString("Action_DragDropPill");
        var pillToolTip = _localizationService.GetString("Action_DragDropPillToolTip");
        var cropApply = _localizationService.GetString("Crop_Apply");
        var cropCancel = _localizationService.GetString("Crop_Cancel");
        var blurApply = _localizationService.GetString("Blur_Apply");
        var blurCancel = _localizationService.GetString("Blur_Cancel");
        var snipHint = _localizationService.GetString("Landing_SnipHint");
        var dropHint = _localizationService.GetString("Landing_DropHint");
        var hotkeyHint = _localizationService.GetString("Landing_HotkeyHint");

        for (int i = 0; i < Tabs.Count; i++)
        {
            if (Tabs[i].Title.Contains((i + 1).ToString()) || Tabs[i].Title.StartsWith("Snip") || Tabs[i].Title.StartsWith("Captura"))
            {
                Tabs[i].Title = _localizationService.GetString("Tab_NewSnippet", i + 1);
            }
            Tabs[i].SharePillText = pillText;
            Tabs[i].SharePillToolTip = pillToolTip;
            Tabs[i].CropApplyText = cropApply;
            Tabs[i].CropCancelText = cropCancel;
            Tabs[i].BlurApplyText = blurApply;
            Tabs[i].BlurCancelText = blurCancel;
            Tabs[i].LandingSnipHint = snipHint;
            Tabs[i].LandingDropHint = dropHint;
            Tabs[i].LandingHotkeyHint = hotkeyHint;
        }
    }
}
