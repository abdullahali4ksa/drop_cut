using System.Runtime.InteropServices;
using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Storage.Streams;
using Windows.System;

namespace DropCut.App;

/// <summary>
/// Professional frameless, fullscreen transparent/dimmed sniper overlay window.
/// Replicates the Windows 11 Snipping Tool interactive cropping experience with top floating pill bar.
/// </summary>
public sealed partial class SnipOverlayWindow : Window
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private readonly byte[] _desktopBmpBytes;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly Action<byte[]?, int, int> _onCompleted;
    private CaptureMode _currentMode;

    private bool _isSelecting;
    private Windows.Foundation.Point _startPoint;
    private double _lastX;
    private double _lastY;
    private double _lastWidth;
    private double _lastHeight;

    public SnipOverlayWindow(
        byte[] desktopBmpBytes,
        CaptureMode initialMode,
        IScreenCaptureService screenCaptureService,
        Action<byte[]?, int, int> onCompleted)
    {
        _desktopBmpBytes = desktopBmpBytes;
        _currentMode = initialMode;
        _screenCaptureService = screenCaptureService;
        _onCompleted = onCompleted;

        InitializeComponent();
        ConfigureFramelessOverlay();
        LoadFrozenDesktopImage();
    }

    private void ConfigureFramelessOverlay()
    {
        try
        {
            var appWindow = this.AppWindow;
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }

            var width = GetSystemMetrics(SM_CXSCREEN);
            var height = GetSystemMetrics(SM_CYSCREEN);
            if (width <= 0) width = 1920;
            if (height <= 0) height = 1080;

            appWindow.MoveAndResize(new RectInt32(0, 0, width, height));
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
    }

    private void LoadFrozenDesktopImage()
    {
        try
        {
            if (_desktopBmpBytes is { Length: > 0 })
            {
                var stream = new InMemoryRandomAccessStream();
                var netStream = stream.AsStreamForWrite();
                netStream.Write(_desktopBmpBytes, 0, _desktopBmpBytes.Length);
                netStream.Flush();
                stream.Seek(0);

                var bmpImg = new BitmapImage();
                bmpImg.SetSource(stream);
                FrozenDesktopImage.Source = bmpImg;
            }

            RootGrid.Loaded += (s, e) => UpdateDimmerCutout(0, 0, 0, 0);
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
        }
    }

    private void UpdateDimmerCutout(double x, double y, double w, double h)
    {
        try
        {
            double totalW = RootGrid.ActualWidth > 0 ? RootGrid.ActualWidth : GetSystemMetrics(SM_CXSCREEN);
            double totalH = RootGrid.ActualHeight > 0 ? RootGrid.ActualHeight : GetSystemMetrics(SM_CYSCREEN);

            if (w <= 0 || h <= 0)
            {
                Canvas.SetLeft(DimmerTop, 0);
                Canvas.SetTop(DimmerTop, 0);
                DimmerTop.Width = totalW;
                DimmerTop.Height = totalH;

                DimmerBottom.Width = 0;
                DimmerBottom.Height = 0;
                DimmerLeft.Width = 0;
                DimmerLeft.Height = 0;
                DimmerRight.Width = 0;
                DimmerRight.Height = 0;
            }
            else
            {
                Canvas.SetLeft(DimmerTop, 0);
                Canvas.SetTop(DimmerTop, 0);
                DimmerTop.Width = totalW;
                DimmerTop.Height = Math.Max(0, y);

                double bottomY = y + h;
                Canvas.SetLeft(DimmerBottom, 0);
                Canvas.SetTop(DimmerBottom, bottomY);
                DimmerBottom.Width = totalW;
                DimmerBottom.Height = Math.Max(0, totalH - bottomY);

                Canvas.SetLeft(DimmerLeft, 0);
                Canvas.SetTop(DimmerLeft, y);
                DimmerLeft.Width = Math.Max(0, x);
                DimmerLeft.Height = h;

                double rightX = x + w;
                Canvas.SetLeft(DimmerRight, rightX);
                Canvas.SetTop(DimmerRight, y);
                DimmerRight.Width = Math.Max(0, totalW - rightX);
                DimmerRight.Height = h;
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    // ==========================================
    // Pointer Drag & Cutout Selection
    // ==========================================
    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (_currentMode == CaptureMode.Fullscreen)
            {
                int bmpW = BitConverter.ToInt32(_desktopBmpBytes, 18);
                int bmpH = Math.Abs(BitConverter.ToInt32(_desktopBmpBytes, 22));
                CompleteCapture(_desktopBmpBytes, bmpW, bmpH);
                return;
            }

            try { RootGrid.CapturePointer(e.Pointer); } catch { }
            var pt = e.GetCurrentPoint(RootGrid).Position;
            _startPoint = pt;
            _isSelecting = true;

            SelectionBox.Visibility = Visibility.Visible;
            DimensionBadge.Visibility = Visibility.Visible;

            Canvas.SetLeft(SelectionBox, pt.X);
            Canvas.SetTop(SelectionBox, pt.Y);
            SelectionBox.Width = 0;
            SelectionBox.Height = 0;
            UpdateDimmerCutout(pt.X, pt.Y, 0, 0);
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (!_isSelecting)
            {
                return;
            }

            var currentPoint = e.GetCurrentPoint(RootGrid).Position;
            _lastX = Math.Min(_startPoint.X, currentPoint.X);
            _lastY = Math.Min(_startPoint.Y, currentPoint.Y);
            _lastWidth = Math.Abs(currentPoint.X - _startPoint.X);
            _lastHeight = Math.Abs(currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionBox, _lastX);
            Canvas.SetTop(SelectionBox, _lastY);
            SelectionBox.Width = _lastWidth;
            SelectionBox.Height = _lastHeight;

            Canvas.SetLeft(DimensionBadge, _lastX);
            Canvas.SetTop(DimensionBadge, Math.Max(10, _lastY - 32));

            int bmpWidth = BitConverter.ToInt32(_desktopBmpBytes, 18);
            int bmpHeight = Math.Abs(BitConverter.ToInt32(_desktopBmpBytes, 22));
            double scaleX = RootGrid.ActualWidth > 0 ? (double)bmpWidth / RootGrid.ActualWidth : 1.0;
            double scaleY = RootGrid.ActualHeight > 0 ? (double)bmpHeight / RootGrid.ActualHeight : 1.0;

            DimensionText.Text = $"{(int)Math.Round(_lastWidth * scaleX)} × {(int)Math.Round(_lastHeight * scaleY)} px";
            UpdateDimmerCutout(_lastX, _lastY, _lastWidth, _lastHeight);
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            try { RootGrid.ReleasePointerCapture(e.Pointer); } catch { }
            if (!_isSelecting)
            {
                return;
            }

            _isSelecting = false;

            if (_lastWidth > 15 && _lastHeight > 15)
            {
                int bmpWidth = BitConverter.ToInt32(_desktopBmpBytes, 18);
                int bmpHeight = Math.Abs(BitConverter.ToInt32(_desktopBmpBytes, 22));
                double scaleX = RootGrid.ActualWidth > 0 ? (double)bmpWidth / RootGrid.ActualWidth : 1.0;
                double scaleY = RootGrid.ActualHeight > 0 ? (double)bmpHeight / RootGrid.ActualHeight : 1.0;

                int cropX = (int)Math.Round(_lastX * scaleX);
                int cropY = (int)Math.Round(_lastY * scaleY);
                int cropW = (int)Math.Round(_lastWidth * scaleX);
                int cropH = (int)Math.Round(_lastHeight * scaleY);

                var croppedBytes = _screenCaptureService.CropBitmap(
                    _desktopBmpBytes,
                    cropX,
                    cropY,
                    cropW,
                    cropH);

                CompleteCapture(croppedBytes, cropW, cropH);
            }
            else
            {
                SelectionBox.Visibility = Visibility.Collapsed;
                DimensionBadge.Visibility = Visibility.Collapsed;
                UpdateDimmerCutout(0, 0, 0, 0);
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            if (e.Key == VirtualKey.Escape)
            {
                CancelCapture();
            }
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    // ==========================================
    // Top Floating Toolbar Mode Buttons
    // ==========================================
    private void BtnModeRect_Click(object sender, RoutedEventArgs e)
    {
        _currentMode = CaptureMode.Rectangular;
        ResetModeButtonStyles();
        BtnModeRect.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(51, 0, 120, 215));
    }

    private void BtnModeFreeform_Click(object sender, RoutedEventArgs e)
    {
        _currentMode = CaptureMode.Freeform;
        ResetModeButtonStyles();
        BtnModeFreeform.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(51, 0, 120, 215));
    }

    private void BtnModeWindow_Click(object sender, RoutedEventArgs e)
    {
        _currentMode = CaptureMode.Window;
        ResetModeButtonStyles();
        BtnModeWindow.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(51, 0, 120, 215));
    }

    private void BtnModeFullscreen_Click(object sender, RoutedEventArgs e)
    {
        _currentMode = CaptureMode.Fullscreen;
        CompleteCapture(_desktopBmpBytes, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => CancelCapture();

    private void ResetModeButtonStyles()
    {
        BtnModeRect.Background = null;
        BtnModeFreeform.Background = null;
        BtnModeWindow.Background = null;
        BtnModeFullscreen.Background = null;
    }

    private void CompleteCapture(byte[]? bytes, int width, int height)
    {
        try
        {
            this.Close();
            _onCompleted(bytes, width, height);
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }

    private void CancelCapture()
    {
        try
        {
            this.Close();
            _onCompleted(null, 0, 0);
        }
        catch (Exception)
        {
            // Protected Zero-Crash boundary
        }
    }
}
