using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// Lights given the temperature of a glow rather than a color: the glow itself, how a light is scaled
/// to it, the brightness that goes with it, and how a scene says both.
/// </summary>
[TestClass]
public class TestColorTemperature
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"kelvin-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// A glow stands at a strength of one seen as a color, whatever its temperature, which is what lets
    /// the sun be one.
    /// </summary>
    [TestMethod]
    public void TestAGlowStandsAtAStrengthOfOne()
    {
        foreach (double kelvin in new[] { 1500.0, 2700, 4000, 6500, 10000 })
        {
            Color color = SpectralColor.ToColor(SpectralColor.Blackbody(kelvin));

            Assert.AreEqual(1, 0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue, 1e-9,
                $"{kelvin} K");
        }
    }

    /// <summary>
    /// The cooler the glow, the redder; the hotter, the bluer; and white, in this renderer, which counts
    /// an even spectrum as white, is about the 5500 K of noon sunlight rather than a screen's 6500.
    /// </summary>
    [TestMethod]
    public void TestCoolIsRedAndHotIsBlue()
    {
        double was = double.PositiveInfinity;

        for (double kelvin = 1500; kelvin <= 12000; kelvin += 500)
        {
            Color color = SpectralColor.ToColor(SpectralColor.Blackbody(kelvin));
            double redOverBlue = color.Red / Math.Max(color.Blue, 1e-12);

            Assert.IsTrue(redOverBlue < was, $"{kelvin} K should be bluer than {kelvin - 500} K");

            was = redOverBlue;
        }

        double whitest = 0;
        double nearest = double.PositiveInfinity;

        for (double kelvin = 3000; kelvin <= 9000; kelvin += 5)
        {
            Color color = SpectralColor.ToColor(SpectralColor.Blackbody(kelvin));
            double off = Math.Max(Math.Abs(color.Red - 1), Math.Max(Math.Abs(color.Green - 1), Math.Abs(color.Blue - 1)));

            if (off < nearest)
                (whitest, nearest) = (kelvin, off);
        }

        Assert.AreEqual(5500, whitest, 100, "white should fall at about 5500 K");
        Assert.IsTrue(nearest < 0.06, $"and the glow there should be nearly white, not {nearest} off");
    }

    /// <summary>
    /// The sun is a glow at 5778 K, worked out by the same reckoning to the last bit.
    /// </summary>
    [TestMethod]
    public void TestTheSunIsAGlowAtItsTemperature()
    {
        CollectionAssert.AreEqual(SpectralColor.Blackbody(5778), Atmosphere.SunlightPerBand());
    }

    /// <summary>
    /// A light given a temperature is scaled so that its brightest channel is one, carries the glow as
    /// its spectrum, and gives the color to a render in red, green and blue and the glow to a spectral
    /// one.
    /// </summary>
    [TestMethod]
    public void TestALightGivenATemperatureCarriesItsGlow()
    {
        PointLight lamp = new ();

        lamp.SetColorTemperature(2700);

        Color color = lamp.Color;

        Assert.AreEqual(1, Math.Max(color.Red, Math.Max(color.Green, color.Blue)), 1e-12);
        Assert.IsTrue(color.Red > color.Green && color.Green > color.Blue, $"2700 K should be orange, not {color}");
        Assert.IsNotNull(lamp.Spectrum);

        Color glowSeen = SpectralColor.ToColor(lamp.Spectrum);

        Assert.AreEqual(color.Red, glowSeen.Red, 1e-9);
        Assert.AreEqual(color.Green, glowSeen.Green, 1e-9);
        Assert.AreEqual(color.Blue, glowSeen.Blue, 1e-9);

        Assert.IsTrue(color.Matches(lamp.Emitted<RgbSpectrum>().ToColor()));

        BandSpectrum glow = lamp.Emitted<BandSpectrum>();

        for (int band = 0; band < BandSpectrum.Count; band++)
            Assert.AreEqual(lamp.Spectrum[band], glow[band], $"band {band}");
    }

    /// <summary>
    /// Brightness multiplies a light's color and, where it has one, its glow, and leaves a light that is
    /// only a color without one.
    /// </summary>
    [TestMethod]
    public void TestBrightnessScalesColorAndGlow()
    {
        PointLight lamp = new ();

        lamp.SetColorTemperature(3200);

        Color color = lamp.Color;
        double[] glow = lamp.Spectrum;

        lamp.Brighten(2);

        Assert.IsTrue((color * 2).Matches(lamp.Color));
        CollectionAssert.AreEqual(glow.Select(amount => amount * 2).ToArray(), lamp.Spectrum);

        PointLight plain = new () { Color = new Color(0.4, 0.5, 0.6) };

        plain.Brighten(0.5);

        Assert.IsTrue(new Color(0.2, 0.25, 0.3).Matches(plain.Color));
        Assert.IsNull(plain.Spectrum);
    }

    /// <summary>
    /// A scene says it with `temperature` and `brightness`.  A temperature takes precedence over a color
    /// written beside it, and brightness multiplies whichever the light has.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysTemperatureAndBrightness()
    {
        PointLight expected = new ();

        expected.SetColorTemperature(2700);

        AssertNear(expected.Color, Lit("temperature 2700"), "temperature 2700");
        AssertNear(expected.Color * 0.4, Lit("temperature 2700  brightness 0.4"), "and dimmer");
        AssertNear(expected.Color, Lit("color Blue  temperature 2700"), "a temperature over a color");
        AssertNear(Colors.White, Lit("color [0.5, 0.5, 0.5]  brightness 2"), "brightness on a color");
    }

    /// <summary>
    /// Every sort of light takes a temperature, said the same way.
    /// </summary>
    [TestMethod]
    public void TestEverySortOfLightTakesATemperature()
    {
        string[] lights =
        [
            "point light { location [0, 0, -10]  temperature 2000 }",
            "spot light { location [0, 0, -10]  point at [0, 0, 0]  radius 20  falloff 30  temperature 2000 }",
            "area light { location [0, 0, -10]  axisU [1, 0, 0]  axisV [0, 1, 0]  temperature 2000 }",
            "distant light { direction [0, 0, 1]  temperature 2000 }",
            "sky light { pigment White  temperature 2000 }"
        ];

        foreach (string light in lights)
        {
            Color color = Rendered(light);

            Assert.IsTrue(color.Red > 0.1 && color.Red > 2 * color.Blue, $"{light} lit the ball {color}");
        }
    }

    /// <summary>
    /// A temperature at or below absolute zero, or a brightness below nothing, says what is wrong.
    /// </summary>
    [TestMethod]
    public void TestABadTemperatureOrBrightnessIsReported()
    {
        StringAssert.Contains(Complaint("point light { location [0, 0, -10]  temperature 0 }"),
            "nothing glows at absolute zero");
        StringAssert.Contains(Complaint("point light { location [0, 0, -10]  brightness -1 }"),
            "its brightness cannot be below nothing");
    }

    /// <summary>
    /// Balancing for a temperature turns a glow at that temperature white, whatever the temperature,
    /// and keeps how much of its pixel a color covers.
    /// </summary>
    [TestMethod]
    public void TestABalanceTurnsItsGlowWhite()
    {
        foreach (double kelvin in new[] { 1900.0, 2800, 3400, 5500, 6500, 10000 })
        {
            Color glow = SpectralColor.ToColor(SpectralColor.Blackbody(kelvin));
            Color balanced = new WhiteBalance(kelvin).Apply(glow);

            Assert.AreEqual(1, balanced.Red, 1e-9, $"{kelvin} K");
            Assert.AreEqual(1, balanced.Green, 1e-9, $"{kelvin} K");
            Assert.AreEqual(1, balanced.Blue, 1e-9, $"{kelvin} K");
        }

        Assert.AreEqual(0.25, new WhiteBalance(3400).Apply(new Color(0.3, 0.3, 0.3, 0.25)).Alpha);
        Assert.ThrowsExactly<ArgumentException>(() => new WhiteBalance(0));
    }

    /// <summary>
    /// Balanced for its own lamps a room is neutral; balanced a little above them it is warm rather
    /// than orange; and balanced for daylight, white being an even spectrum already, little changes.
    /// </summary>
    [TestMethod]
    public void TestABalanceIsWhatACameraWouldDo()
    {
        Color lamp = SpectralColor.GlowAt(2800).Color;
        Color neutral = new WhiteBalance(2800).Apply(lamp);
        Color warm = new WhiteBalance(3400).Apply(lamp);
        Color gray = new WhiteBalance(5500).Apply(new Color(0.5, 0.5, 0.5));

        Assert.AreEqual(neutral.Red, neutral.Green, 1e-9);
        Assert.AreEqual(neutral.Green, neutral.Blue, 1e-9);
        Assert.IsTrue(warm.Red > warm.Green && warm.Green > warm.Blue, $"balanced above it, warm, not {warm}");
        Assert.IsTrue(warm.Blue / warm.Red > lamp.Blue / lamp.Red * 2, "and much less orange than unbalanced");
        Assert.AreEqual(0.5, gray.Red, 0.05);
        Assert.AreEqual(0.5, gray.Green, 0.05);
        Assert.AreEqual(0.5, gray.Blue, 0.05);
    }

    /// <summary>
    /// A scene asks for a balance in its context block, the command line overrules it, and a white
    /// ball lit by a lamp balanced for comes out gray.
    /// </summary>
    [TestMethod]
    public void TestASceneAsksForAWhiteBalance()
    {
        Color balanced = Rendered("point light { location [0, 0, -10]  temperature 2800 }", "white balance 2800");
        Color unbalanced = Rendered("point light { location [0, 0, -10]  temperature 2800 }");
        Color overruled = Rendered("point light { location [0, 0, -10]  temperature 2800 }", "white balance 6000",
            new RenderOptions { WhiteBalance = 2800 });

        Assert.AreEqual(balanced.Red, balanced.Blue, 1.5 / 255, $"balanced for the lamp, gray, not {balanced}");
        Assert.IsTrue(unbalanced.Red > 3 * unbalanced.Blue, $"unbalanced, orange, not {unbalanced}");
        Assert.AreEqual(balanced.Blue, overruled.Blue, 1.5 / 255, "the command line should overrule the scene");
        StringAssert.Contains(
            Complaint("point light { location [0, 0, -10] }", "white balance 0"), "must be above nothing");
    }

    /// <summary>
    /// `kelvin()` is the color of a glow, the same a light given the temperature has, for whatever
    /// glows without being a light.
    /// </summary>
    [TestMethod]
    public void TestKelvinIsTheColorOfAGlow()
    {
        PointLight lamp = new ();

        lamp.SetColorTemperature(2700);

        Assert.IsTrue(lamp.Color.Matches(RayTracer.Terms.ColorFunctions.Kelvin(2700)));

        string path = Path.Combine(_directory, "glow.igl");
        string output = Path.Combine(_directory, "glow.png");

        File.WriteAllText(path,
            "context { no gamma }\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "point light { location [0, 0, -10] }\n" +
            "sphere { material { pigment kelvin(2700)  ambient 1  diffuse 0  specular 0 } }");

        new LanguageParser(path).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = 9, Height = 9, ProgressStyleText = "none"
        });

        AssertNear(lamp.Color, new ImageFile(output).Load()[0].GetPixel(4, 4), "a glowing pigment");
        Assert.ThrowsExactly<ArgumentException>(() => RayTracer.Terms.ColorFunctions.Kelvin(0));
    }

    /// <summary>
    /// This method renders a white ball lit head-on by a point light of the given description and
    /// returns the color at its middle, which is the light's own color, nothing being in the way.
    /// </summary>
    private Color Lit(string light)
    {
        return Rendered($"point light {{ location [0, 0, -10]  {light} }}");
    }

    /// <summary>
    /// This method renders a white ball under the given light, with no gamma, and returns the color at
    /// the middle of the picture.
    /// </summary>
    private Color Rendered(string light, string context = "", RenderOptions options = null)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path,
            $"context {{ no gamma  {context} }}\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            $"{light}\n" +
            "sphere { material { pigment White  ambient 0  specular 0  diffuse 1 } }");

        options ??= new RenderOptions();
        options.OutputFileName = output;
        options.Width = 9;
        options.Height = 9;
        options.ProgressStyleText = "none";

        new LanguageParser(path).Parse().Render(options);

        return new ImageFile(output).Load()[0].GetPixel(4, 4);
    }

    /// <summary>
    /// This method renders under the given light and returns whatever the render said, which for a
    /// term that fails its check is the complaint.
    /// </summary>
    private string Complaint(string light, string context = "")
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            Rendered(light, context);
        }
        catch (Exception exception)
        {
            Console.Write(exception);
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString();
    }

    /// <summary>
    /// This method asserts that a color read back from an eight-bit image matches the one expected, to
    /// within a level: rounding accounts for half of one, and a value sitting on the edge between two
    /// levels may land on either.
    /// </summary>
    private static void AssertNear(Color expected, Color actual, string what)
    {
        const double level = 1.0 / 255;

        Assert.AreEqual(expected.Red, actual.Red, level, $"{what}: red, {actual} for {expected}");
        Assert.AreEqual(expected.Green, actual.Green, level, $"{what}: green, {actual} for {expected}");
        Assert.AreEqual(expected.Blue, actual.Blue, level, $"{what}: blue, {actual} for {expected}");
    }
}
