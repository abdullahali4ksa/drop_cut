using System.Runtime.InteropServices;
using DropCut.Core.Contracts;

namespace DropCut.Infrastructure.Win32;

/// <summary>
/// Hardware-accelerated desktop screen capture service using high-performance Win32 GDI BitBlt
/// with CAPTUREBLT flag for capturing transparent and layered Windows 10/11 windows.
/// </summary>
public class ScreenCaptureService : IScreenCaptureService
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private const int SRCCOPY = 0x00CC0020;
    private const int CAPTUREBLT = 0x40000000;
    private const int BI_RGB = 0;
    private const int DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public int bmiColors;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGdiObj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    public Task<byte[]> CapturePrimaryScreenAsync(CancellationToken cancellationToken = default)
    {
        var width = GetSystemMetrics(SM_CXSCREEN);
        var height = GetSystemMetrics(SM_CYSCREEN);

        if (width <= 0) width = 1920;
        if (height <= 0) height = 1080;

        return CaptureRegionAsync(0, 0, width, height, cancellationToken);
    }

    public Task<byte[]> CaptureActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
        {
            return CapturePrimaryScreenAsync(cancellationToken);
        }

        var x = Math.Max(0, rect.Left);
        var y = Math.Max(0, rect.Top);
        var width = Math.Max(100, rect.Right - rect.Left);
        var height = Math.Max(100, rect.Bottom - rect.Top);

        return CaptureRegionAsync(x, y, width, height, cancellationToken);
    }

    public Task<byte[]> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken cancellationToken = default)
    {
        if (width <= 0) width = 800;
        if (height <= 0) height = 600;

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return Task.FromResult(GenerateFallbackBmp(width, height));
        }

        var memDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        var oldObj = SelectObject(memDc, bitmap);

        try
        {
            // BitBlt with CAPTUREBLT to capture translucent/layered Windows 11 Fluent elements
            var rop = SRCCOPY | CAPTUREBLT;
            BitBlt(memDc, 0, 0, width, height, screenDc, x, y, rop);

            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // Negative height indicates top-down DIB
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB,
                    biSizeImage = width * height * 4
                }
            };

            var pixelBytes = new byte[width * height * 4];
            GetDIBits(memDc, bitmap, 0, (uint)height, pixelBytes, ref bmi, DIB_RGB_COLORS);

            // GDI BitBlt does not populate the Alpha channel in 32-bit DIBs.
            // Force Alpha = 255 (0xFF) on all pixels to ensure 100% solid opacity without any transparency.
            for (int i = 3; i < pixelBytes.Length; i += 4)
            {
                pixelBytes[i] = 255;
            }

            // Construct valid standard 32-bit BMP byte stream
            var bmpBytes = WrapInBmpContainer(pixelBytes, width, height);
            return Task.FromResult(bmpBytes);
        }
        catch
        {
            return Task.FromResult(GenerateFallbackBmp(width, height));
        }
        finally
        {
            SelectObject(memDc, oldObj);
            DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public byte[] CropBitmap(byte[] sourceBmpBytes, int x, int y, int width, int height)
    {
        if (sourceBmpBytes == null || sourceBmpBytes.Length < 54)
        {
            return Array.Empty<byte>();
        }

        try
        {
            var pixelDataOffset = BitConverter.ToInt32(sourceBmpBytes, 10);
            var srcWidth = BitConverter.ToInt32(sourceBmpBytes, 18);
            var rawHeight = BitConverter.ToInt32(sourceBmpBytes, 22);
            var isTopDown = rawHeight < 0;
            var srcHeight = Math.Abs(rawHeight);
            var bpp = BitConverter.ToInt16(sourceBmpBytes, 28);

            if (bpp != 32 || srcWidth <= 0 || srcHeight <= 0)
            {
                return sourceBmpBytes;
            }

            var safeX = Math.Clamp(x, 0, srcWidth - 1);
            var safeY = Math.Clamp(y, 0, srcHeight - 1);
            var safeWidth = Math.Clamp(width, 1, srcWidth - safeX);
            var safeHeight = Math.Clamp(height, 1, srcHeight - safeY);

            var croppedPixels = new byte[safeWidth * safeHeight * 4];

            for (int row = 0; row < safeHeight; row++)
            {
                int srcRowIndex = isTopDown ? (safeY + row) : (srcHeight - 1 - (safeY + row));
                int srcRowOffset = pixelDataOffset + (srcRowIndex * srcWidth + safeX) * 4;
                int dstRowOffset = (row * safeWidth) * 4;

                if (srcRowOffset + (safeWidth * 4) <= sourceBmpBytes.Length)
                {
                    Array.Copy(sourceBmpBytes, srcRowOffset, croppedPixels, dstRowOffset, safeWidth * 4);
                }
            }

            // Ensure 100% solid opacity on all cropped pixels
            for (int i = 3; i < croppedPixels.Length; i += 4)
            {
                croppedPixels[i] = 255;
            }

            return WrapInBmpContainer(croppedPixels, safeWidth, safeHeight);
        }
        catch
        {
            return sourceBmpBytes;
        }
    }

    public byte[] BlurRegion(byte[] sourceBmpBytes, int x, int y, int width, int height, int blockSize = 12)
    {
        if (sourceBmpBytes == null || sourceBmpBytes.Length < 54 || width <= 0 || height <= 0)
        {
            return sourceBmpBytes ?? Array.Empty<byte>();
        }

        try
        {
            var result = (byte[])sourceBmpBytes.Clone();
            var pixelDataOffset = BitConverter.ToInt32(result, 10);
            var srcWidth = BitConverter.ToInt32(result, 18);
            var rawHeight = BitConverter.ToInt32(result, 22);
            var isTopDown = rawHeight < 0;
            var srcHeight = Math.Abs(rawHeight);
            var bpp = BitConverter.ToInt16(result, 28);

            if (bpp != 32 || srcWidth <= 0 || srcHeight <= 0)
            {
                return sourceBmpBytes;
            }

            var safeX = Math.Clamp(x, 0, srcWidth - 1);
            var safeY = Math.Clamp(y, 0, srcHeight - 1);
            var safeWidth = Math.Clamp(width, 1, srcWidth - safeX);
            var safeHeight = Math.Clamp(height, 1, srcHeight - safeY);
            var bs = Math.Max(4, blockSize);

            for (int by = 0; by < safeHeight; by += bs)
            {
                int bh = Math.Min(bs, safeHeight - by);
                for (int bx = 0; bx < safeWidth; bx += bs)
                {
                    int bw = Math.Min(bs, safeWidth - bx);

                    // Compute average color in block
                    long sumB = 0, sumG = 0, sumR = 0;
                    int count = 0;

                    for (int dy = 0; dy < bh; dy++)
                    {
                        int py = safeY + by + dy;
                        int rowIndex = isTopDown ? py : (srcHeight - 1 - py);
                        int rowOffset = pixelDataOffset + (rowIndex * srcWidth + safeX + bx) * 4;

                        for (int dx = 0; dx < bw; dx++)
                        {
                            int offset = rowOffset + dx * 4;
                            if (offset + 3 < result.Length)
                            {
                                sumB += result[offset];
                                sumG += result[offset + 1];
                                sumR += result[offset + 2];
                                count++;
                            }
                        }
                    }

                    if (count > 0)
                    {
                        byte avgB = (byte)(sumB / count);
                        byte avgG = (byte)(sumG / count);
                        byte avgR = (byte)(sumR / count);

                        for (int dy = 0; dy < bh; dy++)
                        {
                            int py = safeY + by + dy;
                            int rowIndex = isTopDown ? py : (srcHeight - 1 - py);
                            int rowOffset = pixelDataOffset + (rowIndex * srcWidth + safeX + bx) * 4;

                            for (int dx = 0; dx < bw; dx++)
                            {
                                int offset = rowOffset + dx * 4;
                                if (offset + 3 < result.Length)
                                {
                                    result[offset] = avgB;
                                    result[offset + 1] = avgG;
                                    result[offset + 2] = avgR;
                                    result[offset + 3] = 255;
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }
        catch
        {
            return sourceBmpBytes;
        }
    }

    private static byte[] WrapInBmpContainer(byte[] pixelData, int width, int height)
    {
        const int fileHeaderSize = 14;
        const int infoHeaderSize = 40;
        const int headerTotal = fileHeaderSize + infoHeaderSize;
        var fileSize = headerTotal + pixelData.Length;

        var bmp = new byte[fileSize];
        using var stream = new MemoryStream(bmp);
        using var writer = new BinaryWriter(stream);

        // BITMAPFILEHEADER
        writer.Write((ushort)0x4D42);          // "BM"
        writer.Write((uint)fileSize);           // File size
        writer.Write((ushort)0);                // Reserved 1
        writer.Write((ushort)0);                // Reserved 2
        writer.Write((uint)headerTotal);        // Offset to pixel bits (54)

        // BITMAPINFOHEADER
        writer.Write((uint)infoHeaderSize);     // Size of header (40)
        writer.Write(width);                    // Width
        writer.Write(-height);                  // Top-down height
        writer.Write((ushort)1);                // Planes
        writer.Write((ushort)32);               // 32 bits per pixel (BGRA)
        writer.Write((uint)0);                  // Compression (BI_RGB)
        writer.Write((uint)pixelData.Length);   // Image size
        writer.Write(2835);                     // X resolution (72 DPI)
        writer.Write(2835);                     // Y resolution (72 DPI)
        writer.Write((uint)0);                  // Colors used
        writer.Write((uint)0);                  // Colors important

        // Raw pixel data
        writer.Write(pixelData);

        return bmp;
    }

    private static byte[] GenerateFallbackBmp(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        // Fill with modern dark slate background #1E1E1E
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 0x1E;     // B
            pixels[i + 1] = 0x1E; // G
            pixels[i + 2] = 0x1E; // R
            pixels[i + 3] = 0xFF; // A
        }
        return WrapInBmpContainer(pixels, width, height);
    }

    public byte[] RenderAnnotations(byte[] sourceBmpBytes, IEnumerable<DropCut.Core.Domain.StrokeRenderData> strokes, IEnumerable<DropCut.Core.Domain.BadgeRenderData>? badges = null)
    {
        if (sourceBmpBytes == null || sourceBmpBytes.Length < 54 || strokes == null)
        {
            return sourceBmpBytes ?? Array.Empty<byte>();
        }

        try
        {
            var result = (byte[])sourceBmpBytes.Clone();
            var pixelDataOffset = BitConverter.ToInt32(result, 10);
            var srcWidth = BitConverter.ToInt32(result, 18);
            var rawHeight = BitConverter.ToInt32(result, 22);
            var isTopDown = rawHeight < 0;
            var srcHeight = Math.Abs(rawHeight);
            var bpp = BitConverter.ToInt16(result, 28);

            if (bpp != 32 || srcWidth <= 0 || srcHeight <= 0)
            {
                return sourceBmpBytes;
            }

            foreach (var stroke in strokes)
            {
                if (stroke.Points == null || stroke.Points.Count < 2) continue;

                // Parse stroke color
                byte r = 255, g = 0, b = 0;
                var hex = stroke.ColorHex?.TrimStart('#') ?? "FF0000";
                if (hex.Length >= 6)
                {
                    if (hex.Length == 8) hex = hex.Substring(2);
                    if (byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var pr)) r = pr;
                    if (byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var pg)) g = pg;
                    if (byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var pb)) b = pb;
                }

                int radius = Math.Max(1, stroke.Thickness / 2);
                bool isHighlight = stroke.IsHighlighter;
                var visited = isHighlight ? new HashSet<int>() : null;

                for (int i = 0; i < stroke.Points.Count - 1; i++)
                {
                    int x0 = (int)stroke.Points[i].X;
                    int y0 = (int)stroke.Points[i].Y;
                    int x1 = (int)stroke.Points[i + 1].X;
                    int y1 = (int)stroke.Points[i + 1].Y;

                    // Bresenham line algorithm with brush circle stamping
                    int dx = Math.Abs(x1 - x0);
                    int dy = Math.Abs(y1 - y0);
                    int sx = x0 < x1 ? 1 : -1;
                    int sy = y0 < y1 ? 1 : -1;
                    int err = dx - dy;

                    int curX = x0;
                    int curY = y0;

                    while (true)
                    {
                        // Stamp disk with radius
                        for (int ry = -radius; ry <= radius; ry++)
                        {
                            int py = curY + ry;
                            if (py < 0 || py >= srcHeight) continue;
                            int rowIndex = isTopDown ? py : (srcHeight - 1 - py);

                            for (int rx = -radius; rx <= radius; rx++)
                            {
                                if (rx * rx + ry * ry > radius * radius) continue;
                                int px = curX + rx;
                                if (px < 0 || px >= srcWidth) continue;

                                int offset = pixelDataOffset + (rowIndex * srcWidth + px) * 4;
                                if (offset + 3 < result.Length)
                                {
                                    if (isHighlight)
                                    {
                                        if (visited!.Add(offset))
                                        {
                                            // True neon highlighter transparency (45% stroke color, 55% background)
                                            result[offset]     = (byte)((result[offset]     * 55 + b * 45) / 100);
                                            result[offset + 1] = (byte)((result[offset + 1] * 55 + g * 45) / 100);
                                            result[offset + 2] = (byte)((result[offset + 2] * 55 + r * 45) / 100);
                                            result[offset + 3] = 255;
                                        }
                                    }
                                    else
                                    {
                                        result[offset] = b;
                                        result[offset + 1] = g;
                                        result[offset + 2] = r;
                                        result[offset + 3] = 255;
                                    }
                                }
                            }
                        }

                        if (curX == x1 && curY == y1) break;
                        int e2 = 2 * err;
                        if (e2 > -dy)
                        {
                            err -= dy;
                            curX += sx;
                        }
                        if (e2 < dx)
                        {
                            err += dx;
                            curY += sy;
                        }
                    }
                }
            }

            // Render Numbered Step Badges if provided
            if (badges != null)
            {
                foreach (var badge in badges)
                {
                    int cx = (int)badge.X;
                    int cy = (int)badge.Y;
                    int r = Math.Max(8, badge.Diameter / 2);
                    int r2 = r * r;
                    int innerBorder2 = Math.Max(1, (r - 2) * (r - 2));

                    // Parse background color
                    byte bgR = 0, bgG = 120, bgB = 215;
                    var bHex = badge.BackgroundHex?.TrimStart('#') ?? "0078D7";
                    if (bHex.Length >= 6)
                    {
                        if (bHex.Length == 8) bHex = bHex.Substring(2);
                        if (byte.TryParse(bHex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var bR)) bgR = bR;
                        if (byte.TryParse(bHex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var bG)) bgG = bG;
                        if (byte.TryParse(bHex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var bB)) bgB = bB;
                    }

                    // 1. Draw circular badge body with white outer border
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int py = cy + dy;
                        if (py < 0 || py >= srcHeight) continue;
                        int rowIndex = isTopDown ? py : (srcHeight - 1 - py);

                        for (int dx = -r; dx <= r; dx++)
                        {
                            int dist2 = dx * dx + dy * dy;
                            if (dist2 > r2) continue;
                            int px = cx + dx;
                            if (px < 0 || px >= srcWidth) continue;

                            int offset = pixelDataOffset + (rowIndex * srcWidth + px) * 4;
                            if (offset + 3 < result.Length)
                            {
                                if (dist2 >= innerBorder2)
                                {
                                    // 2px crisp white outer ring
                                    result[offset] = 255;
                                    result[offset + 1] = 255;
                                    result[offset + 2] = 255;
                                    result[offset + 3] = 255;
                                }
                                else
                                {
                                    // Badge solid fill
                                    result[offset] = bgB;
                                    result[offset + 1] = bgG;
                                    result[offset + 2] = bgR;
                                    result[offset + 3] = 255;
                                }
                            }
                        }
                    }

                    // 2. Draw centered step number (1-99) in white pixels
                    RenderBadgeNumber(result, pixelDataOffset, srcWidth, srcHeight, isTopDown, cx, cy, badge.Number, r);
                }
            }

            return result;
        }
        catch (Exception)
        {
            return sourceBmpBytes;
        }
    }

    private static void RenderBadgeNumber(byte[] buffer, int offset, int width, int height, bool isTopDown, int cx, int cy, int number, int radius)
    {
        var text = Math.Clamp(number, 1, 99).ToString();
        int scale = radius >= 16 ? 2 : 1;
        int charW = 3 * scale;
        int charH = 5 * scale;
        int spacing = 1 * scale;
        int totalW = text.Length * charW + (text.Length - 1) * spacing;

        int startX = cx - totalW / 2;
        int startY = cy - charH / 2;

        for (int i = 0; i < text.Length; i++)
        {
            int digit = text[i] - '0';
            ushort glyph = GetDigitGlyph(digit);
            int digitX = startX + i * (charW + spacing);

            for (int gy = 0; gy < 5; gy++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    int bitIndex = 14 - (gy * 3 + gx);
                    if ((glyph & (1 << bitIndex)) != 0)
                    {
                        for (int sy = 0; sy < scale; sy++)
                        {
                            int py = startY + gy * scale + sy;
                            if (py < 0 || py >= height) continue;
                            int rowIndex = isTopDown ? py : (height - 1 - py);

                            for (int sx = 0; sx < scale; sx++)
                            {
                                int px = digitX + gx * scale + sx;
                                if (px < 0 || px >= width) continue;

                                int pixOffset = offset + (rowIndex * width + px) * 4;
                                if (pixOffset + 3 < buffer.Length)
                                {
                                    buffer[pixOffset] = 255;
                                    buffer[pixOffset + 1] = 255;
                                    buffer[pixOffset + 2] = 255;
                                    buffer[pixOffset + 3] = 255;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static ushort GetDigitGlyph(int digit)
    {
        // 3x5 matrix encoded as 15 bits
        return digit switch
        {
            0 => 0b111_101_101_101_111,
            1 => 0b010_110_010_010_111,
            2 => 0b111_001_111_100_111,
            3 => 0b111_001_111_001_111,
            4 => 0b101_101_111_001_001,
            5 => 0b111_100_111_001_111,
            6 => 0b111_100_111_101_111,
            7 => 0b111_001_010_010_010,
            8 => 0b111_101_111_101_111,
            9 => 0b111_101_111_001_111,
            _ => 0b000_000_000_000_000
        };
    }
}
