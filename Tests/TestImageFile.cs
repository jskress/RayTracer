using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;

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
        // Pure black and pure white are fixed points of gamma correction (0^p = 0 and
        // 1^p = 1 for any power p), so they round-trip cleanly even with the default
        // gamma settings applied, unlike a mid-range gray would.
        _tempFile = Path.Combine(Path.GetTempPath(), $"raytracer-test-{Guid.NewGuid():N}.png");

        Canvas canvas = new (2, 1);

        canvas.SetColor(new Color(0, 0, 0), 0, 0);
        canvas.SetColor(new Color(1, 1, 1), 1, 0);

        RenderContext context = new ();
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
}
