using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace MemReduct.Fluent;

internal static class TrayGlyph
{
    // The caller owns the returned HICON; all temporary GDI objects stay local.
    internal static IntPtr Create(int? percent, int size, uint rgb, out byte[] pixels, int animationFrame = -1)
    {
        if (percent is < 0 or > 100 || size is < 16 or > 256 || animationFrame is < -1 or > 11) throw new ArgumentOutOfRangeException();
        var inset = animationFrame < 0 ? 0 : Math.Max(2, size / 8);
        var dc = Handle(CreateCompatibleDC(IntPtr.Zero));
        IntPtr bitmap = IntPtr.Zero, mask = IntPtr.Zero, font = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero, oldFont = IntPtr.Zero;
        try
        {
            var info = new BitmapInfo { Size = 40, Width = size, Height = -size, Planes = 1, BitCount = 32 };
            bitmap = Handle(CreateDIBSection(dc, ref info, 0, out var bits, IntPtr.Zero, 0));
            pixels = new byte[size * size * 4];
            Marshal.Copy(pixels, 0, bits, pixels.Length);
            oldBitmap = Handle(SelectObject(dc, bitmap));
            var text = percent?.ToString(CultureInfo.InvariantCulture) ?? "?";
            for (var height = size - inset * 2; height > 0; height--)
            {
                font = Handle(CreateFont(-height, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 4, 0, "Segoe UI"));
                oldFont = Handle(SelectObject(dc, font));
                if (!GetTextExtentPoint32(dc, text, text.Length, out var extent)) throw new Win32Exception();
                if (extent.X <= size - inset * 2 && extent.Y <= size - inset * 2) break;
                SelectObject(dc, oldFont); oldFont = IntPtr.Zero;
                DeleteObject(font); font = IntPtr.Zero;
            }
            SetBkMode(dc, 1);
            SetTextColor(dc, 0x00ffffff);
            var rect = new Rect { Left = inset, Top = inset, Right = size - inset, Bottom = size - inset };
            if (DrawText(dc, text, text.Length, ref rect, 1 | 4 | 0x20 | 0x800) == 0) throw new Win32Exception();
            GdiFlush();
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            if (animationFrame >= 0)
            {
                var phase = animationFrame * Math.PI / 6;
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5 - size / 2.0;
                    var dy = y + 0.5 - size / 2.0;
                    var trail = (phase - Math.Atan2(dy, dx) + Math.PI * 4) % (Math.PI * 2);
                    if (trail > Math.PI * 1.4) continue;
                    var fade = 1 - trail / (Math.PI * 1.4);
                    var radius = size / 2.0 - 1 - (1 - fade) * inset * 0.6;
                    var coverage = Math.Clamp(size / 32.0 + 0.75 - Math.Abs(Math.Sqrt(dx * dx + dy * dy) - radius), 0, 1);
                    var alpha = (byte)(255 * coverage * (0.2 + 0.8 * fade));
                    var index = (y * size + x) * 4;
                    pixels[index] = pixels[index + 1] = pixels[index + 2] = Math.Max(pixels[index], alpha);
                }
            }
            // Grayscale coverage becomes alpha; premultiplication avoids dark fringes on a light taskbar.
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i];
                pixels[i] = (byte)((rgb & 255) * alpha / 255);
                pixels[i + 1] = (byte)(((rgb >> 8) & 255) * alpha / 255);
                pixels[i + 2] = (byte)(((rgb >> 16) & 255) * alpha / 255);
                pixels[i + 3] = alpha;
            }
            Marshal.Copy(pixels, 0, bits, pixels.Length);
            SelectObject(dc, oldBitmap); oldBitmap = IntPtr.Zero;
            mask = Handle(CreateBitmap(size, size, 1, 1, new byte[((size + 15) / 16) * 2 * size]));
            var icon = new IconInfo { IsIcon = true, Color = bitmap, Mask = mask };
            return Handle(CreateIconIndirect(ref icon));
        }
        finally
        {
            if (oldFont != IntPtr.Zero) SelectObject(dc, oldFont);
            if (oldBitmap != IntPtr.Zero) SelectObject(dc, oldBitmap);
            if (font != IntPtr.Zero) DeleteObject(font);
            if (mask != IntPtr.Zero) DeleteObject(mask);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            DeleteDC(dc);
        }
    }

    private static IntPtr Handle(IntPtr value) => value == IntPtr.Zero || value == new IntPtr(-1)
        ? throw new Win32Exception(Marshal.GetLastWin32Error()) : value;

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, ImageSize; public int XPels, YPels; public uint Used, Important, Color;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Size { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo { [MarshalAs(UnmanagedType.Bool)] public bool IsIcon; public uint X, Y; public IntPtr Mask, Color; }
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bits, byte[] pixels);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateFontW", SetLastError = true)]
    private static extern IntPtr CreateFont(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeout, uint charset, uint output, uint clip, uint quality, uint pitch, string face);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetTextExtentPoint32W", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetTextExtentPoint32(IntPtr dc, string text, int length, out Size size);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr dc, int mode);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(IntPtr dc, uint color);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GdiFlush();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DrawTextW", SetLastError = true)]
    private static extern int DrawText(IntPtr dc, string text, int length, ref Rect rect, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr CreateIconIndirect(ref IconInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyIcon(IntPtr icon);
}
