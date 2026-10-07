using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// Light carried wavelength by wavelength: colors turned into spectra and back, what that changes and
/// what it must not, and the sky handed over as it was worked out.
/// </summary>
[TestClass]
public class TestBandSpectrum
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"bands-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// A gray is a flat spectrum, needing nothing of the table, and comes back as itself exactly --
    /// white above all, since a white light on a white wall must stay white.
    /// </summary>
    [TestMethod]
    public void TestGraysComeBackAsThemselves()
    {
        SpectralBasis basis = SpectralBasis.Shared;
        double[] bands = new double[basis.Count];

        foreach (double gray in new[] { 0, 0.001, 0.18, 0.5, 0.73, 1 })
        {
            basis.Uplift(new Color(gray, gray, gray), true, bands);

            foreach (double amount in bands)
                Assert.AreEqual(gray, amount, 1e-12, $"a gray of {gray} should be flat");

            AssertColor(new Color(gray, gray, gray), basis.ToColor(bands, 1), 1e-12, $"a gray of {gray}");

            basis.Uplift(new Color(3 * gray, 3 * gray, 3 * gray), false, bands);

            foreach (double amount in bands)
                Assert.AreEqual(3 * gray, amount, 1e-12, $"a light of {3 * gray}");
        }
    }

    /// <summary>
    /// A color goes in and comes back out as itself.  The table alone misses by a level of eight bits
    /// for about one color in ten; the correction after it makes that up exactly.  Kept clear of the
    /// most saturated corners, which no smooth curve can reach.
    /// </summary>
    [TestMethod]
    public void TestColorsComeBackAsThemselves()
    {
        Random random = new (11);
        SpectralBasis basis = SpectralBasis.Shared;
        double[] bands = new double[basis.Count];

        for (int trial = 0; trial < 2000; trial++)
        {
            Color color = new (
                0.05 + 0.9 * random.NextDouble(), 0.05 + 0.9 * random.NextDouble(),
                0.05 + 0.9 * random.NextDouble());

            basis.Uplift(color, true, bands);
            AssertColor(color, basis.ToColor(bands, 1), 1e-9, "as a surface");

            basis.Uplift(color * 4, false, bands);
            AssertColor(color * 4, basis.ToColor(bands, 1), 1e-9, "as a light");
        }
    }

    /// <summary>
    /// Why there are thirty-two bands.  The purest red sRGB has is a color a surface can have when it
    /// is seen in bands twelve and a half nanometers wide, and it comes back as itself.  Seen in bands
    /// twenty-five wide it is no surface's color at all -- a linear program over every possible
    /// reflectance says so, not just the fitting -- and it comes back pink.  Rendered, a scene's
    /// <c>Red</c> did exactly that.
    /// </summary>
    [TestMethod]
    public void TestThePurestRedNeedsThirtyTwoBands()
    {
        Color red = new (1, 0, 0);
        SpectralBasis sixteen = new (16);
        double[] bands = new double[SpectralColor.Bands];

        SpectralBasis.Shared.Uplift(red, true, bands);
        AssertColor(red, SpectralBasis.Shared.ToColor(bands, 1), 0.002, "red in thirty-two bands");

        sixteen.Uplift(red, true, bands);

        Color pink = sixteen.ToColor(bands, 1);

        Assert.IsTrue(pink.Blue > 0.02, $"red in sixteen bands came back as {pink}, not pink");
    }

    /// <summary>
    /// A surface never gives back more than all of the light at any wavelength, nor less than none, and
    /// a light is never negative -- even for the saturated colors whose round trip has to give way.
    /// </summary>
    [TestMethod]
    public void TestASpectrumIsAlwaysAPossibleOne()
    {
        Random random = new (13);
        SpectralBasis basis = SpectralBasis.Shared;
        double[] bands = new double[basis.Count];

        for (int trial = 0; trial < 5000; trial++)
        {
            Color color = new (random.NextDouble(), random.NextDouble(), random.NextDouble());

            basis.Uplift(color, true, bands);

            foreach (double amount in bands)
                Assert.IsTrue(amount is >= 0 and <= 1, $"{color} reflects {amount}");

            basis.Uplift(color * 5, false, bands);

            foreach (double amount in bands)
                Assert.IsTrue(amount >= 0, $"a light of {color * 5} gives {amount}");
        }
    }

    /// <summary>
    /// Turning a light up changes how much light there is and not what color it is, so its spectrum
    /// keeps its shape at any brightness -- which a surface's need not, a dark red paint and a bright
    /// one being different paints.
    /// </summary>
    [TestMethod]
    public void TestALightKeepsItsShapeAtAnyBrightness()
    {
        SpectralBasis basis = SpectralBasis.Shared;
        double[] dim = new double[basis.Count];
        double[] bright = new double[basis.Count];
        Color warm = new (1, 0.8, 0.5);

        basis.Uplift(warm * 0.3, false, dim);
        basis.Uplift(warm * 7, false, bright);

        for (int band = 0; band < basis.Count; band++)
            Assert.AreEqual(dim[band] * 7 / 0.3, bright[band], 1e-9 * bright[band] + 1e-12);
    }

    /// <summary>
    /// The whole point.  Channel by channel, a yellow light leaves a blue paint with no blue at all,
    /// since the light has no blue channel.  But a real yellow light is a broad hump across green and
    /// yellow whose short side reaches into the blue-green, and a real blue paint still reflects some
    /// of that, so carried by wavelength a little blue survives.
    /// </summary>
    [TestMethod]
    public void TestColoredLightOnColoredPaintIsNotChannelByChannel()
    {
        Color yellow = new (1, 1, 0);
        Color blue = new (0.05, 0.2, 0.9);
        Color channelByChannel =
            (RgbSpectrum.FromIlluminant(yellow) * RgbSpectrum.FromReflectance(blue)).ToColor();
        Color byWavelength =
            (BandSpectrum.FromIlluminant(yellow) * BandSpectrum.FromReflectance(blue)).ToColor();

        Assert.AreEqual(0, channelByChannel.Blue, 1e-12);
        Assert.IsTrue(byWavelength.Blue > 0.03,
            $"by wavelength {byWavelength} should keep some blue, where channel by channel " +
            $"{channelByChannel} keeps none");

        // Under white light nothing changes: white is a flat spectrum and multiplies nothing away.
        Color underWhite =
            (BandSpectrum.FromIlluminant(Colors.White) * BandSpectrum.FromReflectance(blue)).ToColor();

        AssertColor(blue, underWhite, 1e-9, "a paint under white light");
    }

    /// <summary>
    /// The sky is worked out wavelength by wavelength, and a spectral render takes it that way; what it
    /// comes to must still be the sky an ordinary render shows.
    /// </summary>
    [TestMethod]
    public void TestTheSkyIsHandedOverAsItWasWorkedOut()
    {
        PhysicalSkyPigment sky = new () { SunElevation = 20 };

        Assert.IsTrue(sky.IsSpectral);

        foreach (Vector direction in new[]
                 {
                     new Vector(0, 1, 0), new Vector(1, 0.3, 0), new Vector(-1, 0.05, 0.4),
                     new Vector(0.2, 0.1, -1), new Vector(0, -0.5, 1)
                 })
        {
            Vector unit = direction.Unit;
            Point toward = new (unit.X, unit.Y, unit.Z);
            Color asColor = sky.GetTransformedColorFor(toward);
            Color asLight = sky.GetTransformedLightFor<BandSpectrum>(toward).ToColor();

            AssertColor(asColor, asLight, 0.02 * Brightest(asColor), $"toward {unit}");
        }
    }

    /// <summary>
    /// The sun a physical sky hangs carries its spectrum, and loses it the moment its color is set to
    /// something else, so that the two never describe different light.
    /// </summary>
    [TestMethod]
    public void TestTheSunCarriesItsSpectrum()
    {
        DistantLight sun = new PhysicalSkyPigment { SunElevation = 30 }.SunAsALight();

        Assert.IsNotNull(sun.Spectrum);
        AssertColor(sun.Color, sun.Emitted<BandSpectrum>().ToColor(), 0.02 * Brightest(sun.Color), "the sun");

        sun.Color = Colors.White;

        Assert.IsNull(sun.Spectrum, "setting the color should let go of the spectrum");
    }

    /// <summary>
    /// A spectrum worked out in the sky's bands is taken as it is, those being the render's bands too.
    /// </summary>
    [TestMethod]
    public void TestASkySpectrumIsTakenAsItIs()
    {
        double[] perBand = Enumerable.Range(0, SpectralColor.Bands).Select(index => (double) index).ToArray();
        BandSpectrum light = BandSpectrum.FromSampled(perBand);

        Assert.AreEqual(SpectralColor.Bands, BandSpectrum.Count);

        for (int band = 0; band < SpectralColor.Bands; band++)
            Assert.AreEqual(band, light[band]);

        Assert.AreEqual(1, light.Alpha);
    }

    /// <summary>
    /// The command line asks for spectral light with `--spectral`, and as a switch it goes one way:
    /// giving it turns spectral light on, and leaving it off says nothing about what a scene asked for.
    /// </summary>
    [TestMethod]
    public void TestSpectralIsAskedForFromTheCommandLine()
    {
        RenderContext context = new ();

        context.ApplyOptions(new RenderOptions(), 0);
        Assert.IsFalse(context.Spectral, "said nowhere, light is red, green and blue");

        context = new RenderContext();
        context.ApplyOptions(new RenderOptions { Spectral = true }, 0);
        Assert.IsTrue(context.Spectral);

        context = new RenderContext { Spectral = true };
        context.ApplyOptions(new RenderOptions(), 0);
        Assert.IsTrue(context.Spectral, "a scene's asking for it should survive a silent command line");
    }

    /// <summary>
    /// A scene asks for it in its context block, and it reaches the render: a yellow lamp on a blue
    /// ball keeps some blue, and a white lamp on a gray one comes out as it always did.
    /// </summary>
    [TestMethod]
    public void TestASceneAsksForSpectralLight()
    {
        Color plain = Rendered("", "[1, 1, 0]", "[0.05, 0.2, 0.9]");
        Color spectral = Rendered("spectral", "[1, 1, 0]", "[0.05, 0.2, 0.9]");

        Assert.AreEqual(0, plain.Blue, 1e-12, $"red, green and blue {plain} should keep no blue");
        Assert.IsTrue(spectral.Blue > 0.03, $"spectral {spectral} should keep some blue");

        // A gray of a half would land on the edge between two levels of eight bits, where a hair either
        // way decides the level, so the gray is kept off it; one level apart is as near as a file holds.
        AssertColor(Rendered("", "[1, 1, 1]", "[0.4, 0.4, 0.4]"),
            Rendered("spectral", "[1, 1, 1]", "[0.4, 0.4, 0.4]"), 1.5 / 255,
            "a gray ball under white light");
    }

    /// <summary>
    /// This method renders a ball of the given color under a lamp of the given color and hands back
    /// the color at the middle of the picture.
    /// </summary>
    private Color Rendered(string context, string lamp, string paint)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path,
            $"context {{ no gamma {context} }}\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            $"point light {{ location [0, 0, -10]  color {lamp} }}\n" +
            $"sphere {{ material {{ pigment {paint}  ambient 0  specular 0  diffuse 1 }} }}");

        new LanguageParser(path).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = 9, Height = 9, ProgressStyleText = "none"
        });

        return new ImageFile(output).Load()[0].GetPixel(4, 4);
    }

    /// <summary>
    /// This method returns a color's brightest channel.
    /// </summary>
    private static double Brightest(Color color)
    {
        return Math.Max(color.Red, Math.Max(color.Green, color.Blue));
    }

    /// <summary>
    /// This method asserts that two colors match, channel by channel, to within a tolerance.
    /// </summary>
    private static void AssertColor(Color expected, Color actual, double tolerance, string what)
    {
        Assert.AreEqual(expected.Red, actual.Red, tolerance, $"{what}: red, {actual} for {expected}");
        Assert.AreEqual(expected.Green, actual.Green, tolerance, $"{what}: green, {actual} for {expected}");
        Assert.AreEqual(expected.Blue, actual.Blue, tolerance, $"{what}: blue, {actual} for {expected}");
    }
}
