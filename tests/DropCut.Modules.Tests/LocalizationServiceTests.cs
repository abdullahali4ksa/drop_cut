using DropCut.Core.Contracts;
using DropCut.Core.Events;
using DropCut.Modules.Settings.Models;
using DropCut.Modules.Settings.Services;
using DropCut.Modules.Settings.ViewModels;
using Xunit;

namespace DropCut.Modules.Tests;

public class LocalizationServiceTests
{
    private class MockUserPreferenceRepository : IUserPreferenceRepository
    {
        private readonly Dictionary<string, string> _storage = new();

        public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
        {
            _storage.TryGetValue(key, out var val);
            return Task.FromResult(val);
        }

        public Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _storage[key] = value;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void LocalizationService_DefaultLanguage_IsArabicAndRtl()
    {
        var bus = new InMemoryEventBus();
        var service = new LocalizationService(bus);

        Assert.Equal("ar", service.CurrentLanguageCode);
        Assert.True(service.IsRtl);
        Assert.Equal(10, service.SupportedLanguages.Count);
        Assert.Equal("قلم الرسم", service.GetString("Tool_Pen"));
    }

    [Theory]
    [InlineData("ar", true, "قلم الرسم")]
    [InlineData("en", false, "Pen")]
    [InlineData("es", false, "Bolígrafo")]
    [InlineData("hi", false, "पेन")]
    [InlineData("zh", false, "画笔")]
    [InlineData("fr", false, "Stylo")]
    [InlineData("de", false, "Stift")]
    [InlineData("ru", false, "Перо")]
    [InlineData("ja", false, "ペン")]
    [InlineData("pt", false, "Caneta")]
    public async Task LocalizationService_SwitchLanguage_ReturnsPureLanguageStrings(string langCode, bool expectedRtl, string expectedPen)
    {
        var bus = new InMemoryEventBus();
        LanguageChangedEvent? receivedEvent = null;
        bus.Subscribe<LanguageChangedEvent>((e, ct) =>
        {
            receivedEvent = e;
            return Task.CompletedTask;
        });

        var service = new LocalizationService(bus);
        await service.SetLanguageAsync(langCode);

        Assert.Equal(langCode, service.CurrentLanguageCode);
        Assert.Equal(expectedRtl, service.IsRtl);
        Assert.Equal(expectedPen, service.GetString("Tool_Pen"));
        Assert.NotNull(receivedEvent);
        Assert.Equal(langCode, receivedEvent!.NewLanguageCode);
        Assert.Equal(expectedRtl, receivedEvent.IsRtl);
    }

    [Fact]
    public async Task LocalizationService_FormattedString_FormatsCorrectly()
    {
        var bus = new InMemoryEventBus();
        var service = new LocalizationService(bus);

        var arFormatted = service.GetString("Tab_NewSnippet", 5);
        Assert.Equal("لقطة 5", arFormatted);

        await service.SetLanguageAsync("en");
        var enFormatted = service.GetString("Tab_NewSnippet", 5);
        Assert.Equal("Snip 5", enFormatted);
    }

    [Fact]
    public async Task SettingsService_LoadAndSave_PersistsAndSyncsLanguage()
    {
        var bus = new InMemoryEventBus();
        var repo = new MockUserPreferenceRepository();
        var locService = new LocalizationService(bus, repo);
        var settingsService = new SettingsService(repo, locService);

        var initial = await settingsService.LoadSettingsAsync();
        Assert.Equal("ar", initial.LanguageCode);
        Assert.Equal(300, initial.DebounceMilliseconds);

        var updated = new AppSettingsModel
        {
            LanguageCode = "fr",
            Theme = "Dark",
            AutoSaveEnabled = true,
            DebounceMilliseconds = 500,
            VaultDirectory = "C:\\CustomVault",
            DefaultExportFormat = "Jpeg",
            SnippingHotkey = "Ctrl+Shift+X"
        };

        await settingsService.SaveSettingsAsync(updated);

        var loaded = await settingsService.LoadSettingsAsync();
        Assert.Equal("fr", loaded.LanguageCode);
        Assert.Equal("Dark", loaded.Theme);
        Assert.Equal(500, loaded.DebounceMilliseconds);
        Assert.Equal("C:\\CustomVault", loaded.VaultDirectory);
        Assert.Equal("fr", locService.CurrentLanguageCode);
    }

    [Fact]
    public async Task SettingsViewModel_InitializeAndSave_WorksCorrectly()
    {
        var bus = new InMemoryEventBus();
        var repo = new MockUserPreferenceRepository();
        var locService = new LocalizationService(bus, repo);
        var settingsService = new SettingsService(repo, locService);
        var vm = new SettingsViewModel(settingsService, locService);

        await vm.InitializeAsync();
        Assert.Equal("ar", vm.SelectedLanguage?.Code);

        var enLang = vm.SupportedLanguages.First(l => l.Code == "en");
        vm.SelectedLanguage = enLang;
        vm.SelectedTheme = "Dark";
        vm.DebounceMilliseconds = 450;

        await vm.SaveAsync();

        var loaded = await settingsService.LoadSettingsAsync();
        Assert.Equal("en", loaded.LanguageCode);
        Assert.Equal("Dark", loaded.Theme);
        Assert.Equal(450, loaded.DebounceMilliseconds);
    }
}
