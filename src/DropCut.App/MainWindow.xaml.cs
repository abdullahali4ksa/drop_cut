using System.Diagnostics;
using System.IO;
using DropCut.Core.Domain;
using DropCut.Modules.Shell.Models;
using DropCut.Modules.Shell.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;

namespace DropCut.App;

/// <summary>
/// Root application shell window hosting the multi-tab layout, Snipping Tool ribbon,
/// tab context menu, keyboard shortcuts, and continuous auto-save indicators.
/// </summary>
public sealed partial class MainWindow : Window
{
    public ShellViewModel ViewModel { get; }

    public MainWindow(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public FlowDirection GetFlowDirection(bool isRtl) =>
        isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    // ==========================================
    // 1. Keyboard Shortcuts (PrintScreen, Ctrl+N, Ctrl+S, Ctrl+C, Ctrl+W)
    // ==========================================
    private void Grid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            var isCtrlDown = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

            if (e.Key == VirtualKey.Snapshot || (int)e.Key == 44 || (isCtrlDown && e.Key == VirtualKey.N))
            {
                e.Handled = true;
                _ = ViewModel.CaptureScreenCommand.ExecuteAsync(null);
            }
            else if (isCtrlDown && e.Key == VirtualKey.S)
            {
                e.Handled = true;
                SaveCurrentTabImage();
            }
            else if (isCtrlDown && e.Key == VirtualKey.C)
            {
                e.Handled = true;
                CopyCurrentTabImage();
            }
            else if (isCtrlDown && e.Key == VirtualKey.W)
            {
                e.Handled = true;
                if (ViewModel.SelectedTab is not null)
                {
                    ViewModel.CloseTabCommand.Execute(ViewModel.SelectedTab);
                }
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    // ==========================================
    // 2. Capture Mode Handlers
    // ==========================================
    private void ModeRectangle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.SelectedMode = CaptureMode.Rectangular;
            _ = ViewModel.CaptureScreenCommand.ExecuteAsync(null);
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void ModeWindow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.SelectedMode = CaptureMode.Window;
            _ = ViewModel.CaptureScreenCommand.ExecuteAsync(null);
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void ModeFullscreen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.SelectedMode = CaptureMode.Fullscreen;
            _ = ViewModel.CaptureScreenCommand.ExecuteAsync(null);
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void ModeFreeform_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.SelectedMode = CaptureMode.Freeform;
            _ = ViewModel.CaptureScreenCommand.ExecuteAsync(null);
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void LandingSnip_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _ = ViewModel.CaptureScreenCommand.ExecuteAsync(null);
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    // ==========================================
    // 3. Delay Countdown Handlers
    // ==========================================
    private void Delay0_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.DelaySeconds = 0; } catch (Exception) { }
    }

    private void Delay3_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.DelaySeconds = 3; } catch (Exception) { }
    }

    private void Delay5_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.DelaySeconds = 5; } catch (Exception) { }
    }

    private void Delay10_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.DelaySeconds = 10; } catch (Exception) { }
    }

    // ==========================================
    // 4. Pen & Tool Ribbon Handlers
    // ==========================================
    private void PenRed_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.PenColor = "#FF0000"; ViewModel.ActiveTool = ToolType.Pen; } catch (Exception) { }
    }

    private void PenBlue_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.PenColor = "#0078D7"; ViewModel.ActiveTool = ToolType.Pen; } catch (Exception) { }
    }

    private void PenBlack_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.PenColor = "#000000"; ViewModel.ActiveTool = ToolType.Pen; } catch (Exception) { }
    }

    private void PenGreen_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.PenColor = "#107C41"; ViewModel.ActiveTool = ToolType.Pen; } catch (Exception) { }
    }

    private void PenYellow_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.PenColor = "#FFD700"; ViewModel.ActiveTool = ToolType.Pen; } catch (Exception) { }
    }

    private void ToolHighlighter_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.ActiveTool = ToolType.Highlighter; } catch (Exception) { }
    }

    private void ToolEraser_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.ActiveTool = ToolType.Select; } catch (Exception) { }
    }

    private void ToolRuler_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.ActiveTool = ToolType.Shape; } catch (Exception) { }
    }

    private void ToolText_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.ActiveTool = ToolType.Text; } catch (Exception) { }
    }

    private void ToolCrop_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.ActiveTool = ToolType.Crop; } catch (Exception) { }
    }

    // ==========================================
    // 5. Actions: Undo, Redo, Save, Copy, Share
    // ==========================================
    private void ActionUndo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.SelectedTab is not null)
            {
                ViewModel.SelectedTab.AutoSaveStatus = "تراجع / Undo";
            }
        }
        catch (Exception) { }
    }

    private void ActionRedo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.SelectedTab is not null)
            {
                ViewModel.SelectedTab.AutoSaveStatus = "إعادة / Redo";
            }
        }
        catch (Exception) { }
    }

    private void ActionSave_Click(object sender, RoutedEventArgs e) => SaveCurrentTabImage();

    private void ActionCopy_Click(object sender, RoutedEventArgs e) => CopyCurrentTabImage();

    private void ActionShare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CopyCurrentTabImage();
            if (ViewModel.SelectedTab is not null)
            {
                ViewModel.SelectedTab.AutoSaveStatus = "تم النسخ — جاهز للمشاركة واللصق المباشر";
            }
        }
        catch (Exception) { }
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.SelectedTab is not null)
            {
                ViewModel.SelectedTab.AutoSaveStatus = "الإعدادات / Settings";
            }
        }
        catch (Exception) { }
    }

    private void MenuOpenVault_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picturesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DropCut");
            Directory.CreateDirectory(picturesPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = picturesPath,
                UseShellExecute = true
            });
        }
        catch (Exception) { }
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.SelectedTab is not null)
            {
                ViewModel.SelectedTab.AutoSaveStatus = "DropCut v1.0.0 — أداة القصاصة المتطورة";
            }
        }
        catch (Exception) { }
    }

    private void SaveCurrentTabImage()
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            if (tab?.ImageBytes is not { Length: > 0 } bytes)
            {
                return;
            }

            var picturesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DropCut");
            Directory.CreateDirectory(picturesPath);
            var fileName = $"DropCut_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var fullPath = Path.Combine(picturesPath, fileName);

            File.WriteAllBytes(fullPath, bytes);
            tab.ImagePath = fullPath;
            tab.AutoSaveStatus = $"تم الحفظ في {fileName}";
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void CopyCurrentTabImage()
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            if (tab?.ImageBytes is not { Length: > 0 } bytes)
            {
                return;
            }

            var dp = new DataPackage();
            var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                writer.StoreAsync().AsTask().GetAwaiter().GetResult();
                writer.DetachStream();
            }
            stream.Seek(0);
            dp.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            Clipboard.SetContent(dp);
            tab.AutoSaveStatus = "تم النسخ إلى الحافظة بنجاح";
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    // ==========================================
    // 6. TabView Management & Context Menus
    // ==========================================
    private void MainTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        try
        {
            if (args.Item is ShellTabModel tab)
            {
                ViewModel.CloseTabCommand.Execute(tab);
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void ContextPin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ShellTabModel tab })
            {
                ViewModel.TogglePinCommand.Execute(tab);
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void ContextFavorite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ShellTabModel tab })
            {
                ViewModel.ToggleFavoriteCommand.Execute(tab);
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void ContextClose_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ShellTabModel tab })
            {
                ViewModel.CloseTabCommand.Execute(tab);
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void ContextCloseRight_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ShellTabModel tab })
            {
                ViewModel.CloseTabsToTheRightCommand.Execute(tab);
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void ContextCloseOthers_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ShellTabModel tab })
            {
                ViewModel.CloseOtherTabsCommand.Execute(tab);
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }

    private void ContextCloseAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.CloseAllTabsCommand.Execute(null);
        }
        catch (Exception)
        {
            // Protected Zero-Crash event boundary
        }
    }
}
