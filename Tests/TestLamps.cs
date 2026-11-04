using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover the named lamps: that the CIE's tables are read as the CIE describes them, that sodium's lines
/// land where sodium's light is, and that a light, a color and a white balance may each be a lamp.
/// </summary>
[TestClass]
public class TestLamps
{
    private string _directory;

    [TestInitialize]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"lamps-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// This method works out the color temperature a lamp's light comes out at, by McCamy's formula from where its
    /// color falls on the chromaticity chart.
    /// </summary>
    private static double ColorTemperatureOf(string lamp)
    {
        double[] perBand = Lamps.Named(lamp).PerBand;
        double x = 0, y = 0, z = 0;

        for (int band = 0; band < SpectralColor.Bands; band++)
        {
            double wavelength = SpectralColor.WavelengthOf(band);

            x += perBand[band] * SpectralColor.MatchRed(wavelength);
            y += perBand[band] * SpectralColor.MatchGreen(wavelength);
            z += perBand[band] * SpectralColor.MatchBlue(wavelength);
        }

        double n = (x / (x + y + z) - 0.3320) / (0.1858 - y / (x + y + z));

        return 449 * n * n * n + 3525 * n * n + 6823.3 * n + 5520.33;
    }

    /// <summary>
    /// The CIE's lamps come out at the color temperatures CIE 015:2018 gives them, within two percent -- what is left
    /// being the renderer's 32 bands -- which is the check that every column of every table is read, and read as the
    /// lamp it is named for.
    /// </summary>
    [TestMethod]
    public void TestTheCiesLampsComeOutAtTheirColorTemperatures()
    {
        (string Lamp, double Kelvin)[] published =
        [
            ("FL1", 6430), ("FL2", 4230), ("FL3", 3450), ("FL4", 2940), ("FL5", 6350), ("FL6", 4150),
            ("FL7", 6500), ("FL8", 5000), ("FL9", 4150), ("FL10", 5000), ("FL11", 4000), ("FL12", 3000),
            ("HP1", 1959), ("LED-B1", 2733), ("LED-B2", 2998), ("LED-B3", 4103), ("LED-B4", 5109),
            ("LED-B5", 6598)
        ];

        foreach ((string lamp, double kelvin) in published)
            Assert.AreEqual(kelvin, ColorTemperatureOf(lamp), kelvin * 0.02, lamp);

        // The everyday names are the CIE's lamps they say they are.
        Assert.AreEqual(ColorTemperatureOf("FL2"), ColorTemperatureOf("fluorescent"), 1e-9);
        Assert.AreEqual(ColorTemperatureOf("LED-B3"), ColorTemperatureOf("neutral white LED"), 1e-9);
        Assert.AreEqual(ColorTemperatureOf("HP1"), ColorTemperatureOf("high pressure sodium"), 1e-9);
    }

    /// <summary>
    /// Low pressure sodium gives the band holding sodium's D lines, 589 nm, their share of its light by NIST's table --
    /// 1500 of the 1535 its lines come to, the rest a few faint lines -- and low pressure mercury gives its light in
    /// the bands holding mercury's lines and nowhere else.
    /// </summary>
    [TestMethod]
    public void TestTheGasLampsGiveTheirLinesLight()
    {
        double[] sodium = Lamps.Named("low pressure sodium").PerBand;
        int yellow = (int) ((589 - SpectralColor.ShortestWavelength) / 12.5);

        Assert.AreEqual(1500.0 / 1535, sodium[yellow] / sodium.Sum(), 1e-12, "sodium's yellow band");

        double[] mercury = Lamps.Named("low pressure mercury").PerBand;
        int[] lines = new[] { 404.66, 433.92, 434.75, 435.83, 546.07, 576.96, 579.07, 708.19 }
            .Select(line => (int) ((line - SpectralColor.ShortestWavelength) / 12.5))
            .ToArray();

        for (int band = 0; band < SpectralColor.Bands; band++)
        {
            Assert.AreEqual(lines.Contains(band), mercury[band] > 0,
                $"band {band} at {SpectralColor.WavelengthOf(band):0} nm");
        }
    }

    /// <summary>
    /// Every lamp's color has its brightest channel at one, its light band by band nowhere less than nothing, and the
    /// two scaled alike; and a name is found whatever its case, the CIE's old names for its fluorescents too.
    /// </summary>
    [TestMethod]
    public void TestEveryLampIsHeldAtABrightestChannelOfOne()
    {
        foreach (string name in Lamps.Names)
        {
            (Color color, double[] perBand) = Lamps.Named(name);
            Color fromBands = SpectralColor.ToColor(perBand);

            Assert.AreEqual(1, Math.Max(color.Red, Math.Max(color.Green, color.Blue)), 1e-9, name);
            Assert.IsTrue(perBand.All(amount => amount >= 0), name);
            Assert.AreEqual(color.Red, fromBands.Red, 1e-9, name);
            Assert.AreEqual(color.Blue, fromBands.Blue, 1e-9, name);
        }

        Assert.AreEqual(Lamps.Named("high pressure sodium").Color.Green, Lamps.Named("High Pressure SODIUM").Color.Green);
        CollectionAssert.AreEqual(Lamps.Named("FL11").PerBand, Lamps.Named("f11").PerBand);

        ArgumentException refused = Assert.ThrowsExactly<ArgumentException>(() => Lamps.Named("gas mantle"));

        StringAssert.Contains(refused.Message, "no lamp named \"gas mantle\"");
        StringAssert.Contains(refused.Message, "'high pressure sodium'");
        StringAssert.Contains(refused.Message, "FL1 to FL12");
    }

    /// <summary>
    /// A light given a lamp lights a spectral render with the lamp's own light: under low pressure sodium, all but
    /// one yellow, a red ball and a blue one are the same color, only the red brighter -- which no color can do, as a
    /// render in red, green and blue under the same lamp, its color, shows.
    /// </summary>
    [TestMethod]
    public void TestALightGivenALampLightsWithItsSpectrum()
    {
        (Color red, Color blue) = Balls("spectral", "lamp 'low pressure sodium'");

        Assert.IsTrue(red.Red > blue.Red, $"the red ball {red} should be brighter than the blue {blue}");
        Assert.AreEqual(red.Green / red.Red, blue.Green / blue.Red, 0.03, $"red {red} and blue {blue} should share a hue");

        (Color plainRed, Color plainBlue) = Balls("", "lamp 'low pressure sodium'");

        Assert.IsTrue(Math.Abs(plainRed.Green / plainRed.Red - plainBlue.Green / plainBlue.Red) > 0.2,
            $"in red, green and blue the red {plainRed} and the blue {plainBlue} should differ in hue");
    }

    /// <summary>
    /// This method renders a red ball and a blue one under the given light and hands back the color at each's middle.
    /// </summary>
    private (Color Red, Color Blue) Balls(string context, string lamp)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path,
            $"context {{ no gamma {context} }}\n" +
            "camera { location [0, 0, -6]  look at [0, 0, 0] }\n" +
            $"point light {{ location [0, 0, -10]  {lamp}  brightness 1.4 }}\n" +
            "sphere { translate [-1.2, 0, 0]  material { pigment [0.8, 0.1, 0.1]  ambient 0  specular 0 } }\n" +
            "sphere { translate [1.2, 0, 0]  material { pigment [0.1, 0.25, 0.85]  ambient 0  specular 0 } }");

        new LanguageParser(path).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = 40, Height = 20, ProgressStyleText = "none"
        });

        Canvas canvas = new ImageFile(output).Load()[0];

        return (canvas.GetPixel(11, 10), canvas.GetPixel(28, 10));
    }

    /// <summary>
    /// A light may give the light of a temperature or of a lamp, not both; and a lamp that is not known is refused,
    /// wherever it is named.
    /// </summary>
    [TestMethod]
    public void TestALampIsRefusedBesideATemperatureOrWhenUnknown()
    {
        StringAssert.Contains(Refusal("point light { location [0, 5, 0]  temperature 2700  lamp 'fluorescent' }"),
            "not both");
        StringAssert.Contains(Refusal("point light { location [0, 5, 0]  lamp 'gaslight' }"),
            "no lamp named \"gaslight\"");
        StringAssert.Contains(Refusal("context { white balance 'gaslight' }"), "to balance for");
        StringAssert.Contains(Refusal("sphere { material { pigment lamp('gaslight') } }"), "no lamp named");
    }

    private string Refusal(string scene)
    {
        string path = Path.Combine(_directory, "refused.igl");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(path, "camera { location [0, 2, -6]  look at [0, 0, 0] }\n" + scene + "\nplane { }");
        Console.SetOut(captured);

        try
        {
            new LanguageParser(path).Parse()?.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "refused.png"), Width = 8, Height = 6,
                ProgressStyleText = "none"
            });
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString();
    }

    /// <summary>
    /// The color of a lamp, as a function, is the lamp's color, for whatever glows of a lamp without being its light.
    /// </summary>
    [TestMethod]
    public void TestALampsColorIsAFunction()
    {
        Color color = ColorFunctions.Lamp("triphosphor fluorescent");

        Assert.AreEqual(Lamps.Named("FL11").Color.Green, color.Green, 1e-12);
    }

    /// <summary>
    /// A picture balanced for a lamp shows the lamp's light as white, at the brightness of white; a scene says so with
    /// the lamp's name, and a temperature on the command line takes the place of it.
    /// </summary>
    [TestMethod]
    public void TestAPictureMayBeBalancedForALamp()
    {
        Color sodium = Lamps.Named("high pressure sodium").Color;
        Color seen = new WhiteBalance("high pressure sodium").Apply(
            sodium / (0.2126 * sodium.Red + 0.7152 * sodium.Green + 0.0722 * sodium.Blue));

        Assert.AreEqual(1, seen.Red, 0.01, $"{seen}");
        Assert.AreEqual(1, seen.Green, 0.01, $"{seen}");
        Assert.AreEqual(1, seen.Blue, 0.01, $"{seen}");

        RenderContext context = new () { WhiteBalanceLamp = "fluorescent" };

        Assert.AreEqual("fluorescent", context.Balance.Lamp);

        context.ApplyOptions(new RenderOptions());
        Assert.AreEqual("fluorescent", context.Balance.Lamp, "a silent command line should leave the scene's balance");

        context.ApplyOptions(new RenderOptions { WhiteBalance = 3200 });
        Assert.AreEqual(3200, context.Balance.Temperature);
        Assert.IsNull(context.WhiteBalanceLamp);
    }
}
