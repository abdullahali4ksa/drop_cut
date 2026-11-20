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

        // If Windows OS is non-Arabic, automatically adapt to English or system language so foreign users see clean English
        try
        {
            var sysCulture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (!sysCulture.Equals("ar", StringComparison.OrdinalIgnoreCase))
            {
                var targetLang = _localizationService.SupportedLanguages.Any(l => l.Code.Equals(sysCulture, StringComparison.OrdinalIgnoreCase))
                    ? sysCulture
                    : "en";
                _ = _localizationService.SetLanguageAsync(targetLang);
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

        // Initialize with Hand tool as default
        SetToolMode(ToolType.Hand);
    }

    private void MainTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SetToolMode(ToolType.Hand);
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
            else if (isCtrlDown && (e.Key == VirtualKey.Number0 || e.Key == VirtualKey.NumberPad0))
            {
                e.Handled = true;
                ZoomResetCurrentTab();
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
            await Task.Delay(180);

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

            // 4. Set rich drag metadata and thumbnail on DataPackage
            data.Properties.Title = "اسحبها وشاركها إلينا (WhatsApp / Telegram / Explorer)";
            data.Properties.Description = "اسحب وأفلت داخل أي محادثة أو مجلد للمشاركة الفورية";
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
                            ViewModel.SelectedTab.AutoSaveStatus = $"تم تحميل الصورة من {file.Name}";
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
            int imgW = tab.ImageWidth > 0 ? tab.ImageWidth : 800;
            int imgH = tab.ImageHeight > 0 ? tab.ImageHeight : 600;

            int insetX = Math.Min(30, Math.Max(10, imgW / 12));
            int insetY = Math.Min(30, Math.Max(10, imgH / 12));
            _tabCropX = insetX;
            _tabCropY = insetY;
            _tabCropW = Math.Max(50, imgW - insetX * 2);
            _tabCropH = Math.Max(50, imgH - insetY * 2);

            SetToolMode(ToolType.Crop);

            var scroll = GetActiveScrollViewer();
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

            var cropped = _screenCaptureService.CropBitmap(bytes, (int)_tabCropX, (int)_tabCropY, (int)_tabCropW, (int)_tabCropH);
            if (cropped is { Length: > 0 })
            {
                tab.ImageBytes = cropped;
                tab.ImageWidth = (int)_tabCropW;
                tab.ImageHeight = (int)_tabCropH;
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

            var blurred = _screenCaptureService.BlurRegion(bytes, (int)_tabBlurX, (int)_tabBlurY, (int)_tabBlurW, (int)_tabBlurH);
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

    private ScrollViewer? GetActiveScrollViewer() =>
        FindChildByTag<ScrollViewer>(MainTabView, "TabScrollViewer");

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e) => ZoomInCurrentTab();
    private void BtnZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOutCurrentTab();
    private void BtnZoomReset_Click(object sender, RoutedEventArgs e) => ZoomResetCurrentTab();

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

    private void ToolEraser_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDrawingCanvas != null && _activeDrawingCanvas.Children.Count > 0)
        {
            _activeDrawingCanvas.Children.Clear();
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
                    ShowModeGuidance("\uED63", "وضع القلم نشط — تم قفل وضع السحب مؤقتاً لحماية الرسم ✏️");
                    SetCanvasCursor(InputSystemCursorShape.Cross);
                    break;

                case ToolType.Highlighter:
                    ApplyActiveYellowStyle(BtnHighlighterTool);
                    ShowModeGuidance("\uE7E6", "وضع قلم التمييز نشط — تم قفل وضع السحب لحماية الرسم ✏️");
                    SetCanvasCursor(InputSystemCursorShape.Cross);
                    break;

                case ToolType.BlurRedact:
                    ApplyActiveYellowStyle(BtnBlurTool);
                    ShowModeGuidance("\uE790", "وضع طمس البيانات الحساسة نشط — تم قفل وضع السحب مؤقتاً 🫥");
                    SetCanvasCursor(InputSystemCursorShape.Cross);
                    break;

                case ToolType.Crop:
                    ApplyActiveYellowStyle(BtnCropTool);
                    ShowModeGuidance("\uE7A8", "وضع اقتصاص الصورة نشط — اسحب من أي مقبض من الـ 8 نقاط للتقليص 🔲");
                    SetCanvasCursor(InputSystemCursorShape.Cross);
                    break;

                case ToolType.Select:
                    ApplyActiveYellowStyle(BtnEraserTool);
                    ShowModeGuidance("\uE75C", "وضع الممحاة نشط 🧹");
                    SetCanvasCursor(InputSystemCursorShape.Arrow);
                    break;

                case ToolType.Shape:
                    ApplyActiveYellowStyle(BtnRulerTool);
                    ShowModeGuidance("\uED5E", "مسطرة القياس نشطة 📏");
                    SetCanvasCursor(InputSystemCursorShape.Cross);
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

            // 3. If currently in Inking mode -> Rasterize annotations!
            if (tab.ImageBytes is { Length: > 0 } bytes && _activeDrawingCanvas != null && _activeDrawingCanvas.Children.Count > 0)
            {
                var strokes = new List<StrokeRenderData>();
                foreach (var child in _activeDrawingCanvas.Children)
                {
                    if (child is Polyline poly && poly.Points.Count >= 2)
                    {
                        var pts = poly.Points.Select(p => (p.X, p.Y)).ToList();
                        var hex = (poly.Stroke as SolidColorBrush)?.Color.ToString() ?? "#FF0000";
                        var thickness = (int)poly.StrokeThickness;
                        bool isHighlight = poly.Opacity < 0.9 || thickness >= 10;
                        strokes.Add(new StrokeRenderData(pts, hex, thickness, isHighlight));
                    }
                }

                if (strokes.Count > 0)
                {
                    var baked = _screenCaptureService.RenderAnnotations(bytes, strokes);
                    if (baked != null && baked.Length > 0)
                    {
                        tab.ImageBytes = baked;
                        _activeDrawingCanvas.Children.Clear();
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
            NotifyToolChange($"تعذر استخراج النصوص: {ex.Message}");
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
            if (_activePolyline != null && sender is Canvas canvas && ViewModel.SelectedTab?.IsCropping != true)
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
    // 7. Actions: Undo, Redo, Save, Copy, Share
    // ==========================================
    private void ActionUndo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_activeDrawingCanvas != null && _activeDrawingCanvas.Children.Count > 0)
            {
                _activeDrawingCanvas.Children.RemoveAt(_activeDrawingCanvas.Children.Count - 1);
                NotifyToolChange(_localizationService.GetString("Action_Undo"));
            }
        }
        catch (Exception) { }
    }

    private void ActionRedo_Click(object sender, RoutedEventArgs e) =>
        NotifyToolChange(_localizationService.GetString("Action_Redo"));

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

            panel.Children.Add(langLabel);
            panel.Children.Add(langCombo);
            panel.Children.Add(themeLabel);
            panel.Children.Add(themeCombo);
            panel.Children.Add(autoSaveToggle);

            var dialog = new ContentDialog
            {
                Title = _localizationService.GetString("Settings_Title"),
                Content = panel,
                PrimaryButtonText = _localizationService.GetString("Settings_SaveButton"),
                CloseButtonText = _localizationService.GetString("Tab_Close"),
                XamlRoot = this.Content.XamlRoot
            };

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
            ToolTipService.SetToolTip(BtnEraserTool, _localizationService.GetString("Tool_Eraser_Tip"));
            ToolTipService.SetToolTip(BtnBlurTool, _localizationService.GetString("Tool_Blur_Tip"));
            ToolTipService.SetToolTip(BtnRulerTool, _localizationService.GetString("Tool_Ruler_Tip"));
            ToolTipService.SetToolTip(BtnTextTool, _localizationService.GetString("Tool_Text_Tip"));
            ToolTipService.SetToolTip(BtnCropTool, _localizationService.GetString("Tool_Crop_Tip"));

            ToolTipService.SetToolTip(BtnUndo, _localizationService.GetString("Action_Undo_Tip"));
            ToolTipService.SetToolTip(BtnRedo, _localizationService.GetString("Action_Redo_Tip"));
            ToolTipService.SetToolTip(BtnSave, _localizationService.GetString("Action_Save_Tip"));
            ToolTipService.SetToolTip(BtnCopy, _localizationService.GetString("Action_Copy_Tip"));
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

            if (MenuSettings != null) MenuSettings.Text = _localizationService.GetString("Nav_Settings");
            if (MenuOpenVault != null) MenuOpenVault.Text = _localizationService.GetString("Nav_Vault");
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
