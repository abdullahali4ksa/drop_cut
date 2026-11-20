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

    private Polyline? _activePolyline;
    private Canvas? _activeDrawingCanvas;

    // In-tab cropping state
    private Point _tabCropStart;
    private double _tabCropX;
    private double _tabCropY;
    private double _tabCropW;
    private double _tabCropH;
    private bool _isTabCropDragging;

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
        ILocalizationService localizationService)
    {
        ViewModel = viewModel;
        _screenCaptureService = screenCaptureService;
        _hotkeyService = hotkeyService;
        _ocrService = ocrService;
        _localizationService = localizationService;

        InitializeComponent();

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
                NotifyToolChange($"انتظر... سيتم التقاط الشاشة بعد {ViewModel.DelaySeconds} ثوانٍ");
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
        TxtDelayIndicator.Text = "بدون تأخير";
        NotifyToolChange("مؤقت القص: فوري بدون تأخير");
    }

    private void Delay3s_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DelaySeconds = 3;
        TxtDelayIndicator.Text = "3 ثوانٍ";
        NotifyToolChange("مؤقت القص: تأخير 3 ثوانٍ لالتقاط القوائم المنسدلة");
    }

    private void Delay5s_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DelaySeconds = 5;
        TxtDelayIndicator.Text = "5 ثوانٍ";
        NotifyToolChange("مؤقت القص: تأخير 5 ثوانٍ");
    }

    private void Delay10s_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DelaySeconds = 10;
        TxtDelayIndicator.Text = "10 ثوانٍ";
        NotifyToolChange("مؤقت القص: تأخير 10 ثوانٍ");
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
            NotifyToolChange("جارٍ سحب اللسان ومشاركة الصورة...");
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
        var deferral = args.GetDeferral();
        try
        {
            var tab = (sender as FrameworkElement)?.DataContext as ShellTabModel ?? ViewModel.SelectedTab;
            PrepareDataPackageForDragSync(tab, args.Data);
            ConfigureCompactDragUI(args.DragUI, tab);
            NotifyToolChange("جارٍ سحب الصورة ومشاركتها...");
        }
        catch (Exception) { }
        finally
        {
            deferral.Complete();
        }
    }

    private void ImageContainer_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var tab = ViewModel.SelectedTab;
            PrepareDataPackageForDragSync(tab, args.Data);
            ConfigureCompactDragUI(args.DragUI, tab);
            NotifyToolChange("جارٍ سحب ومشاركة الصورة بشكل مصغر...");
        }
        catch (Exception) { }
        finally
        {
            deferral.Complete();
        }
    }

    private void ShareButton_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var tab = ViewModel.SelectedTab;
            PrepareDataPackageForDragSync(tab, args.Data);
            ConfigureCompactDragUI(args.DragUI, tab);
            NotifyToolChange("جارٍ سحب ومشاركة اللقطة إلى تطبيق خارجي...");
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
            NotifyToolChange("يرجى أخذ لقطة شاشة أولاً أو اختيار لسان يحتوي على صورة لاقتصاصها");
            return;
        }

        tab.IsCropping = !tab.IsCropping;
        if (tab.IsCropping)
        {
            NotifyToolChange("وضع اقتصاص الصورة الحالية: اسحب بالفأرة فوق الصورة لتحديد منطقة القص ثم اضغط 'تطبيق القص'");
        }
        else
        {
            NotifyToolChange("تم إغلاق وضع الاقتصاص");
        }
    }

    private void CropCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (sender is not Canvas canvas) return;
            _tabCropStart = e.GetCurrentPoint(canvas).Position;
            _isTabCropDragging = true;

            var box = FindChildByTag<Border>(canvas, "InTabCropBox");
            var bar = FindChildByTag<StackPanel>(canvas, "InTabCropActionBar");
            if (box != null)
            {
                box.Visibility = Visibility.Visible;
                Canvas.SetLeft(box, _tabCropStart.X);
                Canvas.SetTop(box, _tabCropStart.Y);
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

    private void CropCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (!_isTabCropDragging || sender is not Canvas canvas) return;
            var cur = e.GetCurrentPoint(canvas).Position;
            _tabCropX = Math.Min(_tabCropStart.X, cur.X);
            _tabCropY = Math.Min(_tabCropStart.Y, cur.Y);
            _tabCropW = Math.Abs(cur.X - _tabCropStart.X);
            _tabCropH = Math.Abs(cur.Y - _tabCropStart.Y);

            var box = FindChildByTag<Border>(canvas, "InTabCropBox");
            if (box != null)
            {
                Canvas.SetLeft(box, _tabCropX);
                Canvas.SetTop(box, _tabCropY);
                box.Width = _tabCropW;
                box.Height = _tabCropH;
            }
        }
        catch (Exception) { }
    }

    private void CropCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (!_isTabCropDragging || sender is not Canvas canvas) return;
            _isTabCropDragging = false;

            var bar = FindChildByTag<StackPanel>(canvas, "InTabCropActionBar");
            if (bar != null && _tabCropW > 15 && _tabCropH > 15)
            {
                Canvas.SetLeft(bar, _tabCropX);
                Canvas.SetTop(bar, Math.Max(0, _tabCropY + _tabCropH + 8));
                bar.Visibility = Visibility.Visible;
            }
        }
        catch (Exception) { }
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
                tab.IsCropping = false;
                NotifyToolChange($"تم اقتصاص الصورة الحالية بنجاح إلى ({tab.ImageWidth} × {tab.ImageHeight} بكسل)");
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
            if (ViewModel.SelectedTab is not null)
            {
                ViewModel.SelectedTab.IsCropping = false;
                NotifyToolChange("تم إلغاء الاقتصاص");
            }
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
            NotifyToolChange("يرجى أخذ لقطة شاشة أولاً لتطبيق التمويه عليها");
            return;
        }

        tab.IsBlurring = !tab.IsBlurring;
        if (tab.IsBlurring)
        {
            tab.IsCropping = false;
            NotifyToolChange("وضع تمويه الخصوصية: اسحب بالفأرة لتحديد منطقة الأرقام أو البيانات الحساسة ثم اضغط 'تطبيق التمويه'");
        }
        else
        {
            NotifyToolChange("تم إغلاق وضع التمويه");
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
                tab.IsBlurring = false;
                NotifyToolChange("تم طمس وتمويه البيانات الحساسة بنجاح وحفظها بالذاكرة");
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
        if (ViewModel.SelectedTab != null)
        {
            ViewModel.SelectedTab.IsBlurring = false;
            NotifyToolChange("تم إلغاء التمويه");
        }
    }

    // ==========================================
    // 4.2 Hand / Pan Tool & Interactive Zoom Controls
    // ==========================================
    private void ToolHand_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ActiveTool = ToolType.Hand;
        NotifyToolChange("تم تفعيل يد التصفح والسحب (Hand Tool) - للتنقل والسحب بدون رسم خطوط");
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
        NotifyToolChange($"التكبير: {tab.ZoomPercentageText}");
    }

    private void ZoomOutCurrentTab()
    {
        var tab = ViewModel.SelectedTab;
        if (tab == null) return;
        var newZoom = Math.Max(0.2, tab.ZoomFactor - 0.15);
        tab.ZoomFactor = newZoom;
        GetActiveScrollViewer()?.ChangeView(null, null, (float)newZoom);
        NotifyToolChange($"التصغير: {tab.ZoomPercentageText}");
    }

    private void ZoomResetCurrentTab()
    {
        var tab = ViewModel.SelectedTab;
        if (tab == null) return;
        tab.ZoomFactor = 1.0;
        GetActiveScrollViewer()?.ChangeView(null, null, 1.0f);
        NotifyToolChange("الحجم الأصلي: 100%");
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
    // 5. Ribbon Tools: Pen, Highlighter, Eraser, Ruler, OCR
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
                NotifyToolChange("تراجع عن آخر رسمة");
            }
            else
            {
                NotifyToolChange("لا توجد رسومات للتراجع عنها");
            }
        }
        catch (Exception) { }
    }

    private void ActionRedo_Click(object sender, RoutedEventArgs e) =>
        NotifyToolChange("إعادة (Redo)");

    private void ActionSave_Click(object sender, RoutedEventArgs e) => SaveCurrentTabImage();

    private void ActionCopy_Click(object sender, RoutedEventArgs e) => CopyCurrentTabImage();

    private void ActionShare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CopyCurrentTabImage();
            NotifyToolChange("تم النسخ للحافظة — يمكنك أيضاً سحب التاب أو الصورة مباشرة إلى WhatsApp أو Telegram أو المستكشف");
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
                        rootElement.RequestedTheme = theme switch
                        {
                            "Dark" => ElementTheme.Dark,
                            "Light" => ElementTheme.Light,
                            _ => ElementTheme.Default
                        };
                    }
                }

                NotifyToolChange(_localizationService.GetString("Settings_SavedSuccess"));
            }
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
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
