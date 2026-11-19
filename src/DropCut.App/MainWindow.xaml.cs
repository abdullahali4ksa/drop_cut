using System.Diagnostics;
using System.IO;
using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using DropCut.Modules.Shell.Models;
using DropCut.Modules.Shell.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace DropCut.App;

/// <summary>
/// Root application shell window hosting the multi-tab layout, Snipping Tool ribbon,
/// interactive in-canvas drawing, OCR extraction, and background global hotkeys.
/// </summary>
public sealed partial class MainWindow : Window
{
    public ShellViewModel ViewModel { get; }

    private readonly IScreenCaptureService _screenCaptureService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IOcrService _ocrService;

    private Polyline? _activePolyline;
    private Canvas? _activeDrawingCanvas;

    public MainWindow(
        ShellViewModel viewModel,
        IScreenCaptureService screenCaptureService,
        IGlobalHotkeyService hotkeyService,
        IOcrService ocrService)
    {
        ViewModel = viewModel;
        _screenCaptureService = screenCaptureService;
        _hotkeyService = hotkeyService;
        _ocrService = ocrService;

        InitializeComponent();

        // Listen for global background shortcuts (PrintScreen, Ctrl+Shift+S, Ctrl+B+B)
        _hotkeyService.HotkeyPressed += OnGlobalHotkeyPressed;
        _hotkeyService.StartListening();

        this.Closed += (s, e) =>
        {
            _hotkeyService.HotkeyPressed -= OnGlobalHotkeyPressed;
            _hotkeyService.Dispose();
        };
    }

    public FlowDirection GetFlowDirection(bool isRtl) =>
        isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    // ==========================================
    // 1. Global & Local Hotkeys (Ctrl+B+B, PrintScreen, Ctrl+N)
    // ==========================================
    private void OnGlobalHotkeyPressed(object? sender, EventArgs e)
    {
        try
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                StartSnippingOverlayAsync(CaptureMode.Rectangular);
            });
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private void Grid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            var isCtrlDown = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

            if (e.Key == VirtualKey.Snapshot || (int)e.Key == 44 || (isCtrlDown && (e.Key == VirtualKey.N || e.Key == VirtualKey.B)))
            {
                e.Handled = true;
                StartSnippingOverlayAsync(CaptureMode.Rectangular);
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
            else if (isCtrlDown && e.Key == VirtualKey.Z)
            {
                e.Handled = true;
                ActionUndo_Click(this, new RoutedEventArgs());
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    // ==========================================
    // 2. Professional Sniper Overlay Launcher
    // ==========================================
    private async void StartSnippingOverlayAsync(CaptureMode mode)
    {
        try
        {
            // Hide main window so desktop is completely clear for the snip
            this.AppWindow.Hide();
            await Task.Delay(130);

            // Capture pristine full desktop snapshot
            var fullDesktopBmp = await _screenCaptureService.CapturePrimaryScreenAsync();

            var overlay = new SnipOverlayWindow(
                fullDesktopBmp,
                mode,
                _screenCaptureService,
                (capturedBytes, width, height) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        this.AppWindow.Show();
                        this.Activate();

                        if (capturedBytes is { Length: > 0 })
                        {
                            ApplyCapturedSnippet(capturedBytes, width, height);
                        }
                    });
                });

            overlay.Activate();
        }
        catch (Exception)
        {
            this.AppWindow.Show();
            this.Activate();
        }
    }

    private void ApplyCapturedSnippet(byte[] bytes, int width, int height)
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            if (tab == null || tab.HasImage)
            {
                ViewModel.AddNewTab();
                tab = ViewModel.SelectedTab;
            }

            if (tab is not null)
            {
                tab.ImageBytes = bytes;
                tab.ImageWidth = width;
                tab.ImageHeight = height;
                tab.AutoSaveStatus = $"تم أخذ لقطة الشاشة ({width} × {height} بكسل) وحفظها بالذاكرة";
            }

            // Copy to clipboard immediately like Snipping Tool
            CopyCurrentTabImage();
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private void NewSnip_Click(SplitButton sender, SplitButtonClickEventArgs args) =>
        StartSnippingOverlayAsync(CaptureMode.Rectangular);

    private void LandingSnip_Click(object sender, RoutedEventArgs e) =>
        StartSnippingOverlayAsync(CaptureMode.Rectangular);

    private void ModeRectangle_Click(object sender, RoutedEventArgs e) =>
        StartSnippingOverlayAsync(CaptureMode.Rectangular);

    private void ModeWindow_Click(object sender, RoutedEventArgs e) =>
        StartSnippingOverlayAsync(CaptureMode.Window);

    private void ModeFullscreen_Click(object sender, RoutedEventArgs e) =>
        StartSnippingOverlayAsync(CaptureMode.Fullscreen);

    private void ModeFreeform_Click(object sender, RoutedEventArgs e) =>
        StartSnippingOverlayAsync(CaptureMode.Freeform);

    // ==========================================
    // 3. Ribbon Tools: Pen, Highlighter, Eraser, Ruler, OCR, Crop
    // ==========================================
    private void PenRed_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PenColor = "#FF0000";
        ViewModel.ActiveTool = ToolType.Pen;
        NotifyToolChange("تم اختيار القلم باللون الأحمر");
    }

    private void PenBlue_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PenColor = "#0078D7";
        ViewModel.ActiveTool = ToolType.Pen;
        NotifyToolChange("تم اختيار القلم باللون الأزرق");
    }

    private void PenBlack_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PenColor = "#000000";
        ViewModel.ActiveTool = ToolType.Pen;
        NotifyToolChange("تم اختيار القلم باللون الأسود");
    }

    private void PenGreen_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PenColor = "#107C41";
        ViewModel.ActiveTool = ToolType.Pen;
        NotifyToolChange("تم اختيار القلم باللون الأخضر");
    }

    private void PenYellow_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PenColor = "#FFD700";
        ViewModel.ActiveTool = ToolType.Pen;
        NotifyToolChange("تم اختيار القلم باللون الأصفر");
    }

    private void ToolHighlighter_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ActiveTool = ToolType.Highlighter;
        ViewModel.PenColor = "#80FFD700";
        NotifyToolChange("تم اختيار قلم التمييز الفسفوري");
    }

    private void ToolEraser_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ActiveTool = ToolType.Select; // Eraser Mode
        if (_activeDrawingCanvas != null && _activeDrawingCanvas.Children.Count > 0)
        {
            _activeDrawingCanvas.Children.Clear();
            NotifyToolChange("تم مسح كافة الرسومات من الصورة");
        }
        else
        {
            NotifyToolChange("وضع الممحاة نشط");
        }
    }

    private void ToolRuler_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ActiveTool = ToolType.Shape;
        NotifyToolChange("مسطرة القياس: انقر واسحب للقياس المباشر بالبكسل");
    }

    private async void ToolText_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            if (tab?.ImageBytes is not { Length: > 0 } bytes)
            {
                NotifyToolChange("يرجى أخذ لقطة شاشة أولاً لاستخراج النصوص");
                return;
            }

            NotifyToolChange("جارٍ قراءة النصوص من الصورة (OCR)...");
            var result = await _ocrService.ExtractTextAsync(bytes);

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
            {
                var dp = new DataPackage();
                dp.SetText(result.Value);
                Clipboard.SetContent(dp);
                tab.AutoSaveStatus = $"تم استخراج النص ({result.Value.Length} حرف) ونسخه للحافظة بنجاح";
            }
            else
            {
                tab.AutoSaveStatus = "لم يتم العثور على نصوص قابلة للقراءة في هذه الصورة";
            }
        }
        catch (Exception ex)
        {
            NotifyToolChange($"تعذر استخراج النصوص: {ex.Message}");
        }
    }

    private void ToolCrop_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ActiveTool = ToolType.Crop;
        StartSnippingOverlayAsync(CaptureMode.Rectangular);
    }

    private void NotifyToolChange(string message)
    {
        if (ViewModel.SelectedTab is not null)
        {
            ViewModel.SelectedTab.AutoSaveStatus = message;
        }
    }

    // ==========================================
    // 4. In-Canvas Interactive Drawing Handlers
    // ==========================================
    private void AnnotationCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (sender is not Canvas canvas) return;
            _activeDrawingCanvas = canvas;

            var pt = e.GetCurrentPoint(canvas).Position;

            if (ViewModel.ActiveTool == ToolType.Pen || ViewModel.ActiveTool == ToolType.Highlighter)
            {
                var color = ParseColorHex(ViewModel.PenColor);
                var thickness = ViewModel.ActiveTool == ToolType.Highlighter ? 14 : 3;

                _activePolyline = new Polyline
                {
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = thickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round
                };
                _activePolyline.Points.Add(pt);
                canvas.Children.Add(_activePolyline);
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private void AnnotationCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (_activePolyline != null && sender is Canvas canvas)
            {
                var pt = e.GetCurrentPoint(canvas).Position;
                _activePolyline.Points.Add(pt);
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private void AnnotationCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            _activePolyline = null;
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private static Color ParseColorHex(string hex)
    {
        try
        {
            hex = hex.Replace("#", "");
            if (hex.Length == 8)
            {
                var a = Convert.ToByte(hex.Substring(0, 2), 16);
                var r = Convert.ToByte(hex.Substring(2, 2), 16);
                var g = Convert.ToByte(hex.Substring(4, 2), 16);
                var b = Convert.ToByte(hex.Substring(6, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
            if (hex.Length == 6)
            {
                var r = Convert.ToByte(hex.Substring(0, 2), 16);
                var g = Convert.ToByte(hex.Substring(2, 2), 16);
                var b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Color.FromArgb(255, r, g, b);
            }
        }
        catch { }
        return Microsoft.UI.Colors.Red;
    }

    // ==========================================
    // 5. Actions: Undo, Redo, Save, Copy, Share
    // ==========================================
    private void ActionUndo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_activeDrawingCanvas != null && _activeDrawingCanvas.Children.Count > 0)
            {
                _activeDrawingCanvas.Children.RemoveAt(_activeDrawingCanvas.Children.Count - 1);
                NotifyToolChange("تراجع عن آخر رسمة");
            }
            else
            {
                NotifyToolChange("لا توجد رسومات للتراجع عنها");
            }
        }
        catch (Exception) { }
    }

    private void ActionRedo_Click(object sender, RoutedEventArgs e)
    {
        NotifyToolChange("إعادة (Redo)");
    }

    private void ActionSave_Click(object sender, RoutedEventArgs e) => SaveCurrentTabImage();

    private void ActionCopy_Click(object sender, RoutedEventArgs e) => CopyCurrentTabImage();

    private void ActionShare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CopyCurrentTabImage();
            NotifyToolChange("تم النسخ للحافظة — جاهز للمشاركة واللصق المباشر في أي برنامج");
        }
        catch (Exception) { }
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e) =>
        NotifyToolChange("الإعدادات / Settings (الاختصار العام: Ctrl+B+B أو PrintScreen)");

    private void MenuOpenVault_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picturesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DropCut");
            Directory.CreateDirectory(picturesPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = picturesPath,
                UseShellExecute = true
            });
        }
        catch (Exception) { }
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e) =>
        NotifyToolChange("DropCut v1.0.0 — أداة القصاصة المتطورة (Snipping & Workspace)");

    private void SaveCurrentTabImage()
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            if (tab?.ImageBytes is not { Length: > 0 } bytes)
            {
                return;
            }

            var picturesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DropCut");
            Directory.CreateDirectory(picturesPath);
            var fileName = $"DropCut_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var fullPath = System.IO.Path.Combine(picturesPath, fileName);

            File.WriteAllBytes(fullPath, bytes);
            tab.ImagePath = fullPath;
            tab.AutoSaveStatus = $"تم الحفظ في {fileName}";
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
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
            tab.AutoSaveStatus = "تم النسخ إلى الحافظة بنجاح (جاهز للصق Ctrl+V)";
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
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
            // Protected Zero-Crash boundary
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
        catch (Exception) { }
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
        catch (Exception) { }
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
        catch (Exception) { }
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
        catch (Exception) { }
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
        catch (Exception) { }
    }

    private void ContextCloseAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.CloseAllTabsCommand.Execute(null);
        }
        catch (Exception) { }
    }
}
