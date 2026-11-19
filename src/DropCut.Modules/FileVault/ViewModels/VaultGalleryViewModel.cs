using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DropCut.Modules.FileVault.Models;

namespace DropCut.Modules.FileVault.ViewModels;

/// <summary>
/// Presentation ViewModel for the FileVault gallery and recent captures explorer.
/// </summary>
public partial class VaultGalleryViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private VaultFile? _selectedFile;

    public ObservableCollection<VaultFile> VaultFiles { get; } = new();

    public VaultGalleryViewModel()
    {
    }

    [RelayCommand]
    public void LoadFiles(IEnumerable<VaultFile> files)
    {
        VaultFiles.Clear();
        foreach (var file in files)
        {
            VaultFiles.Add(file);
        }
    }

    [RelayCommand]
    public void RemoveFile(VaultFile file)
    {
        if (file is not null && VaultFiles.Contains(file))
        {
            VaultFiles.Remove(file);
        }
    }
}
