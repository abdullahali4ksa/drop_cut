using DropCut.Modules.Shell.Models;
using DropCut.Modules.Shell.ViewModels;
using DropCut.Core.Contracts;
using DropCut.Core.Events;
using DropCut.Modules.Settings.Contracts;
using DropCut.Modules.Settings.Services;
using DropCut.Modules.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DropCut.App.Tests;

public class ShellViewModelTests
{
    private static (ShellViewModel ViewModel, InMemoryEventBus EventBus, ILocalizationService LocService) CreateViewModel()
    {
        var services = new ServiceCollection();
        var bus = new InMemoryEventBus();
        var locService = new LocalizationService(bus);

        services.AddSingleton<IEventBus>(bus);
        services.AddSingleton<ILocalizationService>(locService);
        services.AddWorkspaceModule();

        var provider = services.BuildServiceProvider();
        var vm = new ShellViewModel(provider, bus, locService);

        return (vm, bus, locService);
    }

    [Fact]
    public void InitialState_CreatesSingleTab_WithLocalizedTitle()
    {
        var (vm, _, _) = CreateViewModel();

        Assert.Single(vm.Tabs);
        Assert.NotNull(vm.SelectedTab);
        Assert.Equal("Snip 1", vm.SelectedTab!.Title);
        Assert.False(vm.IsRtl);
    }

    [Fact]
    public void AddNewTab_AddsNewTabAndSelectsIt()
    {
        var (vm, _, _) = CreateViewModel();

        vm.AddNewTabCommand.Execute(null);

        Assert.Equal(2, vm.Tabs.Count);
        Assert.Equal(vm.Tabs[1], vm.SelectedTab);
        Assert.Equal("Snip 2", vm.SelectedTab!.Title);
    }

    [Fact]
    public void CloseTab_RemovesUnpinnedTab()
    {
        var (vm, _, _) = CreateViewModel();
        vm.AddNewTabCommand.Execute(null);

        var firstTab = vm.Tabs[0];
        vm.CloseTabCommand.Execute(firstTab);

        Assert.Single(vm.Tabs);
        Assert.DoesNotContain(firstTab, vm.Tabs);
    }

    [Fact]
    public void PinnedTab_CannotBeClosed()
    {
        var (vm, _, _) = CreateViewModel();
        var tab = vm.Tabs[0];

        vm.TogglePinCommand.Execute(tab);
        Assert.True(tab.IsPinned);
        Assert.False(tab.IsClosable);

        vm.CloseTabCommand.Execute(tab);
        Assert.Single(vm.Tabs);
        Assert.Contains(tab, vm.Tabs);
    }

    [Fact]
    public void ToggleFavorite_TogglesFavoriteState()
    {
        var (vm, _, _) = CreateViewModel();
        var tab = vm.Tabs[0];

        Assert.False(tab.IsFavorite);
        vm.ToggleFavoriteCommand.Execute(tab);
        Assert.True(tab.IsFavorite);

        vm.ToggleFavoriteCommand.Execute(tab);
        Assert.False(tab.IsFavorite);
    }

    [Fact]
    public void CloseOtherTabs_PreservesPinnedAndCurrentTab()
    {
        var (vm, _, _) = CreateViewModel();
        // Tab 1 (will be pinned)
        var tab1 = vm.Tabs[0];
        vm.TogglePinCommand.Execute(tab1);

        // Tab 2 (target tab)
        vm.AddNewTabCommand.Execute(null);
        var tab2 = vm.SelectedTab!;

        // Tab 3
        vm.AddNewTabCommand.Execute(null);
        // Tab 4
        vm.AddNewTabCommand.Execute(null);

        Assert.Equal(4, vm.Tabs.Count);

        vm.CloseOtherTabsCommand.Execute(tab2);

        Assert.Equal(2, vm.Tabs.Count);
        Assert.Contains(tab1, vm.Tabs); // Pinned tab preserved
        Assert.Contains(tab2, vm.Tabs); // Target tab preserved
        Assert.Equal(tab2, vm.SelectedTab);
    }

    [Fact]
    public void CloseTabsToTheRight_ClosesOnlyRightUnpinnedTabs()
    {
        var (vm, _, _) = CreateViewModel();
        var tab1 = vm.Tabs[0];

        vm.AddNewTabCommand.Execute(null);
        var tab2 = vm.SelectedTab!;

        vm.AddNewTabCommand.Execute(null);
        var tab3 = vm.SelectedTab!;

        vm.AddNewTabCommand.Execute(null);
        var tab4 = vm.SelectedTab!;
        vm.TogglePinCommand.Execute(tab4); // Tab 4 is pinned

        Assert.Equal(4, vm.Tabs.Count);

        // Close right of Tab 2: Tab 3 is unpinned so closed, Tab 4 is pinned so kept
        vm.CloseTabsToTheRightCommand.Execute(tab2);

        Assert.Equal(3, vm.Tabs.Count);
        Assert.Contains(tab1, vm.Tabs);
        Assert.Contains(tab2, vm.Tabs);
        Assert.DoesNotContain(tab3, vm.Tabs);
        Assert.Contains(tab4, vm.Tabs);
    }

    [Fact]
    public void CloseAllTabs_PreservesOnlyPinnedTabs()
    {
        var (vm, _, _) = CreateViewModel();
        var tab1 = vm.Tabs[0];
        vm.TogglePinCommand.Execute(tab1);

        vm.AddNewTabCommand.Execute(null);
        vm.AddNewTabCommand.Execute(null);

        Assert.Equal(3, vm.Tabs.Count);

        vm.CloseAllTabsCommand.Execute(null);

        Assert.Single(vm.Tabs);
        Assert.Equal(tab1, vm.Tabs[0]);
    }

    [Fact]
    public async Task LanguageChangedEvent_UpdatesDynamicRtlAndTabTitles()
    {
        var (vm, _, locService) = CreateViewModel();

        Assert.False(vm.IsRtl);
        Assert.Equal("Snip 1", vm.Tabs[0].Title);

        await locService.SetLanguageAsync("ar");

        Assert.True(vm.IsRtl);
        Assert.Equal("لقطة 1", vm.Tabs[0].Title);
        Assert.Equal("تطبيق القص", vm.Tabs[0].CropApplyText);
        Assert.Equal("إلغاء", vm.Tabs[0].CropCancelText);
        Assert.Equal("تطبيق التمويه", vm.Tabs[0].BlurApplyText);
        Assert.Equal("انقر فوق «جديد» في الأعلى أو اضغط PrintScreen لأخذ لقطة شاشة", vm.Tabs[0].LandingSnipHint);
    }
}
