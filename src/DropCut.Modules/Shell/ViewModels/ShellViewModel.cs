using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using DropCut.Core.Events;
using DropCut.Modules.Settings.Contracts;
using DropCut.Modules.Shell.Models;
using DropCut.Modules.Workspace.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DropCut.Modules.Shell.ViewModels;

/// <summary>
/// Presentation ViewModel governing the multi-tab layout, tab lifecycle, context menu actions,
/// and dynamic RTL/LTR flow direction of the Shell window.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILocalizationService _localizationService;
    private int _tabCounter = 0;

    private ShellTabModel? _selectedTab;
    private bool _isRtl;
    private string _appTitle = "DropCut";

    public ObservableCollection<ShellTabModel> Tabs { get; } = new();

    public ShellTabModel? SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
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

    public ShellViewModel(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        ILocalizationService localizationService)
    {
        _serviceProvider = Guard.AgainstNull(serviceProvider, nameof(serviceProvider));
        _eventBus = Guard.AgainstNull(eventBus, nameof(eventBus));
        _localizationService = Guard.AgainstNull(localizationService, nameof(localizationService));

        _isRtl = _localizationService.IsRtl;

        _eventBus.Subscribe<LanguageChangedEvent>((e, ct) =>
        {
            IsRtl = e.IsRtl;
            UpdateTabTitles();
            return Task.CompletedTask;
        });

        // Initialize with default studio tab
        AddNewTab();
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
            AutoSaveStatus = _localizationService.GetString("Status_Ready")
        };

        Tabs.Add(tab);
        SelectedTab = tab;
    }

    [RelayCommand]
    public void CloseTab(ShellTabModel? tab)
    {
        if (tab is null || tab.IsPinned)
        {
            return;
        }

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
    }

    [RelayCommand]
    public void CloseOtherTabs(ShellTabModel? currentTab)
    {
        if (currentTab is null)
        {
            return;
        }

        var toRemove = Tabs.Where(t => t != currentTab && !t.IsPinned).ToList();
        foreach (var t in toRemove)
        {
            Tabs.Remove(t);
        }

        SelectedTab = currentTab;
    }

    [RelayCommand]
    public void CloseTabsToTheRight(ShellTabModel? currentTab)
    {
        if (currentTab is null)
        {
            return;
        }

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
    }

    [RelayCommand]
    public void CloseAllTabs()
    {
        var toRemove = Tabs.Where(t => !t.IsPinned).ToList();
        foreach (var t in toRemove)
        {
            Tabs.Remove(t);
        }

        SelectedTab = Tabs.FirstOrDefault();
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
        for (int i = 0; i < Tabs.Count; i++)
        {
            if (Tabs[i].Title.StartsWith("Snip") || Tabs[i].Title.StartsWith("لقطة") || Tabs[i].Title.StartsWith("Captura"))
            {
                Tabs[i].Title = _localizationService.GetString("Tab_NewSnippet", i + 1);
            }
        }
    }
}
