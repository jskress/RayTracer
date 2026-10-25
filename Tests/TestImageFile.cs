using ImageMagick;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;

namespace Tests;

[TestClass]
public class TestImageFile
{
    private string _tempFile;

    [TestCleanup]
    public void Cleanup()
    {
        if (_tempFile != null && File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    /// <summary>
    /// This helper compares two colors with a tolerance wide enough to absorb the
    /// quantization that happens when a color is scaled down to an integer channel value
    /// and back, rather than the razor-thin tolerance <c>Color.Matches()</c> uses.
    /// </summary>
    private static void AssertColorsClose(Color expected, Color actual, double tolerance = 0.01)
    {
        Assert.AreEqual(expected.Red, actual.Red, tolerance, "Red channel mismatch.");
        Assert.AreEqual(expected.Green, actual.Green, tolerance, "Green channel mismatch.");
        Assert.AreEqual(expected.Blue, actual.Blue, tolerance, "Blue channel mismatch.");
        Assert.AreEqual(expected.Alpha, actual.Alpha, tolerance, "Alpha channel mismatch.");
    }

    [TestMethod]
    public void TestSaveCreatesAFile()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"raytracer-test-{Guid.NewGuid():N}.png");

        Canvas canvas = new (1, 1);
        RenderContext context = new ();
        ImageFile file = new (_tempFile);

        canvas.SetColor(new Color(1, 1, 1), 0, 0);

        file.Save(canvas, context);

        Assert.IsTrue(File.Exists(_tempFile));
    }

    [TestMethod]
    public void TestSaveAndLoadRoundTrip()
    {
        // Gamma correction is disabled so the channel scaling is a straight linear
        // quantization, which keeps the expected round-tripped values easy to reason about.
        _tempFile = Path.Combine(Path.GetTempPath(), $"raytracer-test-{Guid.NewGuid():N}.png");

        Canvas canvas = new (2, 2);

        canvas.SetColor(new Color(1, 0, 0), 0, 0);
        canvas.SetColor(new Color(0, 1, 0), 1, 0);
        canvas.SetColor(new Color(0, 0, 1, 0.5), 0, 1);
        canvas.SetColor(new Color(1, 1, 1), 1, 1);

        RenderContext context = new () { ApplyGamma = false };
        ImageFile file = new (_tempFile);

        file.Save(canvas, context);

        Canvas[] loaded = file.Load();

        Assert.AreEqual(1, loaded.Length);
        Assert.AreEqual(2, loaded[0].Width);
        Assert.AreEqual(2, loaded[0].Height);

        AssertColorsClose(new Color(1, 0, 0), loaded[0].GetPixel(0, 0));
        AssertColorsClose(new Color(0, 1, 0), loaded[0].GetPixel(1, 0));
        AssertColorsClose(new Color(0, 0, 1, 0.5), loaded[0].GetPixel(0, 1));
        AssertColorsClose(new Color(1, 1, 1), loaded[0].GetPixel(1, 1));
    }

    [TestMethod]
    public void TestSaveAndLoadRoundTripWithGammaCorrection()
    {
        // Pure black and pure white are fixed points of every curve a picture is written with,
        // so they round-trip cleanly even with the default curve applied, unlike a mid-range
        // gray would.  Developed by a film curve, white is not white, so none is asked for.
        _tempFile = Path.Combine(Path.GetTempPath(), $"raytracer-test-{Guid.NewGuid():N}.png");

        Canvas canvas = new (2, 1);

        canvas.SetColor(new Color(0, 0, 0), 0, 0);
        canvas.SetColor(new Color(1, 1, 1), 1, 0);

        RenderContext context = new () { FilmCurve = FilmCurve.None };
        ImageFile file = new (_tempFile);

        file.Save(canvas, context);

        Canvas[] loaded = file.Load();

        AssertColorsClose(new Color(0, 0, 0), loaded[0].GetPixel(0, 0));
        AssertColorsClose(new Color(1, 1, 1), loaded[0].GetPixel(1, 0));
    }

    [TestMethod]
    public void TestSaveAndLoadRoundTripWithFullyOpaqueLowColorVarietyImage()
    {
        // Regression test: left to its own heuristics, Magick.NET's PNG writer will
        // opportunistically shrink a fully-opaque, low-color-variety image down to a
        // palette or gray+alpha encoding.  ImageFile.Load() reads pixels back assuming a
        // true RGB(A) layout, so those alternate encodings used to cause the alpha channel
        // to come back near zero (fully transparent) instead of the correct, fully-opaque
        // 1.0 -- even though every source pixel was opaque.  A three-color, fully-opaque
        // canvas like this one is exactly the shape that used to trigger it.
        _tempFile = Path.Combine(Path.GetTempPath(), $"raytracer-test-{Guid.NewGuid():N}.png");

        Canvas canvas = new (3, 1);

        canvas.SetColor(new Color(0, 0, 0), 0, 0);
        canvas.SetColor(new Color(1, 1, 1), 1, 0);
        canvas.SetColor(new Color(1, 0, 0), 2, 0);

        RenderContext context = new () { ApplyGamma = false };
        ImageFile file = new (_tempFile);

        file.Save(canvas, context);

        Canvas[] loaded = file.Load();

        AssertColorsClose(new Color(0, 0, 0), loaded[0].GetPixel(0, 0));
        AssertColorsClose(new Color(1, 1, 1), loaded[0].GetPixel(1, 0));
        AssertColorsClose(new Color(1, 0, 0), loaded[0].GetPixel(2, 0));
    }

    [TestMethod]
    public void TestAnOpaqueImageIsWrittenWithoutAnAlphaChannel()
    {
        // An image that is opaque everywhere has no use for a fourth channel, and writing one costs
        // a quarter of the file to carry nothing but 255s.  This was the intent all along; it was
        // lost when the color type came to be stated unconditionally as TrueColorAlpha in order to
        // stop Magick.NET choosing a palette encoding of its own.  Both wants are satisfied by
        // stating the color type either way and picking which from the image's own content.
        Assert.AreEqual(ColorType.TrueColor, SavedPngColorType(opaque: true));
    }

    [TestMethod]
    public void TestAnImageWithAnyTransparencyKeepsItsAlphaChannel()
    {
        // The other side of it, and the half that must not be traded away for the first: one pixel
        // short of opaque anywhere in the image is enough to need the channel that records it.
        Assert.AreEqual(ColorType.TrueColorAlpha, SavedPngColorType(opaque: false));
    }

    /// <summary>
    /// Saves a small image, opaque or not as asked, and reports the color type the PNG was written
    /// with.
    /// </summary>
    private static ColorType SavedPngColorType(bool opaque)
    {
        string path = Path.Combine(Path.GetTempPath(), $"raytracer-type-{Guid.NewGuid():N}.png");

        try
        {
            Canvas canvas = new (2, 1);

            canvas.SetColor(new Color(0.3, 0.6, 0.9), 0, 0);
            canvas.SetColor(opaque ? new Color(1, 1, 1) : new Color(1, 1, 1, 0.5), 1, 0);

            new ImageFile(path).Save(canvas, new RenderContext { ApplyGamma = false });

            using MagickImage image = new (path);

            return image.ColorType;
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void TestThePngIsWrittenAtTheConfiguredChannelDepth()
    {
        // The image carries exactly the precision it was rendered with: an 8-bit render writes an
        // 8-bit PNG, not 8-bit data padded into a 16-bit container.  A wider container was once
        // forced to keep the round trip lossless; the pixels round-trip cleanly at either depth
        // now, which the tests above cover at the default 8 bits.
        Assert.AreEqual(8u, SavedPngDepth(8), "eight bits per channel should write an 8-bit PNG");
        Assert.AreEqual(16u, SavedPngDepth(16), "sixteen bits per channel should write a 16-bit PNG");
    }

    /// <summary>
    /// Saves a small image at the given channel depth and reports the bit depth the PNG was
    /// actually written at.
    /// </summary>
    private static uint SavedPngDepth(int bitsPerChannel)
    {
        string path = Path.Combine(Path.GetTempPath(), $"raytracer-depth-{Guid.NewGuid():N}.png");

        try
        {
            Canvas canvas = new (2, 1);

            canvas.SetColor(new Color(0.3, 0.6, 0.9), 0, 0);
            canvas.SetColor(new Color(1, 1, 1, 0.5), 1, 0);

            new ImageFile(path).Save(canvas,
                new RenderContext { BitsPerChannel = bitsPerChannel, ApplyGamma = false });

            using MagickImage image = new (path);

            return image.Depth;
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// **Everything written must be readable, and nothing may be written as a palette.**  Those are
    /// the two rules the writer has to keep, and they are easy to break by accident: left to choose
    /// for itself, Magick.NET's PNG writer will shrink a low-color-variety image to a palette or a
    /// gray encoding, and an image read back from a palette is not the image that was written --
    /// its colors have become indices into a table.
    /// <para>
    /// So every combination the writer can produce is saved, checked for the container it was meant
    /// to get, and read back through our own loader to see that it came home.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestEveryImageWrittenCanBeReadAndNoneIsAPalette()
    {
        foreach (int depth in new[] { 8, 16 })
        {
            foreach (bool opaque in new[] { true, false })
            {
                foreach (bool grayscale in new[] { true, false })
                {
                    string what = $"{depth} bits, {(opaque ? "opaque" : "transparent")}, " +
                                  $"{(grayscale ? "gray" : "color")}";
                    string path = Path.Combine(
                        Path.GetTempPath(), $"raytracer-round-{Guid.NewGuid():N}.png");

                    try
                    {
                        RenderContext context = new ()
                        {
                            BitsPerChannel = depth, ApplyGamma = false, Grayscale = grayscale
                        };
                        Canvas canvas = new (2, 1);
                        Color first = new (0.3, 0.6, 0.9);
                        // A canvas holds a pixel as its samples add up, so the half-covered one is half its color:
                        // (1, 0.25, 0.5) half there.
                        Color second = opaque ? new Color(1, 0.25, 0.5) : new Color(0.5, 0.125, 0.25, 0.5);

                        canvas.SetColor(first, 0, 0);
                        canvas.SetColor(second, 1, 0);

                        new ImageFile(path).Save(canvas, context);

                        using (MagickImage written = new (path))
                        {
                            Assert.AreNotEqual(ColorType.Palette, written.ColorType,
                                $"{what}: a palette was written");
                            Assert.AreNotEqual(ColorType.PaletteAlpha, written.ColorType,
                                $"{what}: a palette was written");

                            ColorType expected = grayscale
                                ? opaque ? ColorType.Grayscale : ColorType.GrayscaleAlpha
                                : opaque ? ColorType.TrueColor : ColorType.TrueColorAlpha;

                            Assert.AreEqual(expected, written.ColorType, $"{what}: wrong container");
                            Assert.AreEqual((uint) depth, written.Depth, $"{what}: wrong depth");
                        }

                        // And back again, through the loader the rest of the renderer uses.
                        Canvas reloaded = new ImageFile(path).Load()[0];

                        Assert.AreEqual(2, reloaded.Width, $"{what}: width");
                        Assert.AreEqual(1, reloaded.Height, $"{what}: height");

                        // A file keeps the color apart from how much of the pixel it covers, so the half-covered
                        // pixel reads back as its own color, half there.
                        foreach ((Color was, int x) in new[] { (first, 0), (second, 1) })
                        {
                            Color straight = was.Alpha < 1
                                ? new Color(was.Red / was.Alpha, was.Green / was.Alpha, was.Blue / was.Alpha, was.Alpha)
                                : was;
                            Color wanted = grayscale ? straight.AsGray() : straight;
                            Color got = reloaded.GetPixel(x, 0);

                            AssertColorsClose(wanted, got);

                            Assert.AreEqual(was.Alpha, got.Alpha, 0.01, $"{what}: alpha at {x}");
                        }
                    }
                    finally
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                }
            }
        }
    }

    /// <summary>
    /// A gray image really is gray -- the writer weighs each color down to the one value that stands
    /// for its brightness, rather than leaving three channels that merely happen to agree.
    /// </summary>
    [TestMethod]
    public void TestAGrayImageHasNoColorLeftInIt()
    {
        string path = Path.Combine(Path.GetTempPath(), $"raytracer-gray-{Guid.NewGuid():N}.png");

        try
        {
            Canvas canvas = new (1, 1);

            canvas.SetColor(new Color(0.9, 0.2, 0.1), 0, 0);

            new ImageFile(path).Save(canvas,
                new RenderContext { ApplyGamma = false, Grayscale = true });

            Color loaded = new ImageFile(path).Load()[0].GetPixel(0, 0);

            Assert.AreEqual(loaded.Red, loaded.Green, 1e-6, "red and green should agree");
            Assert.AreEqual(loaded.Green, loaded.Blue, 1e-6, "green and blue should agree");

            // 0.9 * 0.299 + 0.2 * 0.587 + 0.1 * 0.114 -- the weights the eye wants, not a mean.
            Assert.AreEqual(0.398, loaded.Red, 0.01, "the gray should be the weighted brightness");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void TestSaveWithImageInformationDoesNotThrow()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"raytracer-test-{Guid.NewGuid():N}.png");

        Canvas canvas = new (1, 1);
        RenderContext context = new ();
        ImageFile file = new (_tempFile);
        ImageInformation info = new ()
        {
            Title = "Test Image",
            Author = "Tests",
            Description = "A test image.",
            Comment = "Created by TestImageFile."
        };

        canvas.SetColor(new Color(0.5, 0.5, 0.5), 0, 0);

        file.Save(canvas, context, info);

        Assert.IsTrue(File.Exists(_tempFile));
    }

    /// <summary>
    /// This tests that a picture file is known to hold sRGB values rather than light.  PNG and JPEG
    /// files say so of themselves, and it is what lets a pigment turn them back into light.
    /// </summary>
    [TestMethod]
    public void TestAPictureFileIsKnownToHoldSrgb()
    {
        foreach (string extension in new[] { "png", "jpg" })
        {
            string path = Path.Combine(Path.GetTempPath(), $"raytracer-srgb-{Guid.NewGuid():N}.{extension}");

            try
            {
                using (MagickImage image = new (MagickColors.Gray, 2, 2))
                    image.Write(path);

                Canvas canvas = new ImageFile(path).Load()[0];

                Assert.AreSame(TransferFunction.Srgb, canvas.Encoding, extension);
                Assert.AreEqual(128 / 255.0, canvas.GetPixel(0, 0).Red, 0.01, "the values are read as they stand");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }

    /// <summary>
    /// This tests that a Radiance or OpenEXR file is known to hold light, and that light brighter than
    /// white survives being read -- which is what lets a picture of a real sky, sun and all, light a
    /// scene.  OpenEXR files are reported as sRGB by Magick.NET, with a gamma of one; the format settles
    /// it.
    /// </summary>
    [TestMethod]
    public void TestAHighDynamicRangeFileHoldsLight()
    {
        foreach (MagickFormat format in new[] { MagickFormat.Hdr, MagickFormat.Exr })
        {
            string path = Path.Combine(
                Path.GetTempPath(), $"raytracer-hdr-{Guid.NewGuid():N}.{format.ToString().ToLower()}");

            try
            {
                using (MagickImage image = new (MagickColors.Black, 2, 2))
                {
                    image.ColorSpace = ColorSpace.RGB;

                    using (IPixelCollection<float> pixels = image.GetPixels())
                    {
                        foreach (IPixel<float> pixel in pixels)
                        {
                            pixel.SetChannel(0, 3f * 65535);
                            pixel.SetChannel(1, 0.5f * 65535);
                            pixel.SetChannel(2, 0.25f * 65535);
                        }
                    }

                    image.Write(path, format);
                }

                Canvas canvas = new ImageFile(path).Load()[0];
                Color color = canvas.GetPixel(1, 1);

                Assert.AreSame(TransferFunction.Linear, canvas.Encoding, format.ToString());
                Assert.AreEqual(3, color.Red, 0.05, format.ToString());
                Assert.AreEqual(0.5, color.Green, 0.01, format.ToString());
                Assert.AreEqual(0.25, color.Blue, 0.01, format.ToString());
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }

    /// <summary>
    /// This tests that each curve a file can be written with writes what it reads: the sRGB curve, a plain
    /// power and light as it stands, and that sRGB gives its own numbers -- a straight run to a 2.4 power at
    /// 0.0031308, where a plain 2.2 power puts a thousandth of white at 0.043, half again what sRGB does.
    /// </summary>
    [TestMethod]
    public void TestEveryCurveWritesWhatItReads()
    {
        foreach (TransferFunction curve in new[] { TransferFunction.Srgb, TransferFunction.Power(1 / 1.8), TransferFunction.Linear })
        {
            for (int step = 0; step <= 100; step++)
            {
                double light = step / 100.0;

                Assert.AreEqual(light, curve.ToLinear(curve.FromLinear(light)), 1e-12, $"{curve} at {light}");
            }
        }

        Assert.AreEqual(0.001 * 12.92, TransferFunction.Srgb.FromLinear(0.001), 1e-12);
        Assert.AreEqual(1.055 * Math.Pow(0.5, 1 / 2.4) - 0.055, TransferFunction.Srgb.FromLinear(0.5), 1e-12);
        Assert.AreEqual(Math.Pow(0.5, 1 / 1.8), TransferFunction.Power(1 / 1.8).FromLinear(0.5), 1e-12);
        Assert.AreEqual(3, TransferFunction.Linear.FromLinear(3));
    }

    /// <summary>
    /// This tests which curve a render is written with: sRGB unless a gamma is given, a plain power when one
    /// is -- by a scene or on the command line, but not by a command line that says nothing -- and light as it
    /// stands with no gamma at all.
    /// </summary>
    [TestMethod]
    public void TestARenderIsWrittenWithTheSrgbCurveUnlessToldOtherwise()
    {
        Assert.AreSame(TransferFunction.Srgb, new RenderContext().OutputCurve);
        Assert.AreSame(TransferFunction.Linear, new RenderContext { ApplyGamma = false }.OutputCurve);
        Assert.AreSame(TransferFunction.Linear, new RenderContext { ApplyGamma = false, Gamma = 1.8 }.OutputCurve);

        RenderContext said = new () { Gamma = 1.8 };

        Assert.AreEqual(Math.Pow(0.5, 1 / 1.8), said.OutputCurve.FromLinear(0.5), 1e-12);

        RenderContext unsaid = new ();

        unsaid.ApplyOptions(new RenderOptions());

        Assert.AreSame(TransferFunction.Srgb, unsaid.OutputCurve, "a command line saying nothing says no gamma");

        unsaid.ApplyOptions(new RenderOptions { Gamma = 2.6 });

        Assert.AreEqual(Math.Pow(0.5, 1 / 2.6), unsaid.OutputCurve.FromLinear(0.5), 1e-12);
    }

    /// <summary>
    /// This tests that a picture is written with the sRGB curve, as every viewer reads one: deep darks, a
    /// mid-gray and white written to a 16-bit PNG come back as the sRGB values of their light, which the
    /// file is known to hold, and so as the light itself.  The curve is asked for alone, with no film curve
    /// developing the light first.
    /// </summary>
    [TestMethod]
    public void TestAPictureIsWrittenWithTheSrgbCurve()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"raytracer-test-{Guid.NewGuid():N}.png");

        double[] lights = [0.001, 0.05, 0.5, 1];
        Canvas canvas = new (lights.Length, 1);

        for (int x = 0; x < lights.Length; x++)
            canvas.SetColor(new Color(lights[x], lights[x], lights[x]), x, 0);

        new ImageFile(_tempFile).Save(canvas, new RenderContext { BitsPerChannel = 16, FilmCurve = FilmCurve.None });

        Canvas loaded = new ImageFile(_tempFile).Load()[0];

        Assert.AreSame(TransferFunction.Srgb, loaded.Encoding);

        for (int x = 0; x < lights.Length; x++)
        {
            double value = loaded.GetPixel(x, 0).Red;

            Assert.AreEqual(TransferFunction.Srgb.FromLinear(lights[x]), value, 1 / 65535.0, $"value of {lights[x]}");
            Assert.AreEqual(lights[x], loaded.Encoding.ToLinear(value), 1e-4 + 1e-4 * lights[x], $"light of {lights[x]}");
        }
    }

    /// <summary>
    /// This tests that a gamma given outright is written as the plain power it asks for, and that the file
    /// says so in a gAMA chunk -- a PNG that says nothing is read as sRGB -- so that it is read back as the
    /// light it was.
    /// </summary>
    [TestMethod]
    public void TestAGammaGivenOutrightIsDeclared()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"raytracer-test-{Guid.NewGuid():N}.png");

        Canvas canvas = new (2, 1);

        canvas.SetColor(new Color(0.2, 0.2, 0.2), 0, 0);
        canvas.SetColor(new Color(0.5, 0.5, 0.5), 1, 0);

        new ImageFile(_tempFile).Save(canvas, new RenderContext { Gamma = 1.8, BitsPerChannel = 16 });

        using (MagickImage image = new (_tempFile))
            Assert.AreEqual(1 / 1.8, image.Gamma, 1e-5, "the file should declare its gamma");

        Canvas loaded = new ImageFile(_tempFile).Load()[0];

        Assert.AreEqual(Math.Pow(0.5, 1 / 1.8), loaded.GetPixel(1, 0).Red, 1 / 65535.0);
        Assert.AreEqual(0.2, loaded.Encoding.ToLinear(loaded.GetPixel(0, 0).Red), 1e-4);
        Assert.AreEqual(0.5, loaded.Encoding.ToLinear(loaded.GetPixel(1, 0).Red), 1e-4);
    }

    /// <summary>
    /// This tests that a file holding light -- OpenEXR, Radiance or a portable float map -- is written with
    /// the light itself: brighter than white, the deepest darks and the colors as they are, neither curved
    /// for a screen, nor clamped, nor cut down to eight bits.  Each is held to its format's own precision:
    /// a half float's for OpenEXR, a shared exponent's for Radiance.
    /// </summary>
    [TestMethod]
    public void TestAFileOfLightHoldsTheLightItself()
    {
        // Two rows, since Magick.NET cannot read back an OpenEXR file one pixel high.
        Color[] colors = [new (3, 3, 3), new (0.001, 0.001, 0.001), new (0.5, 0.25, 0.125), new (12, 6, 0.75)];
        Canvas canvas = new (colors.Length, 2);

        for (int x = 0; x < colors.Length; x++)
        {
            canvas.SetColor(colors[x], x, 0);
            canvas.SetColor(colors[x], x, 1);
        }

        foreach ((string extension, double precision) in new[] { ("exr", 1e-3), ("hdr", 1e-2), ("pfm", 1e-6) })
        {
            string path = Path.Combine(Path.GetTempPath(), $"raytracer-light-{Guid.NewGuid():N}.{extension}");

            try
            {
                new ImageFile(path).Save(canvas, new RenderContext());

                Canvas loaded = new ImageFile(path).Load()[0];

                Assert.AreSame(TransferFunction.Linear, loaded.Encoding, extension);

                for (int x = 0; x < colors.Length; x++)
                {
                    Color wanted = colors[x], got = loaded.GetPixel(x, 0);
                    double brightest = Math.Max(wanted.Red, Math.Max(wanted.Green, wanted.Blue));

                    Assert.AreEqual(wanted.Red, got.Red, precision * brightest, $"{extension} red at {x}: {got}");
                    Assert.AreEqual(wanted.Green, got.Green, precision * brightest, $"{extension} green at {x}: {got}");
                    Assert.AreEqual(wanted.Blue, got.Blue, precision * brightest, $"{extension} blue at {x}: {got}");
                }
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
