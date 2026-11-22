using System.Diagnostics;
using System.IO;
using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using DropCut.Core.Events;
using DropCut.Modules.Shell.Models;
using DropCut.Modules.Shell.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using DropCut.Modules.Settings.Contracts;

namespace DropCut.App;

/// <summary>
/// Root application shell window hosting the multi-tab layout, Snipping Tool ribbon,
/// interactive in-canvas drawing, in-tab image cropping, external Drag &amp; Drop sharing, and global hotkeys.
/// </summary>
public sealed partial class MainWindow : Window
{
    public ShellViewModel ViewModel { get; }

    private readonly IScreenCaptureService _screenCaptureService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IOcrService _ocrService;
    private readonly ILocalizationService _localizationService;
    private readonly IEventBus _eventBus;

    private Polyline? _activePolyline;
    private Canvas? _activeDrawingCanvas;

    // Tool styling & customization state
    private string _highlighterColor = "#FFFF00";
    private int _highlighterThickness = 20;
    private int _penThickness = 4;

    // Version 2.0: Geometric Shapes & Vector Tool state
    private ShapeType _currentShapeType = ShapeType.Rectangle;
    private string _shapeColor = "#FF0000";
    private int _shapeThickness = 4;
    private bool _shapeFill = false;
    private bool _isDrawingShape;
    private Point _shapeStartPoint;
    private FrameworkElement? _activeShapeElement;

    // Version 2.0: Sequential Numbered Step Badges state (①, ②, ③...)
    private int _nextBadgeNumber = 1;
    private string _badgeColor = "#0078D7";
    private int _badgeSize = 32;

    // Version 2.0: Redo Stack
    private readonly Stack<UIElement> _redoStack = new();

    // In-tab cropping state (8-point interactive handles like MS Paint)
    private Point _tabCropStart;
    private double _tabCropX;
    private double _tabCropY;
    private double _tabCropW;
    private double _tabCropH;
    private bool _isTabCropDragging;
    private bool _isMovingCropBox;
    private bool _isNewCropDrawing;
    private Point _cropBoxMoveStart;
    private double _cropBoxInitialX;
    private double _cropBoxInitialY;
    private string? _activeCropHandle;
    private Point _cropDragStartPoint;
    private double _initialCropX;
    private double _initialCropY;
    private double _initialCropW;
    private double _initialCropH;

    // In-tab privacy blur state
    private Point _tabBlurStart;
    private double _tabBlurX;
    private double _tabBlurY;
    private double _tabBlurW;
    private double _tabBlurH;
    private bool _isTabBlurDragging;

    public MainWindow(
        ShellViewModel viewModel,
        IScreenCaptureService screenCaptureService,
        IGlobalHotkeyService hotkeyService,
        IOcrService ocrService,
        ILocalizationService localizationService,
        IEventBus eventBus)
    {
        ViewModel = viewModel;
        _screenCaptureService = screenCaptureService;
        _hotkeyService = hotkeyService;
        _ocrService = ocrService;
        _localizationService = localizationService;
        _eventBus = eventBus;

        InitializeComponent();

        _eventBus.Subscribe<LanguageChangedEvent>((e, ct) =>
        {
            DispatcherQueue.TryEnqueue(() => ApplyLocalization());
            return Task.CompletedTask;
        });

        ApplyLocalization();

        // Default to clean English for all users. If Windows OS has a non-English supported language (excluding Arabic unless explicitly chosen in Settings), adapt cleanly.
        try
        {
            var sysCulture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (!sysCulture.Equals("ar", StringComparison.OrdinalIgnoreCase) && !sysCulture.Equals("en", StringComparison.OrdinalIgnoreCase))
            {
                if (_localizationService.SupportedLanguages.Any(l => l.Code.Equals(sysCulture, StringComparison.OrdinalIgnoreCase)))
                {
                    _ = _localizationService.SetLanguageAsync(sysCulture);
                }
            }
        }
        catch { }

        // Configure window icon
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                this.AppWindow.SetIcon(iconPath);
            }
        }
        catch { }

        // Listen for global background shortcuts (PrintScreen, Ctrl+Shift+S, Ctrl+B+B)
        _hotkeyService.HotkeyPressed += OnGlobalHotkeyPressed;
        _hotkeyService.StartListening();

        this.Closed += (s, e) =>
        {
            _hotkeyService.HotkeyPressed -= OnGlobalHotkeyPressed;
            _hotkeyService.Dispose();
        };

        // Listen for window resize to dynamically maintain auto-fit scaling on small/medium/large windows
        this.SizeChanged += MainWindow_SizeChanged;

        // Initialize with Hand tool as default
        SetToolMode(ToolType.Hand);
    }

    private void MainTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SetToolMode(ToolType.Hand);
        _activeDrawingCanvas = GetActiveDrawingCanvas();
        if (ViewModel.SelectedTab?.HasImage == true)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                AutoFitImageToViewport(ViewModel.SelectedTab);
            });
        }
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
            else if (isCtrlDown && (e.Key == VirtualKey.Add || (int)e.Key == 187))
            {
                e.Handled = true;
                ZoomInCurrentTab();
            }
            else if (isCtrlDown && (e.Key == VirtualKey.Subtract || (int)e.Key == 189))
            {
                e.Handled = true;
                ZoomOutCurrentTab();
            }
            else if (isCtrlDown && e.Key == VirtualKey.P)
            {
                e.Handled = true;
                ActionPrint_Click(this, new RoutedEventArgs());
            }
            else if (isCtrlDown && (e.Key == VirtualKey.Number0 || e.Key == VirtualKey.NumberPad0))
            {
                e.Handled = true;
                ZoomResetCurrentTab();
            }
            else if (isCtrlDown && (e.Key == VirtualKey.Number9 || e.Key == VirtualKey.NumberPad9))
            {
                e.Handled = true;
                AutoFitImageToViewport(ViewModel.SelectedTab);
            }
            else if (isCtrlDown && e.Key == VirtualKey.Y)
            {
                e.Handled = true;
                ActionRedo_Click(this, new RoutedEventArgs());
            }
            else if (!isCtrlDown && e.Key == VirtualKey.H)
            {
                e.Handled = true;
                ToolHand_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                SetToolMode(ToolType.Hand);
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    // ==========================================
    // 2. Professional Sniper Overlay Launcher (Desktop Snip)
    // ==========================================
    private async void StartSnippingOverlayAsync(CaptureMode mode)
    {
        try
        {
            if (ViewModel.DelaySeconds > 0)
            {
                NotifyToolChange(_localizationService.GetString("Delay_" + ViewModel.DelaySeconds + "s"));
                await Task.Delay(ViewModel.DelaySeconds * 1000);
            }

            // Hide main window so desktop is completely clear for the snip
            this.AppWindow.Hide();
            // Allow ample time for Windows 11 DWM window fade-out animation to complete 100%
            await Task.Delay(320);

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
                tab.IsCropping = false;
                tab.IsBlurring = false;
                tab.IsDrawingMode = false;
                tab.AutoSaveStatus = _localizationService.GetString("Status_Captured", width, height);

                // Version 2.0: Smart Auto-Fit Viewport Scaling (P08-T001)
                // Schedule with low priority so layout engine arranges viewport before calculating aspect ratio
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    AutoFitImageToViewport(tab);
                });
            }

            _activeDrawingCanvas = GetActiveDrawingCanvas();

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

    private void DelayNone_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DelaySeconds = 0;
        ToolTipService.SetToolTip(BtnDelayTool, _localizationService.GetString("Ribbon_Delay_None"));
        NotifyToolChange(_localizationService.GetString("Ribbon_Delay_None"));
    }

    private void Delay3s_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DelaySeconds = 3;
        ToolTipService.SetToolTip(BtnDelayTool, _localizationService.GetString("Ribbon_Delay_3s"));
        NotifyToolChange(_localizationService.GetString("Ribbon_Delay_3s"));
    }

    private void Delay5s_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DelaySeconds = 5;
        ToolTipService.SetToolTip(BtnDelayTool, _localizationService.GetString("Ribbon_Delay_5s"));
        NotifyToolChange(_localizationService.GetString("Ribbon_Delay_5s"));
    }

    private void Delay10s_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DelaySeconds = 10;
        ToolTipService.SetToolTip(BtnDelayTool, _localizationService.GetString("Ribbon_Delay_10s"));
        NotifyToolChange(_localizationService.GetString("Ribbon_Delay_10s"));
    }

    // ==========================================
    // 3. External Drag & Drop Sharing to WhatsApp / Telegram / Explorer
    // ==========================================
    private void PrepareDataPackageForDragSync(ShellTabModel? tab, DataPackage data)
    {
        if (tab?.ImageBytes is not { Length: > 0 } bytes)
        {
            return;
        }

        try
        {
            data.RequestedOperation = DataPackageOperation.Copy | DataPackageOperation.Move | DataPackageOperation.Link;

            // 1. Create a persistent temporary PNG file so Windows File Explorer, WhatsApp, Telegram, Outlook, Discord receive real file drops!
            var tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DropCut");
            System.IO.Directory.CreateDirectory(tempFolder);
            var safeTitle = string.Join("_", tab.Title.Split(System.IO.Path.GetInvalidFileNameChars()));
            var fileName = $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filePath = System.IO.Path.Combine(tempFolder, fileName);
            System.IO.File.WriteAllBytes(filePath, bytes);

            var storageFile = StorageFile.GetFileFromPathAsync(filePath).AsTask().GetAwaiter().GetResult();
            data.SetStorageItems(new[] { storageFile });

            // 2. Also attach direct Bitmap RandomAccessStream for apps that accept bitmap drag/drop
            var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                writer.StoreAsync().AsTask().GetAwaiter().GetResult();
                writer.DetachStream();
            }
            stream.Seek(0);
            data.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));

            // 3. Text fallback
            data.SetText(filePath);

            // 4. Set rich drag metadata and thumbnail on DataPackage (Pure Localized Strings)
            data.Properties.Title = _localizationService.GetString("Action_DragDropPill");
            data.Properties.Description = _localizationService.GetString("Action_DragDropPillToolTip");
            data.Properties.Thumbnail = RandomAccessStreamReference.CreateFromStream(stream);
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private void ConfigureCompactDragUI(DragUI dragUI, ShellTabModel? tab)
    {
        if (tab?.ImageBytes is not { Length: > 0 } bytes)
        {
            return;
        }

        try
        {
            var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                writer.StoreAsync().AsTask().GetAwaiter().GetResult();
                writer.DetachStream();
            }
            stream.Seek(0);

            var thumb = new BitmapImage
            {
                DecodePixelWidth = 140,
                DecodePixelHeight = 140
            };
            thumb.SetSource(stream);

            dragUI.SetContentFromBitmapImage(thumb, new Windows.Foundation.Point(70, 70));
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
    }

    private void MainTabView_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
    {
        try
        {
            var tab = args.Item as ShellTabModel ?? ViewModel.SelectedTab;
            PrepareDataPackageForDragSync(tab, args.Data);
            NotifyToolChange(_localizationService.GetString("Status_Dragging"));
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
    }

    private void MainTabView_AddTabButtonClick(TabView sender, object args)
    {
        StartSnippingOverlayAsync(CaptureMode.Rectangular);
    }

    private void TabItem_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (ViewModel.ActiveTool != ToolType.Hand)
        {
            args.Cancel = true;
            return;
        }

        var deferral = args.GetDeferral();
        try
        {
            var tab = (sender as FrameworkElement)?.DataContext as ShellTabModel ?? ViewModel.SelectedTab;
            PrepareDataPackageForDragSync(tab, args.Data);
            ConfigureCompactDragUI(args.DragUI, tab);
            NotifyToolChange(_localizationService.GetString("Status_Dragging"));
        }
        catch (Exception) { }
        finally
        {
            deferral.Complete();
        }
    }

    private void ImageContainer_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        // Enforce strict drag locking while editing is active
        if (ViewModel.ActiveTool != ToolType.Hand)
        {
            args.Cancel = true;
            return;
        }

        var deferral = args.GetDeferral();
        try
        {
            var tab = ViewModel.SelectedTab;
            PrepareDataPackageForDragSync(tab, args.Data);
            ConfigureCompactDragUI(args.DragUI, tab);
            NotifyToolChange(_localizationService.GetString("Status_Dragging"));
        }
        catch (Exception) { }
        finally
        {
            deferral.Complete();
        }
    }

    private void ShareButton_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (ViewModel.ActiveTool != ToolType.Hand)
        {
            args.Cancel = true;
            return;
        }

        var deferral = args.GetDeferral();
        try
        {
            var tab = ViewModel.SelectedTab;
            PrepareDataPackageForDragSync(tab, args.Data);
            ConfigureCompactDragUI(args.DragUI, tab);
            NotifyToolChange(_localizationService.GetString("Status_Dragging"));
        }
        catch (Exception) { }
        finally
        {
            deferral.Complete();
        }
    }

    private void MainTabView_DragOver(object sender, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }
        catch { }
    }

    private async void MainTabView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items)
                {
                    if (item is StorageFile file && (file.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                                                    file.FileType.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                    file.FileType.Equals(".bmp", StringComparison.OrdinalIgnoreCase)))
                    {
                        using var stream = await file.OpenStreamForReadAsync();
                        using var ms = new MemoryStream();
                        await stream.CopyToAsync(ms);
                        var bytes = ms.ToArray();

                        ViewModel.AddNewTab();
                        if (ViewModel.SelectedTab != null)
                        {
                            ViewModel.SelectedTab.Title = file.Name;
                            ViewModel.SelectedTab.ImageBytes = bytes;
                            ViewModel.SelectedTab.AutoSaveStatus = _localizationService.GetString("Status_ImageLoaded", file.Name);
                            AutoFitImageToViewport(ViewModel.SelectedTab);
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    // ==========================================
    // 4. In-Tab Crop Feature (Crops the Current Tab Image)
    // ==========================================
    private void ToolCrop_Click(object sender, RoutedEventArgs e)
    {
        var tab = ViewModel.SelectedTab;
        if (tab?.ImageBytes is not { Length: > 0 })
        {
            NotifyToolChange(_localizationService.GetString("Action_NewSnip_Tip"));
            return;
        }

        if (ViewModel.ActiveTool == ToolType.Crop)
        {
            BtnApplyInTabCrop_Click(sender, e);
        }
        else
        {
            var scroll = GetActiveScrollViewer();
            var cropBox = scroll != null ? FindChildByTag<Border>(scroll, "InTabCropBox") : null;
            var cropCanvas = cropBox?.Parent as Canvas;
            double displayW = cropCanvas?.ActualWidth > 0 ? cropCanvas.ActualWidth : (tab.ImageWidth > 0 ? tab.ImageWidth : 800);
            double displayH = cropCanvas?.ActualHeight > 0 ? cropCanvas.ActualHeight : (tab.ImageHeight > 0 ? tab.ImageHeight : 600);

            int insetX = (int)Math.Min(30, Math.Max(10, displayW / 12));
            int insetY = (int)Math.Min(30, Math.Max(10, displayH / 12));
            _tabCropX = insetX;
            _tabCropY = insetY;
            _tabCropW = Math.Max(50, displayW - insetX * 2);
            _tabCropH = Math.Max(50, displayH - insetY * 2);

            SetToolMode(ToolType.Crop);

            if (scroll != null)
            {
                var box = FindChildByTag<Border>(scroll, "InTabCropBox");
                if (box?.Parent is Canvas canvas)
                {
                    UpdateCropVisuals(canvas);
                }
            }
        }
    }

    private void CropCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (sender is not Canvas canvas) return;
            try { canvas.CapturePointer(e.Pointer); } catch { }
            var pt = e.GetCurrentPoint(canvas).Position;

            var hitHandle = HitTestCropHandle(pt, _tabCropX, _tabCropY, _tabCropW, _tabCropH);
            if (hitHandle != null)
            {
                _activeCropHandle = hitHandle;
                _cropDragStartPoint = pt;
                _initialCropX = _tabCropX;
                _initialCropY = _tabCropY;
                _initialCropW = _tabCropW;
                _initialCropH = _tabCropH;
                _isTabCropDragging = true;
                _isMovingCropBox = false;
                _isNewCropDrawing = false;
            }
            else if (_tabCropW > 20 && _tabCropH > 20 &&
                     pt.X >= _tabCropX && pt.X <= _tabCropX + _tabCropW &&
                     pt.Y >= _tabCropY && pt.Y <= _tabCropY + _tabCropH)
            {
                // Clicked inside existing crop area -> Move / Pan the crop box
                _activeCropHandle = null;
                _isMovingCropBox = true;
                _isNewCropDrawing = false;
                _cropBoxMoveStart = pt;
                _cropBoxInitialX = _tabCropX;
                _cropBoxInitialY = _tabCropY;
                _isTabCropDragging = true;
            }
            else
            {
                // Clicked outside -> prepare to draw a new crop box if dragged
                _activeCropHandle = null;
                _isMovingCropBox = false;
                _isNewCropDrawing = true;
                _tabCropStart = pt;
                _isTabCropDragging = true;
            }
        }
        catch (Exception) { }
    }

    private void CropCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (sender is not Canvas canvas) return;
            var pt = e.GetCurrentPoint(canvas).Position;

            if (_isTabCropDragging)
            {
                if (_activeCropHandle != null)
                {
                    double dx = pt.X - _cropDragStartPoint.X;
                    double dy = pt.Y - _cropDragStartPoint.Y;

                    switch (_activeCropHandle)
                    {
                        case "NW":
                            _tabCropX = Math.Min(_initialCropX + _initialCropW - 20, _initialCropX + dx);
                            _tabCropY = Math.Min(_initialCropY + _initialCropH - 20, _initialCropY + dy);
                            _tabCropW = _initialCropW - (_tabCropX - _initialCropX);
                            _tabCropH = _initialCropH - (_tabCropY - _initialCropY);
                            break;
                        case "N":
                            _tabCropY = Math.Min(_initialCropY + _initialCropH - 20, _initialCropY + dy);
                            _tabCropH = _initialCropH - (_tabCropY - _initialCropY);
                            break;
                        case "NE":
                            _tabCropY = Math.Min(_initialCropY + _initialCropH - 20, _initialCropY + dy);
                            _tabCropH = _initialCropH - (_tabCropY - _initialCropY);
                            _tabCropW = Math.Max(20, _initialCropW + dx);
                            break;
                        case "E":
                            _tabCropW = Math.Max(20, _initialCropW + dx);
                            break;
                        case "SE":
                            _tabCropW = Math.Max(20, _initialCropW + dx);
                            _tabCropH = Math.Max(20, _initialCropH + dy);
                            break;
                        case "S":
                            _tabCropH = Math.Max(20, _initialCropH + dy);
                            break;
                        case "SW":
                            _tabCropX = Math.Min(_initialCropX + _initialCropW - 20, _initialCropX + dx);
                            _tabCropW = _initialCropW - (_tabCropX - _initialCropX);
                            _tabCropH = Math.Max(20, _initialCropH + dy);
                            break;
                        case "W":
                            _tabCropX = Math.Min(_initialCropX + _initialCropW - 20, _initialCropX + dx);
                            _tabCropW = _initialCropW - (_tabCropX - _initialCropX);
                            break;
                    }
                    UpdateCropVisuals(canvas);
                }
                else if (_isMovingCropBox)
                {
                    double dx = pt.X - _cropBoxMoveStart.X;
                    double dy = pt.Y - _cropBoxMoveStart.Y;
                    var tab = ViewModel.SelectedTab;
                    double maxW = tab?.ImageWidth > 0 ? tab.ImageWidth : 3000;
                    double maxH = tab?.ImageHeight > 0 ? tab.ImageHeight : 3000;

                    _tabCropX = Math.Clamp(_cropBoxInitialX + dx, 0, Math.Max(0, maxW - _tabCropW));
                    _tabCropY = Math.Clamp(_cropBoxInitialY + dy, 0, Math.Max(0, maxH - _tabCropH));
                    UpdateCropVisuals(canvas);
                }
                else if (_isNewCropDrawing)
                {
                    double dist = Math.Max(Math.Abs(pt.X - _tabCropStart.X), Math.Abs(pt.Y - _tabCropStart.Y));
                    if (dist > 8)
                    {
                        _tabCropX = Math.Min(_tabCropStart.X, pt.X);
                        _tabCropY = Math.Min(_tabCropStart.Y, pt.Y);
                        _tabCropW = Math.Abs(pt.X - _tabCropStart.X);
                        _tabCropH = Math.Abs(pt.Y - _tabCropStart.Y);
                        UpdateCropVisuals(canvas);
                    }
                }
            }
            else
            {
                var hoverHandle = HitTestCropHandle(pt, _tabCropX, _tabCropY, _tabCropW, _tabCropH);
                switch (hoverHandle)
                {
                    case "NW":
                    case "SE":
                        SetCanvasCursor(InputSystemCursorShape.SizeNorthwestSoutheast);
                        break;
                    case "NE":
                    case "SW":
                        SetCanvasCursor(InputSystemCursorShape.SizeNortheastSouthwest);
                        break;
                    case "N":
                    case "S":
                        SetCanvasCursor(InputSystemCursorShape.SizeNorthSouth);
                        break;
                    case "E":
                    case "W":
                        SetCanvasCursor(InputSystemCursorShape.SizeWestEast);
                        break;
                    default:
                        if (_tabCropW > 20 && _tabCropH > 20 &&
                            pt.X >= _tabCropX && pt.X <= _tabCropX + _tabCropW &&
                            pt.Y >= _tabCropY && pt.Y <= _tabCropY + _tabCropH)
                        {
                            SetCanvasCursor(InputSystemCursorShape.SizeAll);
                        }
                        else
                        {
                            SetCanvasCursor(InputSystemCursorShape.Cross);
                        }
                        break;
                }
            }
        }
        catch (Exception) { }
    }

    private void CropCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (sender is not Canvas canvas) return;
            try { canvas.ReleasePointerCapture(e.Pointer); } catch { }
            _isTabCropDragging = false;
            _activeCropHandle = null;
            _isMovingCropBox = false;
            _isNewCropDrawing = false;
            UpdateCropVisuals(canvas);
        }
        catch (Exception) { }
    }

    private void UpdateCropVisuals(Canvas canvas)
    {
        var box = FindChildByTag<Border>(canvas, "InTabCropBox");
        if (box != null)
        {
            box.Visibility = Visibility.Visible;
            Canvas.SetLeft(box, _tabCropX);
            Canvas.SetTop(box, _tabCropY);
            box.Width = Math.Max(10, _tabCropW);
            box.Height = Math.Max(10, _tabCropH);
        }

        double cx = _tabCropX;
        double cy = _tabCropY;
        double cw = Math.Max(10, _tabCropW);
        double ch = Math.Max(10, _tabCropH);

        // Position 4 Corner brackets (18x18, center offset: -9)
        PositionHandle(canvas, "CropHandle_NW", cx - 9, cy - 9);
        PositionHandle(canvas, "CropHandle_NE", cx + cw - 9, cy - 9);
        PositionHandle(canvas, "CropHandle_SE", cx + cw - 9, cy + ch - 9);
        PositionHandle(canvas, "CropHandle_SW", cx - 9, cy + ch - 9);

        // Position 4 Edge prominent dashes (Top/Bottom are 42x8, Left/Right are 8x42)
        PositionHandle(canvas, "CropHandle_N",  cx + cw / 2 - 21, cy - 4);
        PositionHandle(canvas, "CropHandle_S",  cx + cw / 2 - 21, cy + ch - 4);
        PositionHandle(canvas, "CropHandle_W",  cx - 4, cy + ch / 2 - 21);
        PositionHandle(canvas, "CropHandle_E",  cx + cw - 4, cy + ch / 2 - 21);

        var bar = FindChildByTag<StackPanel>(canvas, "InTabCropActionBar");
        if (bar != null)
        {
            bar.Visibility = Visibility.Visible;
            double barTop = cy + ch + 12;
            var tab = ViewModel.SelectedTab;
            if (tab?.ImageHeight > 0 && barTop + 45 > tab.ImageHeight)
            {
                barTop = Math.Max(10, cy + ch - 45);
            }
            Canvas.SetLeft(bar, Math.Max(10, cx));
            Canvas.SetTop(bar, barTop);
        }
    }

    private static void PositionHandle(Canvas canvas, string tag, double left, double top)
    {
        var handle = FindChildByTag<Border>(canvas, tag);
        if (handle != null)
        {
            handle.Visibility = Visibility.Visible;
            Canvas.SetLeft(handle, left);
            Canvas.SetTop(handle, top);
        }
    }

    private static string? HitTestCropHandle(Point pt, double cx, double cy, double cw, double ch)
    {
        const double cornerTol = 24;
        const double edgeBreadthTol = 18;
        const double edgeLengthTol = 28;

        bool Near(double hx, double hy, double rx, double ry) =>
            Math.Abs(pt.X - hx) <= rx && Math.Abs(pt.Y - hy) <= ry;

        // Corners
        if (Near(cx, cy, cornerTol, cornerTol)) return "NW";
        if (Near(cx + cw, cy, cornerTol, cornerTol)) return "NE";
        if (Near(cx + cw, cy + ch, cornerTol, cornerTol)) return "SE";
        if (Near(cx, cy + ch, cornerTol, cornerTol)) return "SW";

        // Top & Bottom Edges (wide dashes)
        if (Near(cx + cw / 2, cy, edgeLengthTol, edgeBreadthTol)) return "N";
        if (Near(cx + cw / 2, cy + ch, edgeLengthTol, edgeBreadthTol)) return "S";

        // Left & Right Edges (tall dashes)
        if (Near(cx, cy + ch / 2, edgeBreadthTol, edgeLengthTol)) return "W";
        if (Near(cx + cw, cy + ch / 2, edgeBreadthTol, edgeLengthTol)) return "E";

        return null;
    }

    private void BtnApplyInTabCrop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            if (tab?.ImageBytes is not { Length: > 0 } bytes || _tabCropW <= 10 || _tabCropH <= 10)
            {
                return;
            }

            var scroll = GetActiveScrollViewer();
            var cropBox = scroll != null ? FindChildByTag<Border>(scroll, "InTabCropBox") : null;
            var cropCanvas = cropBox?.Parent as Canvas;
            double cW = cropCanvas?.ActualWidth > 0 ? cropCanvas.ActualWidth : (tab.ImageWidth > 0 ? tab.ImageWidth : _tabCropW);
            double cH = cropCanvas?.ActualHeight > 0 ? cropCanvas.ActualHeight : (tab.ImageHeight > 0 ? tab.ImageHeight : _tabCropH);
            double scaleX = cW > 0 && tab.ImageWidth > 0 ? (double)tab.ImageWidth / cW : 1.0;
            double scaleY = cH > 0 && tab.ImageHeight > 0 ? (double)tab.ImageHeight / cH : 1.0;

            int cropX = (int)Math.Round(_tabCropX * scaleX);
            int cropY = (int)Math.Round(_tabCropY * scaleY);
            int cropW = (int)Math.Round(_tabCropW * scaleX);
            int cropH = (int)Math.Round(_tabCropH * scaleY);

            var cropped = _screenCaptureService.CropBitmap(bytes, cropX, cropY, cropW, cropH);
            if (cropped is { Length: > 0 })
            {
                tab.ImageBytes = cropped;
                tab.ImageWidth = cropW;
                tab.ImageHeight = cropH;
                SetToolMode(ToolType.Hand);
                NotifyToolChange(_localizationService.GetString("Status_CropCommitted"));
                CopyCurrentTabImage();
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private void BtnCancelInTabCrop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetToolMode(ToolType.Hand);
            NotifyToolChange(_localizationService.GetString("Status_HandActive"));
        }
        catch (Exception) { }
    }

    // ==========================================
    // 4.1 In-Tab Privacy Blur & Redaction Feature
    // ==========================================
    private void ToolBlur_Click(object sender, RoutedEventArgs e)
    {
        var tab = ViewModel.SelectedTab;
        if (tab?.ImageBytes is not { Length: > 0 })
        {
            NotifyToolChange(_localizationService.GetString("Action_NewSnip_Tip"));
            return;
        }

        if (ViewModel.ActiveTool == ToolType.BlurRedact)
        {
            SetToolMode(ToolType.Hand);
        }
        else
        {
            SetToolMode(ToolType.BlurRedact);
        }
    }

    private void BlurCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (sender is not Canvas canvas) return;
            _tabBlurStart = e.GetCurrentPoint(canvas).Position;
            _isTabBlurDragging = true;

            var box = FindChildByTag<Border>(canvas, "InTabBlurBox");
            var bar = FindChildByTag<StackPanel>(canvas, "InTabBlurActionBar");
            if (box != null)
            {
                box.Visibility = Visibility.Visible;
                Canvas.SetLeft(box, _tabBlurStart.X);
                Canvas.SetTop(box, _tabBlurStart.Y);
                box.Width = 0;
                box.Height = 0;
            }
            if (bar != null)
            {
                bar.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception) { }
    }

    private void BlurCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (!_isTabBlurDragging || sender is not Canvas canvas) return;
            var cur = e.GetCurrentPoint(canvas).Position;
            _tabBlurX = Math.Min(_tabBlurStart.X, cur.X);
            _tabBlurY = Math.Min(_tabBlurStart.Y, cur.Y);
            _tabBlurW = Math.Abs(cur.X - _tabBlurStart.X);
            _tabBlurH = Math.Abs(cur.Y - _tabBlurStart.Y);

            var box = FindChildByTag<Border>(canvas, "InTabBlurBox");
            if (box != null)
            {
                Canvas.SetLeft(box, _tabBlurX);
                Canvas.SetTop(box, _tabBlurY);
                box.Width = _tabBlurW;
                box.Height = _tabBlurH;
            }
        }
        catch (Exception) { }
    }

    private void BlurCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (!_isTabBlurDragging || sender is not Canvas canvas) return;
            _isTabBlurDragging = false;

            var bar = FindChildByTag<StackPanel>(canvas, "InTabBlurActionBar");
            if (bar != null && _tabBlurW > 5 && _tabBlurH > 5)
            {
                Canvas.SetLeft(bar, _tabBlurX);
                Canvas.SetTop(bar, Math.Max(0, _tabBlurY + _tabBlurH + 8));
                bar.Visibility = Visibility.Visible;
            }
        }
        catch (Exception) { }
    }

    private void BtnApplyInTabBlur_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            if (tab?.ImageBytes is not { Length: > 0 } bytes || _tabBlurW <= 5 || _tabBlurH <= 5)
            {
                return;
            }

            var scroll = GetActiveScrollViewer();
            var blurBox = scroll != null ? FindChildByTag<Border>(scroll, "InTabBlurBox") : null;
            var blurCanvas = blurBox?.Parent as Canvas;
            double cW = blurCanvas?.ActualWidth > 0 ? blurCanvas.ActualWidth : (tab.ImageWidth > 0 ? tab.ImageWidth : _tabBlurW);
            double cH = blurCanvas?.ActualHeight > 0 ? blurCanvas.ActualHeight : (tab.ImageHeight > 0 ? tab.ImageHeight : _tabBlurH);
            double scaleX = cW > 0 && tab.ImageWidth > 0 ? (double)tab.ImageWidth / cW : 1.0;
            double scaleY = cH > 0 && tab.ImageHeight > 0 ? (double)tab.ImageHeight / cH : 1.0;

            int blurX = (int)Math.Round(_tabBlurX * scaleX);
            int blurY = (int)Math.Round(_tabBlurY * scaleY);
            int blurW = (int)Math.Round(_tabBlurW * scaleX);
            int blurH = (int)Math.Round(_tabBlurH * scaleY);

            var blurred = _screenCaptureService.BlurRegion(bytes, blurX, blurY, blurW, blurH);
            if (blurred is { Length: > 0 })
            {
                tab.ImageBytes = blurred;
                SetToolMode(ToolType.Hand);
                NotifyToolChange(_localizationService.GetString("Status_BlurCommitted"));
                CopyCurrentTabImage();
            }
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
    }

    private void BtnCancelInTabBlur_Click(object sender, RoutedEventArgs e)
    {
        SetToolMode(ToolType.Hand);
        NotifyToolChange(_localizationService.GetString("Status_HandActive"));
    }

    // ==========================================
    // 4.2 Hand / Pan Tool & Interactive Zoom Controls
    // ==========================================
    private void ToolHand_Click(object sender, RoutedEventArgs e)
    {
        SetToolMode(ToolType.Hand);
    }

    private ScrollViewer? GetActiveScrollViewer()
    {
        try
        {
            if (ViewModel.SelectedTab != null)
            {
                var container = MainTabView.ContainerFromItem(ViewModel.SelectedTab);
                if (container != null)
                {
                    var scroll = FindChildByTag<ScrollViewer>(container, "TabScrollViewer");
                    if (scroll != null) return scroll;
                }
            }
        }
        catch { }

        return FindChildByTag<ScrollViewer>(MainTabView, "TabScrollViewer");
    }

    private Canvas? GetActiveDrawingCanvas()
    {
        try
        {
            if (ViewModel.SelectedTab != null)
            {
                var container = MainTabView.ContainerFromItem(ViewModel.SelectedTab);
                if (container != null)
                {
                    var canvas = FindChildByTag<Canvas>(container, "InTabDrawingCanvas");
                    if (canvas != null) return canvas;
                }
            }
        }
        catch { }

        return _activeDrawingCanvas;
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e) => ZoomInCurrentTab();
    private void BtnZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOutCurrentTab();
    private void BtnZoomReset_Click(object sender, RoutedEventArgs e)
    {
        var tab = ViewModel.SelectedTab;
        if (tab == null) return;
        // Toggle between 100% and Auto-Fit for maximum user convenience
        if (Math.Abs(tab.ZoomFactor - 1.0) < 0.05)
        {
            AutoFitImageToViewport(tab);
        }
        else
        {
            ZoomResetCurrentTab();
        }
    }

    private void AutoFitImageToViewport(ShellTabModel? tab)
    {
        if (tab == null) return;

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            try
            {
                var scroll = GetActiveScrollViewer();

                // Compute real available viewport dimensions across all window states (Small, Medium, Large, Maximized)
                double viewW = 0;
                double viewH = 0;

                if (scroll != null && scroll.ViewportWidth > 50 && scroll.ViewportHeight > 50)
                {
                    viewW = scroll.ViewportWidth;
                    viewH = scroll.ViewportHeight;
                }
                else if (scroll != null && scroll.ActualWidth > 50 && scroll.ActualHeight > 50)
                {
                    viewW = scroll.ActualWidth;
                    viewH = scroll.ActualHeight;
                }
                else if (RootAppGrid != null && RootAppGrid.ActualWidth > 50 && RootAppGrid.ActualHeight > 50)
                {
                    viewW = RootAppGrid.ActualWidth;
                    viewH = RootAppGrid.ActualHeight - 120; // Top ribbon + Tab header + Bottom status bar
                }
                else if (this.AppWindow.Size.Width > 100 && this.AppWindow.Size.Height > 100)
                {
                    viewW = this.AppWindow.Size.Width - 32;
                    viewH = this.AppWindow.Size.Height - 130;
                }
                else
                {
                    viewW = 800;
                    viewH = 500;
                }

                // Available display area inside the tab with comfortable margin
                double availW = Math.Max(100, viewW - 24);
                double availH = Math.Max(100, viewH - 24);

                double imgW = tab.ImageWidth > 0 ? tab.ImageWidth : 1920;
                double imgH = tab.ImageHeight > 0 ? tab.ImageHeight : 1080;

                // If image is larger than available space, calculate exact fit ratio
                if (imgW > availW || imgH > availH)
                {
                    double ratioW = availW / imgW;
                    double ratioH = availH / imgH;
                    double fitRatio = Math.Min(ratioW, ratioH);

                    fitRatio = Math.Floor(fitRatio * 100.0) / 100.0;
                    fitRatio = Math.Clamp(fitRatio, 0.05, 1.0);

                    tab.ZoomFactor = fitRatio;
                    scroll?.ChangeView(null, null, (float)fitRatio);
                    NotifyToolChange(_localizationService.GetString("Status_AutoFit", (int)(fitRatio * 100)));
                }
                else
                {
                    tab.ZoomFactor = 1.0;
                    scroll?.ChangeView(null, null, 1.0f);
                }
            }
            catch (Exception)
            {
                // Protected Zero-Crash boundary
            }
        });
    }

    private void MainWindow_SizeChanged(object sender, Microsoft.UI.Xaml.WindowSizeChangedEventArgs args)
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            // Dynamically scale image when user resizes DropCut window (small, medium, large, restored, maximized)
            if (tab is { HasImage: true } && tab.ZoomFactor < 1.0)
            {
                AutoFitImageToViewport(tab);
            }
        }
        catch { }
    }

    private void ZoomInCurrentTab()
    {
        var tab = ViewModel.SelectedTab;
        if (tab == null) return;
        var newZoom = Math.Min(5.0, tab.ZoomFactor + 0.15);
        tab.ZoomFactor = newZoom;
        GetActiveScrollViewer()?.ChangeView(null, null, (float)newZoom);
        NotifyToolChange($"{_localizationService.GetString("Zoom_In")}: {tab.ZoomPercentageText}");
    }

    private void ZoomOutCurrentTab()
    {
        var tab = ViewModel.SelectedTab;
        if (tab == null) return;
        var newZoom = Math.Max(0.2, tab.ZoomFactor - 0.15);
        tab.ZoomFactor = newZoom;
        GetActiveScrollViewer()?.ChangeView(null, null, (float)newZoom);
        NotifyToolChange($"{_localizationService.GetString("Zoom_Out")}: {tab.ZoomPercentageText}");
    }

    private void ZoomResetCurrentTab()
    {
        var tab = ViewModel.SelectedTab;
        if (tab == null) return;
        tab.ZoomFactor = 1.0;
        GetActiveScrollViewer()?.ChangeView(null, null, 1.0f);
        NotifyToolChange(_localizationService.GetString("Zoom_Reset"));
    }

    private static T? FindChildByTag<T>(DependencyObject parent, string tag) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T element && string.Equals(element.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }
            var deeper = FindChildByTag<T>(child, tag);
            if (deeper != null) return deeper;
        }
        return null;
    }

    // ==========================================
    // 5. Ribbon Tools: Pen, Highlighter, Eraser, Ruler, OCR & Mode Switching
    // ==========================================
    private void ToolPen_Click(SplitButton sender, SplitButtonClickEventArgs args)
    {
        if (ViewModel.ActiveTool == ToolType.Pen)
        {
            BtnCommitAndSaveEditing_Click(sender, new RoutedEventArgs());
        }
        else
        {
            SetToolMode(ToolType.Pen);
        }
    }

    private void PenColor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement elem && elem.Tag is string hex)
            {
                ViewModel.PenColor = hex;
                var brush = new SolidColorBrush(ParseColorHex(hex));
                if (IconPenGlyph != null) IconPenGlyph.Foreground = brush;
                if (IndicatorPenColor != null) IndicatorPenColor.Background = brush;
                SetToolMode(ToolType.Pen);
                NotifyToolChange($"{_localizationService.GetString("Tool_Pen")}: {hex}");
            }
        }
        catch (Exception) { }
    }

    private void PenSize_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement elem && int.TryParse(elem.Tag?.ToString(), out var size))
            {
                _penThickness = size;
                SetToolMode(ToolType.Pen);
                NotifyToolChange($"{_localizationService.GetString("Tool_Pen")}: {size}px");
            }
        }
        catch (Exception) { }
    }

    private void ToolHighlighter_Click(SplitButton sender, SplitButtonClickEventArgs args)
    {
        if (ViewModel.ActiveTool == ToolType.Highlighter)
        {
            BtnCommitAndSaveEditing_Click(sender, new RoutedEventArgs());
        }
        else
        {
            SetToolMode(ToolType.Highlighter);
        }
    }

    private void HighlighterColor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement elem && elem.Tag is string hex)
            {
                _highlighterColor = hex;
                var brush = new SolidColorBrush(ParseColorHex(hex));
                if (IconHighlighterGlyph != null) IconHighlighterGlyph.Foreground = brush;
                if (IndicatorHighlighterColor != null) IndicatorHighlighterColor.Background = brush;
                SetToolMode(ToolType.Highlighter);
                NotifyToolChange($"{_localizationService.GetString("Tool_Highlighter")}: {hex}");
            }
        }
        catch (Exception) { }
    }

    private void HighlighterSize_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement elem && int.TryParse(elem.Tag?.ToString(), out var size))
            {
                _highlighterThickness = size;
                SetToolMode(ToolType.Highlighter);
                NotifyToolChange($"{_localizationService.GetString("Tool_Highlighter")}: {size}px");
            }
        }
        catch (Exception) { }
    }

    // ==========================================
    // 5. Version 2.0: Geometric Shapes & Vector Tool Handlers (P08-T003)
    // ==========================================
    private void ToolShape_Click(SplitButton sender, SplitButtonClickEventArgs args)
    {
        if (ViewModel.ActiveTool == ToolType.Shape)
            SetToolMode(ToolType.Hand);
        else
            SetToolMode(ToolType.Shape);
    }

    private void ShapeSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && Enum.TryParse<ShapeType>(item.Tag?.ToString(), out var shapeType))
        {
            _currentShapeType = shapeType;
            SetToolMode(ToolType.Shape);
            NotifyToolChange($"{_localizationService.GetString("Tool_Shape")}: {_localizationService.GetString($"Shape_{shapeType}")}");
        }
    }

    private void ShapeColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string hex)
        {
            _shapeColor = hex;
            var color = ParseColorHex(hex);
            if (IndicatorShapeColor != null) IndicatorShapeColor.Background = new SolidColorBrush(color);
            if (IconShapeGlyph != null) IconShapeGlyph.Foreground = new SolidColorBrush(color);
            SetToolMode(ToolType.Shape);
        }
    }

    private void ShapeSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && int.TryParse(item.Tag?.ToString(), out var thickness))
        {
            _shapeThickness = thickness;
            SetToolMode(ToolType.Shape);
        }
    }

    private void ShapeFill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem toggle)
        {
            _shapeFill = toggle.IsChecked;
            SetToolMode(ToolType.Shape);
        }
    }

    // ==========================================
    // 6. Version 2.0: Sequential Numbered Step Badges Handlers (P08-T004)
    // ==========================================
    private void ToolBadge_Click(SplitButton sender, SplitButtonClickEventArgs args)
    {
        if (ViewModel.ActiveTool == ToolType.Badge)
            SetToolMode(ToolType.Hand);
        else
            SetToolMode(ToolType.Badge);
    }

    private void BadgeColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string hex)
        {
            _badgeColor = hex;
            var color = ParseColorHex(hex);
            if (IndicatorBadgeColor != null) IndicatorBadgeColor.Background = new SolidColorBrush(color);
            if (IconBadgeGlyph != null) IconBadgeGlyph.Foreground = new SolidColorBrush(color);
            SetToolMode(ToolType.Badge);
        }
    }

    private void BadgeSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && int.TryParse(item.Tag?.ToString(), out var size))
        {
            _badgeSize = size;
            SetToolMode(ToolType.Badge);
        }
    }

    private void BadgeReset_Click(object sender, RoutedEventArgs e)
    {
        _nextBadgeNumber = 1;
        NotifyToolChange(_localizationService.GetString("Badge_Reset"));
    }

    // ==========================================
    // 7. Version 2.0: Native Windows Print Pipeline (P08-T002)
    // ==========================================
    private async void ActionPrint_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            if (tab?.ImageBytes is not { Length: > 0 } bytes)
            {
                NotifyToolChange(_localizationService.GetString("Action_NewSnip_Tip"));
                return;
            }

            // 1. Bake any active drawing canvas annotations before printing
            if (_activeDrawingCanvas != null && _activeDrawingCanvas.Children.Count > 0)
            {
                BtnCommitAndSaveEditing_Click(sender, e);
                bytes = tab.ImageBytes!;
            }

            NotifyToolChange(_localizationService.GetString("Status_Printing"));

            // 2. Write temp image file for native Windows printing wizard
            var tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DropCut");
            System.IO.Directory.CreateDirectory(tempFolder);
            var safeTitle = string.Join("_", tab.Title.Split(System.IO.Path.GetInvalidFileNameChars()));
            var printPath = System.IO.Path.Combine(tempFolder, $"Print_{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            await System.IO.File.WriteAllBytesAsync(printPath, bytes);

            // 3. Launch native Windows Print Picture dialog (supports printer selection, A4/Letter, PDF export, orientation)
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = printPath,
                    Verb = "print",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception)
            {
                // Fallback to photo viewer print verb
                var fallbackPsi = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"\"%ProgramFiles%\\Windows Photo Viewer\\PhotoViewer.dll\",ImageView_PrintTo /pt \"{printPath}\"",
                    UseShellExecute = true
                };
                Process.Start(fallbackPsi);
            }
        }
        catch (Exception ex)
        {
            NotifyToolChange($"Print error: {ex.Message}");
        }
    }

    private void ToolEraser_Click(object sender, RoutedEventArgs e)
    {
        var canvas = GetActiveDrawingCanvas() ?? _activeDrawingCanvas;
        if (canvas != null && canvas.Children.Count > 0)
        {
            canvas.Children.Clear();
            _redoStack.Clear();
            NotifyToolChange(_localizationService.GetString("Status_AllCleared"));
        }
        SetToolMode(ToolType.Hand);
    }

    private void ToolRuler_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ActiveTool == ToolType.Shape)
        {
            SetToolMode(ToolType.Hand);
        }
        else
        {
            _currentShapeType = ShapeType.Line;
            SetToolMode(ToolType.Shape);
        }
    }

    private void MenuCopy_Click(object sender, RoutedEventArgs e) => CopyCurrentTabImage();
    private void MenuSave_Click(object sender, RoutedEventArgs e) => SaveCurrentTabImage();
    private void BtnRevertToHand_Click(object sender, RoutedEventArgs e) => SetToolMode(ToolType.Hand);

    private void SetToolMode(ToolType tool)
    {
        try
        {
            ViewModel.ActiveTool = tool;
            var tab = ViewModel.SelectedTab;

            if (tool == ToolType.Hand)
            {
                if (tab != null)
                {
                    tab.IsCropping = false;
                    tab.IsBlurring = false;
                    tab.IsDrawingMode = false;
                }
                NotifyToolChange(_localizationService.GetString("Status_HandActive"));
            }
            else if (tool == ToolType.Crop)
            {
                if (tab != null)
                {
                    tab.IsCropping = true;
                    tab.IsBlurring = false;
                    tab.IsDrawingMode = false;
                }
                NotifyToolChange(_localizationService.GetString("Status_CropActive"));
            }
            else if (tool == ToolType.BlurRedact)
            {
                if (tab != null)
                {
                    tab.IsCropping = false;
                    tab.IsBlurring = true;
                    tab.IsDrawingMode = false;
                }
                NotifyToolChange(_localizationService.GetString("Status_BlurActive"));
            }
            else if (tool == ToolType.Shape)
            {
                if (tab != null)
                {
                    tab.IsCropping = false;
                    tab.IsBlurring = false;
                    tab.IsDrawingMode = true;
                }
                NotifyToolChange(_localizationService.GetString("Tool_Shape_Tip"));
            }
            else if (tool == ToolType.Badge)
            {
                if (tab != null)
                {
                    tab.IsCropping = false;
                    tab.IsBlurring = false;
                    tab.IsDrawingMode = true;
                }
                NotifyToolChange(_localizationService.GetString("Tool_Badge_Tip"));
            }
            else
            {
                if (tab != null)
                {
                    tab.IsCropping = false;
                    tab.IsBlurring = false;
                    tab.IsDrawingMode = true;
                }
                NotifyToolChange(tool == ToolType.Highlighter
                    ? _localizationService.GetString("Status_HighlighterActive")
                    : _localizationService.GetString("Status_PenActive"));
            }

            UpdateActiveToolUI(tool);
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
    }

    private void UpdateActiveToolUI(ToolType tool)
    {
        try
        {
            // 1. Reset all button styles to clean default
            ResetButtonStyle(BtnHandTool);
            ResetButtonStyle(BtnPenTool);
            ResetButtonStyle(BtnHighlighterTool);
            ResetButtonStyle(BtnShapeTool);
            ResetButtonStyle(BtnBadgeTool);
            ResetButtonStyle(BtnEraserTool);
            ResetButtonStyle(BtnBlurTool);
            ResetButtonStyle(BtnRulerTool);
            ResetButtonStyle(BtnTextTool);
            ResetButtonStyle(BtnCropTool);

            // Re-apply chosen tool color badges
            if (IconPenGlyph != null) IconPenGlyph.Foreground = new SolidColorBrush(ParseColorHex(ViewModel.PenColor));
            if (IndicatorPenColor != null) IndicatorPenColor.Background = new SolidColorBrush(ParseColorHex(ViewModel.PenColor));
            if (IconHighlighterGlyph != null) IconHighlighterGlyph.Foreground = new SolidColorBrush(ParseColorHex(_highlighterColor));
            if (IndicatorHighlighterColor != null) IndicatorHighlighterColor.Background = new SolidColorBrush(ParseColorHex(_highlighterColor));
            if (IconShapeGlyph != null) IconShapeGlyph.Foreground = new SolidColorBrush(ParseColorHex(_shapeColor));
            if (IndicatorShapeColor != null) IndicatorShapeColor.Background = new SolidColorBrush(ParseColorHex(_shapeColor));
            if (IconBadgeGlyph != null) IconBadgeGlyph.Foreground = new SolidColorBrush(ParseColorHex(_badgeColor));
            if (IndicatorBadgeColor != null) IndicatorBadgeColor.Background = new SolidColorBrush(ParseColorHex(_badgeColor));

            // 2. Apply vibrant active styling (Golden Yellow for editing tools, Fluent Blue for Hand tool)
            switch (tool)
            {
                case ToolType.Hand:
                    ApplyActiveBlueStyle(BtnHandTool);
                    HideModeGuidance();
                    SetCanvasCursor(InputSystemCursorShape.Hand);
                    break;

                case ToolType.Pen:
                    ApplyActiveYellowStyle(BtnPenTool);
                    ShowModeGuidance("\uED63", _localizationService.GetString("Action_ModeGuidance"));
                    SetCanvasCursor(InputSystemCursorShape.Cross);
                    break;

                case ToolType.Highlighter:
                    ApplyActiveYellowStyle(BtnHighlighterTool);
                    ShowModeGuidance("\uE7E6", _localizationService.GetString("Action_ModeGuidance"));
                    SetCanvasCursor(InputSystemCursorShape.Cross);
                    break;

                case ToolType.Shape:
                    ApplyActiveYellowStyle(BtnShapeTool);
                    ShowModeGuidance("\uE73B", _localizationService.GetString("Action_ModeGuidance"));
                    SetCanvasCursor(InputSystemCursorShape.Cross);
                    break;

                case ToolType.Badge:
                    ApplyActiveYellowStyle(BtnBadgeTool);
                    ShowModeGuidance("\uE735", _localizationService.GetString("Action_ModeGuidance"));
                    SetCanvasCursor(InputSystemCursorShape.Hand);
                    break;

                case ToolType.BlurRedact:
                    ApplyActiveYellowStyle(BtnBlurTool);
                    ShowModeGuidance("\uE790", _localizationService.GetString("Action_ModeGuidance"));
                    SetCanvasCursor(InputSystemCursorShape.Cross);
                    break;

                case ToolType.Crop:
                    ApplyActiveYellowStyle(BtnCropTool);
                    ShowModeGuidance("\uE7A8", _localizationService.GetString("Action_ModeGuidance"));
                    SetCanvasCursor(InputSystemCursorShape.Cross);
                    break;

                case ToolType.Select:
                    ApplyActiveYellowStyle(BtnEraserTool);
                    ShowModeGuidance("\uE75C", _localizationService.GetString("Action_ModeGuidance"));
                    SetCanvasCursor(InputSystemCursorShape.Arrow);
                    break;

                default:
                    ApplyActiveBlueStyle(BtnHandTool);
                    HideModeGuidance();
                    SetCanvasCursor(InputSystemCursorShape.Hand);
                    break;
            }
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
    }

    private static void ApplyActiveYellowStyle(Control? button)
    {
        if (button == null) return;
        button.Background = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)); // #FFD700 Vibrant Gold Yellow
        button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
        button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 184, 134, 11)); // #B8860B
        button.BorderThickness = new Thickness(2);
        button.FontWeight = Microsoft.UI.Text.FontWeights.Bold;

        SetInnerElementsForeground(button, Microsoft.UI.Colors.Black);
    }

    private static void ApplyActiveBlueStyle(Control? button)
    {
        if (button == null) return;
        button.Background = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)); // #0078D7 Fluent Blue
        button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0, 90, 170));
        button.BorderThickness = new Thickness(2);
        button.FontWeight = Microsoft.UI.Text.FontWeights.Bold;

        SetInnerElementsForeground(button, Microsoft.UI.Colors.White);
    }

    private static void ResetButtonStyle(Control? button)
    {
        if (button == null) return;
        button.ClearValue(Control.BackgroundProperty);
        button.ClearValue(Control.ForegroundProperty);
        button.ClearValue(Control.BorderBrushProperty);
        button.ClearValue(Control.BorderThicknessProperty);
        button.ClearValue(Control.FontWeightProperty);
        button.Style = null;

        ResetInnerElementsForeground(button);
    }

    private static void SetInnerElementsForeground(DependencyObject parent, Color color)
    {
        var brush = new SolidColorBrush(color);
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FontIcon fi) fi.Foreground = brush;
            if (child is TextBlock tb) tb.Foreground = brush;
            SetInnerElementsForeground(child, color);
        }
    }

    private static void ResetInnerElementsForeground(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FontIcon fi) fi.ClearValue(FontIcon.ForegroundProperty);
            if (child is TextBlock tb) tb.ClearValue(TextBlock.ForegroundProperty);
            ResetInnerElementsForeground(child);
        }
    }

    private void BtnCommitAndSaveEditing_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            if (tab == null)
            {
                SetToolMode(ToolType.Hand);
                return;
            }

            // 1. If currently in Crop mode -> Commit crop!
            if (ViewModel.ActiveTool == ToolType.Crop)
            {
                BtnApplyInTabCrop_Click(sender, e);
                return;
            }

            // 2. If currently in Privacy Blur mode -> Commit blur!
            if (ViewModel.ActiveTool == ToolType.BlurRedact)
            {
                BtnApplyInTabBlur_Click(sender, e);
                return;
            }

            // 3. If currently in Inking / Shapes / Badges mode -> Rasterize annotations!
            var activeCanvas = GetActiveDrawingCanvas() ?? _activeDrawingCanvas;
            if (tab.ImageBytes is { Length: > 0 } bytes && activeCanvas != null && activeCanvas.Children.Count > 0)
            {
                var strokes = new List<StrokeRenderData>();
                var badges = new List<BadgeRenderData>();

                double cW = activeCanvas.ActualWidth > 0 ? activeCanvas.ActualWidth : (tab.ImageWidth > 0 ? tab.ImageWidth : 1);
                double cH = activeCanvas.ActualHeight > 0 ? activeCanvas.ActualHeight : (tab.ImageHeight > 0 ? tab.ImageHeight : 1);
                double scaleX = cW > 0 && tab.ImageWidth > 0 ? (double)tab.ImageWidth / cW : 1.0;
                double scaleY = cH > 0 && tab.ImageHeight > 0 ? (double)tab.ImageHeight / cH : 1.0;

                foreach (var child in activeCanvas.Children)
                {
                    if (child is Polyline poly && poly.Points.Count >= 2)
                    {
                        var pts = poly.Points.Select(p => (p.X * scaleX, p.Y * scaleY)).ToList();
                        var hex = (poly.Stroke as SolidColorBrush)?.Color.ToString() ?? "#FF0000";
                        var thickness = (int)Math.Round(poly.StrokeThickness * scaleX);
                        bool isHighlight = poly.Opacity < 0.9 || poly.StrokeThickness >= 10;
                        strokes.Add(new StrokeRenderData(pts, hex, Math.Max(1, thickness), isHighlight));
                    }
                    else if (child is Microsoft.UI.Xaml.Shapes.Rectangle rect)
                    {
                        double l = Canvas.GetLeft(rect) * scaleX;
                        double t = Canvas.GetTop(rect) * scaleY;
                        double w = (rect.Width > 0 ? rect.Width : 20) * scaleX;
                        double h = (rect.Height > 0 ? rect.Height : 20) * scaleY;
                        var rectPts = new List<(double X, double Y)>
                        {
                            (l, t), (l + w, t), (l + w, t + h), (l, t + h), (l, t)
                        };
                        var hex = (rect.Stroke as SolidColorBrush)?.Color.ToString() ?? "#FF0000";
                        var thick = (int)Math.Round(rect.StrokeThickness * scaleX);
                        strokes.Add(new StrokeRenderData(rectPts, hex, Math.Max(1, thick), false));
                    }
                    else if (child is Microsoft.UI.Xaml.Shapes.Line line)
                    {
                        var linePts = new List<(double X, double Y)>
                        {
                            (line.X1 * scaleX, line.Y1 * scaleY),
                            (line.X2 * scaleX, line.Y2 * scaleY)
                        };
                        var hex = (line.Stroke as SolidColorBrush)?.Color.ToString() ?? "#FF0000";
                        var thick = (int)Math.Round(line.StrokeThickness * scaleX);
                        strokes.Add(new StrokeRenderData(linePts, hex, Math.Max(1, thick), false));
                    }
                    else if (child is Microsoft.UI.Xaml.Shapes.Ellipse ell)
                    {
                        double cx = (Canvas.GetLeft(ell) + ell.Width / 2.0) * scaleX;
                        double cy = (Canvas.GetTop(ell) + ell.Height / 2.0) * scaleY;
                        double rx = (ell.Width / 2.0) * scaleX;
                        double ry = (ell.Height / 2.0) * scaleY;
                        var ellPts = new List<(double X, double Y)>();
                        for (int deg = 0; deg <= 360; deg += 10)
                        {
                            double rad = deg * Math.PI / 180.0;
                            ellPts.Add((cx + rx * Math.Cos(rad), cy + ry * Math.Sin(rad)));
                        }
                        var hex = (ell.Stroke as SolidColorBrush)?.Color.ToString() ?? "#FF0000";
                        var thick = (int)Math.Round(ell.StrokeThickness * scaleX);
                        strokes.Add(new StrokeRenderData(ellPts, hex, Math.Max(1, thick), false));
                    }
                    else if (child is Grid badgeGrid && badgeGrid.Tag is string tag && tag.StartsWith("Badge_"))
                    {
                        if (int.TryParse(tag.Substring(6), out var num))
                        {
                            double bx = (Canvas.GetLeft(badgeGrid) + badgeGrid.Width / 2.0) * scaleX;
                            double by = (Canvas.GetTop(badgeGrid) + badgeGrid.Height / 2.0) * scaleY;
                            var ellChild = badgeGrid.Children.OfType<Microsoft.UI.Xaml.Shapes.Ellipse>().FirstOrDefault();
                            var bgHex = (ellChild?.Fill as SolidColorBrush)?.Color.ToString() ?? "#0078D7";
                            var diam = (int)Math.Round(badgeGrid.Width * scaleX);
                            badges.Add(new BadgeRenderData(bx, by, num, bgHex, Math.Max(16, diam)));
                        }
                    }
                }

                if (strokes.Count > 0 || badges.Count > 0)
                {
                    var baked = _screenCaptureService.RenderAnnotations(bytes, strokes, badges);
                    if (baked != null && baked.Length > 0)
                    {
                        tab.ImageBytes = baked;
                        activeCanvas.Children.Clear();
                        _redoStack.Clear();
                        CopyCurrentTabImage();
                        NotifyToolChange(_localizationService.GetString("Status_Saved"));
                    }
                }
            }

            SetToolMode(ToolType.Hand);
        }
        catch (Exception)
        {
            SetToolMode(ToolType.Hand);
        }
    }

    private void ShowModeGuidance(string glyph, string message)
    {
        if (ModeGuidancePill != null)
        {
            IconModeGuidance.Glyph = glyph;
            TxtModeGuidance.Text = message;
            ModeGuidancePill.Visibility = Visibility.Visible;
        }
    }

    private void HideModeGuidance()
    {
        if (ModeGuidancePill != null)
        {
            ModeGuidancePill.Visibility = Visibility.Collapsed;
        }
    }

    private void SetCanvasCursor(InputSystemCursorShape shape)
    {
        try
        {
            var cursor = InputSystemCursor.Create(shape);
            var prop = typeof(UIElement).GetProperty(
                "ProtectedCursor",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            prop?.SetValue(MainTabView, cursor);
        }
        catch (Exception) { }
    }

    private async void ToolText_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tab = ViewModel.SelectedTab;
            if (tab?.ImageBytes is not { Length: > 0 } bytes)
            {
                NotifyToolChange(_localizationService.GetString("Action_NewSnip_Tip"));
                return;
            }

            // Flash active yellow style while extracting
            ApplyActiveYellowStyle(BtnTextTool);
            NotifyToolChange(_localizationService.GetString("Tool_Text"));

            var result = await _ocrService.ExtractTextAsync(bytes);

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
            {
                var dp = new DataPackage();
                dp.SetText(result.Value);
                Clipboard.SetContent(dp);
                tab.AutoSaveStatus = _localizationService.GetString("Status_Copied");
            }
            else
            {
                tab.AutoSaveStatus = _localizationService.GetString("Status_Ready");
            }

            // Automatically turn off yellow style and return to previous state
            await Task.Delay(400);
            ResetButtonStyle(BtnTextTool);
            SetToolMode(ToolType.Hand);
        }
        catch (Exception ex)
        {
            ResetButtonStyle(BtnTextTool);
            NotifyToolChange(_localizationService.GetString("Status_TextExtractionFailed", ex.Message));
        }
    }

    private void NotifyToolChange(string message)
    {
        if (ViewModel.SelectedTab is not null)
        {
            ViewModel.SelectedTab.AutoSaveStatus = message;
        }
    }

    // ==========================================
    // 6. In-Canvas Interactive Drawing Handlers
    // ==========================================
    private void AnnotationCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (ViewModel.SelectedTab?.IsCropping == true || ViewModel.SelectedTab?.IsBlurring == true || ViewModel.ActiveTool == ToolType.Hand) return;
            if (sender is not Canvas canvas) return;
            _activeDrawingCanvas = canvas;
            try { canvas.CapturePointer(e.Pointer); } catch { }

            var pt = e.GetCurrentPoint(canvas).Position;

            if (ViewModel.ActiveTool == ToolType.Pen)
            {
                var color = ParseColorHex(ViewModel.PenColor);
                var thickness = _penThickness;

                _activePolyline = new Polyline
                {
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = thickness,
                    Opacity = 1.0,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round
                };
                _activePolyline.Points.Add(pt);
                canvas.Children.Add(_activePolyline);
            }
            else if (ViewModel.ActiveTool == ToolType.Highlighter)
            {
                var color = ParseColorHex(_highlighterColor);
                var thickness = _highlighterThickness;

                _activePolyline = new Polyline
                {
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = thickness,
                    Opacity = 0.45, // Semi-transparent highlighter so text underneath is clearly visible
                    StrokeStartLineCap = PenLineCap.Flat,
                    StrokeEndLineCap = PenLineCap.Flat,
                    StrokeLineJoin = PenLineJoin.Round
                };
                _activePolyline.Points.Add(pt);
                canvas.Children.Add(_activePolyline);
            }
            else if (ViewModel.ActiveTool == ToolType.Shape)
            {
                _redoStack.Clear();
                _shapeStartPoint = pt;
                _isDrawingShape = true;
                var color = ParseColorHex(_shapeColor);
                var strokeBrush = new SolidColorBrush(color);
                var fillBrush = _shapeFill
                    ? new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B))
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

                switch (_currentShapeType)
                {
                    case ShapeType.Rectangle:
                        var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
                        {
                            Stroke = strokeBrush,
                            StrokeThickness = _shapeThickness,
                            Fill = fillBrush,
                            Tag = "Shape_Rectangle"
                        };
                        Canvas.SetLeft(rect, pt.X);
                        Canvas.SetTop(rect, pt.Y);
                        canvas.Children.Add(rect);
                        _activeShapeElement = rect;
                        break;

                    case ShapeType.RoundedRectangle:
                        var rrect = new Microsoft.UI.Xaml.Shapes.Rectangle
                        {
                            Stroke = strokeBrush,
                            StrokeThickness = _shapeThickness,
                            RadiusX = 12,
                            RadiusY = 12,
                            Fill = fillBrush,
                            Tag = "Shape_RoundedRectangle"
                        };
                        Canvas.SetLeft(rrect, pt.X);
                        Canvas.SetTop(rrect, pt.Y);
                        canvas.Children.Add(rrect);
                        _activeShapeElement = rrect;
                        break;

                    case ShapeType.Ellipse:
                        var ell = new Microsoft.UI.Xaml.Shapes.Ellipse
                        {
                            Stroke = strokeBrush,
                            StrokeThickness = _shapeThickness,
                            Fill = fillBrush,
                            Tag = "Shape_Ellipse"
                        };
                        Canvas.SetLeft(ell, pt.X);
                        Canvas.SetTop(ell, pt.Y);
                        canvas.Children.Add(ell);
                        _activeShapeElement = ell;
                        break;

                    case ShapeType.Line:
                        var line = new Microsoft.UI.Xaml.Shapes.Line
                        {
                            X1 = pt.X,
                            Y1 = pt.Y,
                            X2 = pt.X,
                            Y2 = pt.Y,
                            Stroke = strokeBrush,
                            StrokeThickness = _shapeThickness,
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round,
                            Tag = "Shape_Line"
                        };
                        canvas.Children.Add(line);
                        _activeShapeElement = line;
                        break;

                    case ShapeType.Arrow:
                        var arrow = new Polyline
                        {
                            Stroke = strokeBrush,
                            StrokeThickness = _shapeThickness,
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round,
                            StrokeLineJoin = PenLineJoin.Round,
                            Tag = "Shape_Arrow"
                        };
                        arrow.Points.Add(pt);
                        arrow.Points.Add(pt);
                        canvas.Children.Add(arrow);
                        _activeShapeElement = arrow;
                        break;

                    case ShapeType.Callout:
                        var callout = new Microsoft.UI.Xaml.Shapes.Rectangle
                        {
                            Stroke = strokeBrush,
                            StrokeThickness = _shapeThickness,
                            RadiusX = 16,
                            RadiusY = 16,
                            Fill = fillBrush,
                            Tag = "Shape_Callout"
                        };
                        Canvas.SetLeft(callout, pt.X);
                        Canvas.SetTop(callout, pt.Y);
                        canvas.Children.Add(callout);
                        _activeShapeElement = callout;
                        break;
                }
            }
            else if (ViewModel.ActiveTool == ToolType.Badge)
            {
                _redoStack.Clear();
                int num = _nextBadgeNumber++;
                int diameter = _badgeSize;
                var bColor = ParseColorHex(_badgeColor);

                var grid = new Grid
                {
                    Width = diameter,
                    Height = diameter,
                    Tag = $"Badge_{num}"
                };

                var ellipse = new Microsoft.UI.Xaml.Shapes.Ellipse
                {
                    Fill = new SolidColorBrush(bColor),
                    Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
                    StrokeThickness = 2
                };

                var tb = new TextBlock
                {
                    Text = num.ToString(),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    FontSize = Math.Max(10, diameter * 0.45),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                grid.Children.Add(ellipse);
                grid.Children.Add(tb);
                Canvas.SetLeft(grid, pt.X - diameter / 2.0);
                Canvas.SetTop(grid, pt.Y - diameter / 2.0);
                canvas.Children.Add(grid);
                NotifyToolChange(_localizationService.GetString("Status_BadgeStamped", num));
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
            if (sender is not Canvas canvas || ViewModel.SelectedTab?.IsCropping == true) return;
            var pt = e.GetCurrentPoint(canvas).Position;

            if (_activePolyline != null)
            {
                _activePolyline.Points.Add(pt);
            }
            else if (_isDrawingShape && _activeShapeElement != null)
            {
                double x = Math.Min(_shapeStartPoint.X, pt.X);
                double y = Math.Min(_shapeStartPoint.Y, pt.Y);
                double w = Math.Max(2, Math.Abs(pt.X - _shapeStartPoint.X));
                double h = Math.Max(2, Math.Abs(pt.Y - _shapeStartPoint.Y));

                if (_activeShapeElement is Microsoft.UI.Xaml.Shapes.Rectangle || _activeShapeElement is Microsoft.UI.Xaml.Shapes.Ellipse)
                {
                    _activeShapeElement.Width = w;
                    _activeShapeElement.Height = h;
                    Canvas.SetLeft(_activeShapeElement, x);
                    Canvas.SetTop(_activeShapeElement, y);
                }
                else if (_activeShapeElement is Microsoft.UI.Xaml.Shapes.Line line)
                {
                    line.X1 = _shapeStartPoint.X;
                    line.Y1 = _shapeStartPoint.Y;
                    line.X2 = pt.X;
                    line.Y2 = pt.Y;
                }
                else if (_activeShapeElement is Polyline arrow && string.Equals(arrow.Tag?.ToString(), "Shape_Arrow", StringComparison.OrdinalIgnoreCase))
                {
                    double dx = pt.X - _shapeStartPoint.X;
                    double dy = pt.Y - _shapeStartPoint.Y;
                    double angle = Math.Atan2(dy, dx);
                    double barbLen = Math.Max(12, _shapeThickness * 3.5);
                    double barbAngle = Math.PI / 6.0;

                    double xA = pt.X - barbLen * Math.Cos(angle - barbAngle);
                    double yA = pt.Y - barbLen * Math.Sin(angle - barbAngle);
                    double xB = pt.X - barbLen * Math.Cos(angle + barbAngle);
                    double yB = pt.Y - barbLen * Math.Sin(angle + barbAngle);

                    arrow.Points.Clear();
                    arrow.Points.Add(_shapeStartPoint);
                    arrow.Points.Add(pt);
                    arrow.Points.Add(new Point(xA, yA));
                    arrow.Points.Add(pt);
                    arrow.Points.Add(new Point(xB, yB));
                }
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
            if (_activeDrawingCanvas != null)
            {
                try { _activeDrawingCanvas.ReleasePointerCapture(e.Pointer); } catch { }
            }
            _activePolyline = null;
            _isDrawingShape = false;
            _activeShapeElement = null;
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
    // 7. Actions: Undo, Redo, Save, Copy, Share
    // ==========================================
    private void ActionUndo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var canvas = GetActiveDrawingCanvas() ?? _activeDrawingCanvas;
            if (canvas != null && canvas.Children.Count > 0)
            {
                var last = canvas.Children[canvas.Children.Count - 1];
                canvas.Children.RemoveAt(canvas.Children.Count - 1);
                _redoStack.Push(last);
                if (last is FrameworkElement fe && fe.Tag is string s && s.StartsWith("Badge_"))
                {
                    if (_nextBadgeNumber > 1) _nextBadgeNumber--;
                }
                NotifyToolChange(_localizationService.GetString("Action_Undo"));
            }
        }
        catch (Exception) { }
    }

    private void ActionRedo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var canvas = GetActiveDrawingCanvas() ?? _activeDrawingCanvas;
            if (_redoStack.Count > 0 && canvas != null)
            {
                var item = _redoStack.Pop();
                canvas.Children.Add(item);
                if (item is FrameworkElement fe && fe.Tag is string s && s.StartsWith("Badge_"))
                {
                    _nextBadgeNumber++;
                }
                NotifyToolChange(_localizationService.GetString("Action_Redo"));
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
        }
        catch (Exception) { }
    }

    private async void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var panel = new StackPanel { Spacing = 16, Width = 380 };

            // 1. Language selector
            var langLabel = new TextBlock { Text = _localizationService.GetString("Settings_Language"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var langCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            var languages = _localizationService.SupportedLanguages;
            foreach (var l in languages)
            {
                langCombo.Items.Add(new ComboBoxItem { Content = $"{l.NativeName} ({l.DisplayName})", Tag = l.Code });
            }
            langCombo.SelectedIndex = languages.ToList().FindIndex(l => l.Code == _localizationService.CurrentLanguageCode);
            if (langCombo.SelectedIndex < 0) langCombo.SelectedIndex = 0;

            // 2. Theme selector
            var themeLabel = new TextBlock { Text = _localizationService.GetString("Settings_Theme"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var themeCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            themeCombo.Items.Add(new ComboBoxItem { Content = _localizationService.GetString("Settings_Theme_Dark"), Tag = "Dark" });
            themeCombo.Items.Add(new ComboBoxItem { Content = _localizationService.GetString("Settings_Theme_Light"), Tag = "Light" });
            themeCombo.Items.Add(new ComboBoxItem { Content = _localizationService.GetString("Settings_Theme_Gray"), Tag = "Gray" });
            themeCombo.Items.Add(new ComboBoxItem { Content = _localizationService.GetString("Settings_Theme_System"), Tag = "Default" });
            themeCombo.SelectedIndex = 0;

            // 3. AutoSave toggle
            var autoSaveToggle = new ToggleSwitch
            {
                Header = _localizationService.GetString("Settings_AutoSave"),
                IsOn = true
            };

            // 4. Privacy & Licensing Card
            var privacyCard = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };

            var privacyStack = new StackPanel { Spacing = 6 };
            var privacyTitle = new TextBlock
            {
                Text = _localizationService.GetString("Settings_PrivacyLicenseButton"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13
            };
            var privacySummary = new TextBlock
            {
                Text = _localizationService.GetString("Privacy_OfflineFirstBody"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Opacity = 0.8
            };

            ContentDialog? settingsDialogRef = null;

            var privacyBtn = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE72E", FontSize = 12 },
                        new TextBlock { Text = _localizationService.GetString("Settings_PrivacyLicenseButton") }
                    }
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 4, 0, 0)
            };
            privacyBtn.Click += async (s, args) =>
            {
                try
                {
                    settingsDialogRef?.Hide();
                    await ShowPrivacyAndLicensingDialogAsync();
                }
                catch (Exception) { }
            };

            var openOnlineBtn = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE8A7", FontSize = 12 },
                        new TextBlock { Text = _localizationService.GetString("Privacy_OpenOnlineButton") }
                    }
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 2, 0, 0)
            };
            openOnlineBtn.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/abdullahali4ksa/drop_cut/blob/main/PRIVACY_POLICY.md",
                        UseShellExecute = true
                    });
                }
                catch { }
            };

            privacyStack.Children.Add(privacyTitle);
            privacyStack.Children.Add(privacySummary);
            privacyStack.Children.Add(privacyBtn);
            privacyStack.Children.Add(openOnlineBtn);
            privacyCard.Child = privacyStack;

            panel.Children.Add(langLabel);
            panel.Children.Add(langCombo);
            panel.Children.Add(themeLabel);
            panel.Children.Add(themeCombo);
            panel.Children.Add(autoSaveToggle);
            panel.Children.Add(privacyCard);

            var dialog = new ContentDialog
            {
                Title = _localizationService.GetString("Settings_Title"),
                Content = panel,
                PrimaryButtonText = _localizationService.GetString("Settings_SaveButton"),
                CloseButtonText = _localizationService.GetString("Tab_Close"),
                XamlRoot = this.Content.XamlRoot
            };
            settingsDialogRef = dialog;

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                if (langCombo.SelectedItem is ComboBoxItem item && item.Tag is string code)
                {
                    await _localizationService.SetLanguageAsync(code);
                }

                if (themeCombo.SelectedItem is ComboBoxItem tItem && tItem.Tag is string theme)
                {
                    if (this.Content is FrameworkElement rootElement)
                    {
                        if (theme == "Gray")
                        {
                            rootElement.RequestedTheme = ElementTheme.Dark;
                            if (RootAppGrid != null) RootAppGrid.Background = new SolidColorBrush(Color.FromArgb(255, 43, 45, 48)); // #2B2D30 Slate Gray
                        }
                        else
                        {
                            if (RootAppGrid != null) RootAppGrid.ClearValue(Grid.BackgroundProperty);
                            rootElement.RequestedTheme = theme switch
                            {
                                "Dark" => ElementTheme.Dark,
                                "Light" => ElementTheme.Light,
                                _ => ElementTheme.Default
                            };
                        }
                    }
                }

                ApplyLocalization();
                NotifyToolChange(_localizationService.GetString("Settings_SavedSuccess"));
            }
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
    }

    private async Task ShowPrivacyAndLicensingDialogAsync()
    {
        try
        {
            if (this.Content?.XamlRoot == null)
            {
                return;
            }

            var contentStack = new StackPanel { Spacing = 12, MaxWidth = 440 };

            // 1. 100% Offline-First
            contentStack.Children.Add(new TextBlock
            {
                Text = _localizationService.GetString("Privacy_OfflineFirstHeading"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 13
            });
            contentStack.Children.Add(new TextBlock
            {
                Text = _localizationService.GetString("Privacy_OfflineFirstBody"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.85
            });

            // 2. EULA
            contentStack.Children.Add(new TextBlock
            {
                Text = _localizationService.GetString("Privacy_EulaHeading"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 13
            });
            contentStack.Children.Add(new TextBlock
            {
                Text = _localizationService.GetString("Privacy_EulaBody"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.85
            });

            // 3. MIT
            contentStack.Children.Add(new TextBlock
            {
                Text = _localizationService.GetString("Privacy_MitHeading"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 13
            });
            contentStack.Children.Add(new TextBlock
            {
                Text = _localizationService.GetString("Privacy_MitBody"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.85
            });

            var onlineBtn = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE8A7", FontSize = 12 },
                        new TextBlock { Text = _localizationService.GetString("Privacy_OpenOnlineButton") }
                    }
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 0)
            };
            onlineBtn.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/abdullahali4ksa/drop_cut/blob/main/PRIVACY_POLICY.md",
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            contentStack.Children.Add(onlineBtn);

            var dialog = new ContentDialog
            {
                Title = _localizationService.GetString("Privacy_DialogTitle"),
                Content = new ScrollViewer { Content = contentStack, MaxHeight = 400 },
                CloseButtonText = _localizationService.GetString("Tab_Close"),
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
    }

    private void ApplyLocalization()
    {
        try
        {
            // 1. ToolTips across all 10 languages
            ToolTipService.SetToolTip(BtnNewSnip, _localizationService.GetString("Action_NewSnip_Tip"));
            ToolTipService.SetToolTip(BtnDelayTool, _localizationService.GetString("Action_Delay_Tip"));
            ToolTipService.SetToolTip(BadgeCameraMode, _localizationService.GetString("Action_SnipMode_Tip"));

            ToolTipService.SetToolTip(BtnHandTool, _localizationService.GetString("Tool_Hand_Tip"));
            ToolTipService.SetToolTip(BtnPenTool, _localizationService.GetString("Tool_Pen_Tip"));
            ToolTipService.SetToolTip(BtnHighlighterTool, _localizationService.GetString("Tool_Highlighter_Tip"));
            if (BtnShapeTool != null) ToolTipService.SetToolTip(BtnShapeTool, _localizationService.GetString("Tool_Shape_Tip"));
            if (BtnBadgeTool != null) ToolTipService.SetToolTip(BtnBadgeTool, _localizationService.GetString("Tool_Badge_Tip"));
            ToolTipService.SetToolTip(BtnEraserTool, _localizationService.GetString("Tool_Eraser_Tip"));
            ToolTipService.SetToolTip(BtnBlurTool, _localizationService.GetString("Tool_Blur_Tip"));
            ToolTipService.SetToolTip(BtnRulerTool, _localizationService.GetString("Tool_Ruler_Tip"));
            ToolTipService.SetToolTip(BtnTextTool, _localizationService.GetString("Tool_Text_Tip"));
            ToolTipService.SetToolTip(BtnCropTool, _localizationService.GetString("Tool_Crop_Tip"));

            ToolTipService.SetToolTip(BtnUndo, _localizationService.GetString("Action_Undo_Tip"));
            ToolTipService.SetToolTip(BtnRedo, _localizationService.GetString("Action_Redo_Tip"));
            ToolTipService.SetToolTip(BtnSave, _localizationService.GetString("Action_Save_Tip"));
            ToolTipService.SetToolTip(BtnCopy, _localizationService.GetString("Action_Copy_Tip"));
            if (BtnPrint != null) ToolTipService.SetToolTip(BtnPrint, _localizationService.GetString("Action_Print_Tip"));
            ToolTipService.SetToolTip(BtnShare, _localizationService.GetString("Action_Share_Tip"));
            ToolTipService.SetToolTip(BtnMoreOptions, _localizationService.GetString("Action_Settings_Tip"));

            // 2. Menu Flyout Items
            if (MenuModeRect != null) MenuModeRect.Text = _localizationService.GetString("Mode_Rect");
            if (MenuModeWin != null) MenuModeWin.Text = _localizationService.GetString("Mode_Win");
            if (MenuModeFull != null) MenuModeFull.Text = _localizationService.GetString("Mode_Full");
            if (MenuModeFree != null) MenuModeFree.Text = _localizationService.GetString("Mode_Free");

            if (MenuDelayNone != null) MenuDelayNone.Text = _localizationService.GetString("Delay_None");
            if (MenuDelay3s != null) MenuDelay3s.Text = _localizationService.GetString("Delay_3s");
            if (MenuDelay5s != null) MenuDelay5s.Text = _localizationService.GetString("Delay_5s");
            if (MenuDelay10s != null) MenuDelay10s.Text = _localizationService.GetString("Delay_10s");

            if (MenuShapeRect != null) MenuShapeRect.Text = _localizationService.GetString("Shape_Rect");
            if (MenuShapeRoundedRect != null) MenuShapeRoundedRect.Text = _localizationService.GetString("Shape_RoundedRect");
            if (MenuShapeEllipse != null) MenuShapeEllipse.Text = _localizationService.GetString("Shape_Ellipse");
            if (MenuShapeArrow != null) MenuShapeArrow.Text = _localizationService.GetString("Shape_Arrow");
            if (MenuShapeLine != null) MenuShapeLine.Text = _localizationService.GetString("Shape_Line");
            if (MenuShapeCallout != null) MenuShapeCallout.Text = _localizationService.GetString("Shape_Callout");
            if (MenuShapeFill != null) MenuShapeFill.Text = _localizationService.GetString("Shape_Fill");

            if (MenuShapeThickness2 != null) MenuShapeThickness2.Text = _localizationService.GetString("Shape_Thickness_Fine");
            if (MenuShapeThickness4 != null) MenuShapeThickness4.Text = _localizationService.GetString("Shape_Thickness_Medium");
            if (MenuShapeThickness8 != null) MenuShapeThickness8.Text = _localizationService.GetString("Shape_Thickness_Bold");
            if (MenuShapeThickness12 != null) MenuShapeThickness12.Text = _localizationService.GetString("Shape_Thickness_ExtraBold");

            if (MenuShapeColorRed != null) MenuShapeColorRed.Text = _localizationService.GetString("Color_Red");
            if (MenuShapeColorBlue != null) MenuShapeColorBlue.Text = _localizationService.GetString("Color_Blue");
            if (MenuShapeColorGreen != null) MenuShapeColorGreen.Text = _localizationService.GetString("Color_Green");
            if (MenuShapeColorYellow != null) MenuShapeColorYellow.Text = _localizationService.GetString("Color_Yellow");
            if (MenuShapeColorBlack != null) MenuShapeColorBlack.Text = _localizationService.GetString("Color_Black");
            if (MenuShapeColorWhite != null) MenuShapeColorWhite.Text = _localizationService.GetString("Color_White");

            if (MenuBadgeColorAccent != null) MenuBadgeColorAccent.Text = _localizationService.GetString("Color_Accent");
            if (MenuBadgeColorRed != null) MenuBadgeColorRed.Text = _localizationService.GetString("Color_Red");
            if (MenuBadgeColorOrange != null) MenuBadgeColorOrange.Text = _localizationService.GetString("Color_Orange");
            if (MenuBadgeColorGreen != null) MenuBadgeColorGreen.Text = _localizationService.GetString("Color_Green");
            if (MenuBadgeColorPurple != null) MenuBadgeColorPurple.Text = _localizationService.GetString("Color_Purple");

            if (MenuBadgeSizeSmall != null) MenuBadgeSizeSmall.Text = _localizationService.GetString("Badge_Size_Small");
            if (MenuBadgeSizeMedium != null) MenuBadgeSizeMedium.Text = _localizationService.GetString("Badge_Size_Medium");
            if (MenuBadgeSizeLarge != null) MenuBadgeSizeLarge.Text = _localizationService.GetString("Badge_Size_Large");
            if (MenuBadgeReset != null) MenuBadgeReset.Text = _localizationService.GetString("Badge_Reset");

            if (MenuPrint != null) MenuPrint.Text = _localizationService.GetString("Action_Print");
            if (MenuSettings != null) MenuSettings.Text = _localizationService.GetString("Nav_Settings");
            if (MenuOpenVault != null) MenuOpenVault.Text = _localizationService.GetString("Nav_Vault");
            if (MenuPrivacy != null) MenuPrivacy.Text = _localizationService.GetString("Privacy_DialogTitle");
            if (MenuAbout != null) MenuAbout.Text = _localizationService.GetString("Nav_About");

            if (BtnZoomOut != null) ToolTipService.SetToolTip(BtnZoomOut, _localizationService.GetString("Zoom_Out"));
            if (BtnZoomReset != null) ToolTipService.SetToolTip(BtnZoomReset, _localizationService.GetString("Zoom_Reset"));
            if (BtnZoomIn != null) ToolTipService.SetToolTip(BtnZoomIn, _localizationService.GetString("Zoom_In"));
            if (TxtStatusBarEngine != null) TxtStatusBarEngine.Text = _localizationService.GetString("Status_EngineReady");

            // 3. Guidance & Action Button Text
            if (TxtCommitSave != null) TxtCommitSave.Text = _localizationService.GetString("Action_CommitSave");
            if (TxtRevertHand != null) TxtRevertHand.Text = _localizationService.GetString("Action_RevertHand");
            if (TxtModeGuidance != null) TxtModeGuidance.Text = _localizationService.GetString("Action_ModeGuidance");
        }
        catch (Exception) { }
    }

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

    private async void MenuPrivacy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ShowPrivacyAndLicensingDialogAsync();
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e) =>
        NotifyToolChange(_localizationService.GetString("About_Title"));

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
            tab.AutoSaveStatus = string.Format(_localizationService.GetString("Status_SavedTo"), fileName);
            NotifyToolChange(tab.AutoSaveStatus);
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
            tab.AutoSaveStatus = _localizationService.GetString("Status_Copied");
            NotifyToolChange(tab.AutoSaveStatus);
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    // ==========================================
    // 8. TabView Management & Context Menus
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
