using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Calculator.UI.Tests;

/// <summary>
/// The logo drawing (Resources/Logo.axaml). When BTL_LOGO names a folder, the test also writes the logo as PNG files and
/// as the Windows icon (BtlCalculator.ico, see Calculator.Desktop.csproj) and the Store package pictures (Package/) into it.
/// </summary>
public sealed class LogoTests
{
    private static readonly int[] IconSizes = [16, 24, 32, 48, 64, 128, 256];

    [AvaloniaFact]
    public void LogoIsDrawn()
    {
        const int size = 64;
        using RenderTargetBitmap bitmap = Render(size);
        byte[] pixels = new byte[size * size * 4];
        nint buffer = Marshal.AllocHGlobal(pixels.Length);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size, size), buffer, pixels.Length, size * 4);
            Marshal.Copy(buffer, pixels, 0, pixels.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        // The corners stay transparent, the middle (the body of the calculator) is painted.
        Assert.Equal(0, pixels[3]);
        Assert.Equal(255, pixels[(((size / 2 * size) + (size / 2)) * 4) + 3]);
    }

    [AvaloniaFact]
    public void ExportLogo()
    {
        string? folder = Environment.GetEnvironmentVariable("BTL_LOGO");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        foreach (int size in new[] { 512, 1024 })
        {
            using RenderTargetBitmap bitmap = Render(size);
            bitmap.Save(Path.Combine(folder, $"logo-{size}.png"), new PngBitmapEncoderOptions());
        }

        WriteIcon(Path.Combine(folder, "BtlCalculator.ico"));

        // The pictures of the Store package (Calculator.Desktop/Package/Assets), at 200% of their nominal size.
        string package = Path.Combine(folder, "Package");
        Directory.CreateDirectory(package);
        foreach ((string name, int width, int height) in new[]
        {
            ("StoreLogo", 100, 100),
            ("Square44x44Logo", 88, 88),
            ("Square150x150Logo", 300, 300),
            ("Wide310x150Logo", 620, 300),
        })
        {
            using RenderTargetBitmap bitmap = Render(width, height);
            bitmap.Save(Path.Combine(package, name + ".png"), new PngBitmapEncoderOptions());
        }
    }

    private static RenderTargetBitmap Render(int size) => Render(size, size);

    /// <summary>The logo as big as fits, in the middle of the picture; the rest stays transparent.</summary>
    private static RenderTargetBitmap Render(int width, int height)
    {
        Assert.True(Application.Current!.TryGetResource("AppLogo", null, out object? resource));
        var logo = Assert.IsAssignableFrom<IImage>(resource);

        int size = Math.Min(width, height);
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        using (DrawingContext context = bitmap.CreateDrawingContext())
        {
            logo.Draw(context, new Rect(logo.Size), new Rect((width - size) / 2, (height - size) / 2, size, size));
        }

        return bitmap;
    }

    /// <summary>An .ico file with a PNG picture for each size (Windows Vista and later read PNG entries).</summary>
    private static void WriteIcon(string path)
    {
        var pictures = new List<byte[]>();
        foreach (int size in IconSizes)
        {
            using RenderTargetBitmap bitmap = Render(size);
            using var stream = new MemoryStream();
            bitmap.Save(stream, new PngBitmapEncoderOptions());
            pictures.Add(stream.ToArray());
        }

        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((short)0); // reserved
        writer.Write((short)1); // icon
        writer.Write((short)pictures.Count);

        int offset = 6 + (16 * pictures.Count);
        for (int i = 0; i < pictures.Count; i++)
        {
            int size = IconSizes[i];
            writer.Write((byte)(size >= 256 ? 0 : size)); // width, 0 means 256
            writer.Write((byte)(size >= 256 ? 0 : size)); // height
            writer.Write((byte)0); // no palette
            writer.Write((byte)0); // reserved
            writer.Write((short)1); // color planes
            writer.Write((short)32); // bits per pixel
            writer.Write(pictures[i].Length);
            writer.Write(offset);
            offset += pictures[i].Length;
        }

        foreach (byte[] picture in pictures)
        {
            writer.Write(picture);
        }
    }
}
