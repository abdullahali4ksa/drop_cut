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
                using (var writer = new DataWriter(stream))
                {
                    writer.WriteBytes(_desktopBmpBytes);
                    writer.StoreAsync().AsTask().GetAwaiter().GetResult();
                    writer.DetachStream();
                }

                stream.Seek(0);
                var bmpImg = new BitmapImage();
                bmpImg.SetSource(stream);
                FrozenDesktopImage.Source = bmpImg;
            }
        }
        catch (Exception)
        {
            // Zero-Crash Mandate
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
                CompleteCapture(_desktopBmpBytes, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
                return;
            }

            var pt = e.GetCurrentPoint(RootGrid).Position;
            _startPoint = pt;
            _isSelecting = true;

            SelectionBox.Visibility = Visibility.Visible;
            DimensionBadge.Visibility = Visibility.Visible;

            Canvas.SetLeft(SelectionBox, pt.X);
            Canvas.SetTop(SelectionBox, pt.Y);
            SelectionBox.Width = 0;
            SelectionBox.Height = 0;
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
            DimensionText.Text = $"{(int)_lastWidth} × {(int)_lastHeight} px";
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
            if (!_isSelecting)
            {
                return;
            }

            _isSelecting = false;

            if (_lastWidth > 15 && _lastHeight > 15)
            {
                var croppedBytes = _screenCaptureService.CropBitmap(
                    _desktopBmpBytes,
                    (int)_lastX,
                    (int)_lastY,
                    (int)_lastWidth,
                    (int)_lastHeight);

                CompleteCapture(croppedBytes, (int)_lastWidth, (int)_lastHeight);
            }
            else
            {
                SelectionBox.Visibility = Visibility.Collapsed;
                DimensionBadge.Visibility = Visibility.Collapsed;
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
