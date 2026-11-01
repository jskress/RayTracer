using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// Light profiles read from IES files: the format, the symmetries, which way a profile points, and that the light it
/// shapes is shaped everywhere it reaches.
/// </summary>
[TestClass]
public class TestLightProfiles
{
    /// <summary>
    /// This helper writes an IES file's text: a header, the given tilt, the numbers that describe the measurement, and
    /// the angles and candelas, wrapped six to a line, as files wrap them however they like.
    /// </summary>
    private static string Ies(double[] vertical, double[] horizontal, double[][] candela, int type = 1,
        double multiplier = 1, string tilt = "TILT=NONE")
    {
        IEnumerable<double> numbers = new double[]
            {
                1, -1, multiplier, vertical.Length, horizontal.Length, type, 2, 0.1, 0.1, 0.05, 1, 1, 10
            }
            .Concat(vertical)
            .Concat(horizontal)
            .Concat(candela.SelectMany(plane => plane));
        List<string> lines = ["IESNA:LM-63-2002", "[TEST] made for a test", "[MANUFAC] nobody", tilt];

        lines.AddRange(numbers
            .Select((number, index) => (number, index))
            .GroupBy(pair => pair.index / 6)
            .Select(group => string.Join(" ", group.Select(pair => pair.number.ToString("R")))));

        return string.Join("\r\n", lines);
    }

    private static readonly double[] Down = [0, 30, 60, 90];

    /// <summary>
    /// A round fitting's one plane stands for every way round; between the angles it was measured at it is read along
    /// straight lines; it is scaled so its brightest way is one, its multiplier and all; and past the angles it covers
    /// -- above the horizon, for a downlight -- it is dark.
    /// </summary>
    [TestMethod]
    public void TestARoundProfileIsReadBetweenItsAngles()
    {
        LightProfile profile = LightProfile.Parse(Ies(Down, [0], [[400, 300, 100, 0]], multiplier: 2.5), "round");

        Assert.AreEqual(1, profile.At(0, 0), 1e-12);
        Assert.AreEqual(0.75, profile.At(30, 123), 1e-12);
        Assert.AreEqual((0.75 + 0.25) / 2, profile.At(45, 300), 1e-12);
        Assert.AreEqual(0, profile.At(91, 0), 1e-12);
        Assert.AreEqual(0, profile.At(170, 0), 1e-12);
    }

    /// <summary>
    /// A fitting measured over a quarter, a half, or from 90 to 270 is the same mirrored: what stands at 30 stands at
    /// 150, 210 and 330 for a quarter, at 330 for a half, and at 150 for the half from 90 to 270.  One measured the
    /// whole way round runs from its last plane on to its first.
    /// </summary>
    [TestMethod]
    public void TestEachSymmetryIsMirroredRound()
    {
        double[][] Planes(int count) =>
            Enumerable.Range(0, count).Select(index => new double[] { 100 + 10 * index, 50, 20, 0 }).ToArray();

        LightProfile quarter = LightProfile.Parse(Ies(Down, [0, 30, 60, 90], Planes(4)), "quarter");

        foreach (double horizontal in new[] { 150.0, 210, 330 })
            Assert.AreEqual(quarter.At(0, 30), quarter.At(0, horizontal), 1e-12, $"a quarter at {horizontal}");

        LightProfile half = LightProfile.Parse(Ies(Down, [0, 90, 180], Planes(3)), "half");

        Assert.AreEqual(half.At(0, 30), half.At(0, 330), 1e-12);
        Assert.AreEqual(half.At(0, 135), half.At(0, 225), 1e-12);
        Assert.AreNotEqual(half.At(0, 30), half.At(0, 150), "a half is not a quarter");

        LightProfile across = LightProfile.Parse(Ies(Down, [90, 180, 270], Planes(3)), "across");

        Assert.AreEqual(across.At(0, 150), across.At(0, 30), 1e-12);
        Assert.AreEqual(across.At(0, 240), across.At(0, 300), 1e-12);

        LightProfile whole = LightProfile.Parse(Ies(Down, [0, 90, 180, 270], Planes(4)), "whole");

        Assert.AreEqual((whole.At(0, 270) + whole.At(0, 0)) / 2, whole.At(0, 315), 1e-12, "the last plane runs on to the first");
    }

    /// <summary>
    /// A tilt table is read past, and so is the maker's description before it; a file of floodlight or car-lamp
    /// photometry is refused, saying which; and one that stops short says so.
    /// </summary>
    [TestMethod]
    public void TestTheFormatIsReadAndWhatCannotBeIsRefused()
    {
        string tilted = Ies(Down, [0], [[400, 300, 100, 0]], tilt: "TILT=INCLUDE\r\n1\r\n3\r\n0 45 90\r\n1 0.9 0.8");

        Assert.AreEqual(0.75, LightProfile.Parse(tilted, "tilted").At(30, 0), 1e-12);

        string floodlight = Ies(Down, [0], [[400, 300, 100, 0]], type: 2);

        StringAssert.Contains(Refusal(() => LightProfile.Parse(floodlight, "flood")), "photometric type B");

        string cut = Ies(Down, [0], [[400, 300, 100, 0]]);

        StringAssert.Contains(Refusal(() => LightProfile.Parse(cut[..cut.LastIndexOf(' ')], "cut")), "ends before");
        StringAssert.Contains(Refusal(() => LightProfile.Parse("IESNA91\r\n1 2 3", "bare")), "no TILT line");
    }

    private static string Refusal(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception.Message;
        }

        return "nothing was refused";
    }

    /// <summary>
    /// A profile hangs straight down, its horizontal nought along +X and its 90 along +Z -- counterclockwise seen from
    /// above -- unless it is turned or aimed: turned a quarter, what lay along +X lies along +Z; aimed along +X, its
    /// straight down is +X.
    /// </summary>
    [TestMethod]
    public void TestAProfilePointsWhereItIsToldTo()
    {
        // Bright only toward its horizontal 90, a little below the horizon.
        double[][] planes = Enumerable.Range(0, 5)
            .Select(index => index == 1 ? new double[] { 0, 0, 0, 100 } : new double[] { 0, 0, 0, 1 })
            .ToArray();
        LightProfile profile = LightProfile.Parse(Ies(Down, [0, 90, 180, 270, 360], planes), "sideways");
        PointLight light = new () { Location = new Point(0, 5, 0), Profile = profile };

        Assert.AreEqual(1, light.IntensityToward(new Point(0, 5, 3)), 1e-9, "its 90 lies along +Z");
        Assert.AreEqual(0.01, light.IntensityToward(new Point(3, 5, 0)), 1e-9);

        PointLight turned = new () { Location = new Point(0, 5, 0), Profile = profile, ProfileTurn = Math.PI / 2 };

        Assert.AreEqual(1, turned.IntensityToward(new Point(-3, 5, 0)), 1e-9, "turned, its 90 lies along -X");

        LightProfile downward = LightProfile.Parse(Ies(Down, [0], [[100, 50, 10, 0]]), "down");
        PointLight aimed = new () { Location = Point.Zero, Profile = downward, ProfileAim = new Point(4, 0, 0) };

        Assert.AreEqual(1, aimed.IntensityToward(new Point(9, 0, 0)), 1e-9, "aimed along +X");
        Assert.AreEqual(0, aimed.IntensityToward(new Point(0, -9, 0)), 1e-9, "square to its aim is its horizon");
    }

    /// <summary>
    /// A light placed in a group turns its profile with the group: a group turned a quarter about Y carries the
    /// profile's 90 from +Z to +X.
    /// </summary>
    [TestMethod]
    public void TestAPlacedProfileTurnsWithItsGroup()
    {
        double[][] planes = Enumerable.Range(0, 5)
            .Select(index => index == 1 ? new double[] { 0, 0, 0, 100 } : new double[] { 0, 0, 0, 1 })
            .ToArray();
        LightProfile profile = LightProfile.Parse(Ies(Down, [0, 90, 180, 270, 360], planes), "sideways");
        PointLight light = new () { Location = new Point(0, 5, 0), Profile = profile };
        Matrix turn = Transforms.RotateAroundY(90);
        PointLight placed = (PointLight) light.PlacedBy(point => turn * point);
        Point toward = turn * new Point(0, 5, 3);

        Assert.AreEqual(1, placed.IntensityToward(toward), 1e-9);
        Assert.AreEqual(1, light.IntensityToward(new Point(0, 5, 3)), 1e-9, "the light written is left as it was");
    }

    /// <summary>
    /// A spotlight's profile points where its cone does and shapes its cone further; an area light's samples carry its
    /// profile toward the place lit, each of them.
    /// </summary>
    [TestMethod]
    public void TestSpotlightsAndAreaLightsTakeTheirProfiles()
    {
        LightProfile profile = LightProfile.Parse(Ies(Down, [0], [[100, 50, 10, 0]]), "down");
        Spotlight spot = new ()
        {
            Location = Point.Zero, PointAt = new Point(0, 0, 5), Radius = Math.PI / 3, Falloff = Math.PI / 2.5,
            Profile = profile
        };
        Spotlight plain = new () { Location = Point.Zero, PointAt = new Point(0, 0, 5), Radius = Math.PI / 3, Falloff = Math.PI / 2.5 };
        Point aside = new (Math.Tan(Math.PI / 6) * 5, 0, 5);

        Assert.AreEqual(1, spot.IntensityToward(new Point(0, 0, 9)), 1e-9);
        Assert.AreEqual(plain.IntensityToward(aside) * 0.5, spot.IntensityToward(aside), 1e-9);

        AreaLight area = new ()
        {
            Location = new Point(0, 3, 0), Axis1 = new Vector(0.5, 0, 0), Axis2 = new Vector(0, 0, 0.5), USteps = 2,
            VSteps = 2, Profile = profile
        };
        Point lit = new (Math.Tan(Math.PI / 6) * 3, 0, 0);

        for (int index = 0; index < area.SampleCount; index++)
            Assert.AreEqual(0.5, area.SampleToward(lit, index).Cone, 1e-9);
    }

    /// <summary>
    /// A scene's light given a profile from a file beside it lights a floor where the profile shines and leaves it
    /// dark past its cutoff; a profile that is not there is refused, saying where it was looked for.
    /// </summary>
    [TestMethod]
    public void TestASceneLightsAFloorByItsProfile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"profile-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, "cone.ies"), Ies(Down, [0], [[100, 100, 0, 0]]));

            (Canvas image, string output) = Render(directory,
                "context { no gamma }\n" +
                "camera { location [0, 10, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 90 }\n" +
                "plane { material { pigment White  ambient 0  diffuse 1  specular 0 } }\n" +
                "point light { location [0, 2, 0]  profile 'cone.ies' }\n");

            Assert.IsNotNull(image, output);

            // Straight below, lit; far out to the side, past the profile's 30 to 60 degree fade to nothing, dark.
            Assert.IsTrue(image.GetPixel(20, 20).Red > 0.5, $"below the light: {image.GetPixel(20, 20).Red}");
            Assert.AreEqual(0, image.GetPixel(1, 20).Red, 1e-9, "past the profile's cutoff");

            StringAssert.Contains(Render(directory, "point light { profile 'missing.ies' }\n").Output,
                "There is no light profile at");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static (Canvas Image, string Output) Render(string directory, string scene)
    {
        string path = Path.Combine(directory, "scene.igl");
        string output = Path.Combine(directory, "out.pfm");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(path, scene);
        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return (null, captured.ToString());

            renderer.Render(new RenderOptions { OutputFileName = output, Width = 41, Height = 41 });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (new ImageFile(output).Load()[0], captured.ToString());
        }
        catch (Exception exception)
        {
            return (null, exception.Message);
        }
        finally
        {
            Console.SetOut(was);
        }
    }
}
