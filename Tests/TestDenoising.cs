using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Core.Denoising;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Scanners;

namespace Tests;

/// <summary>
/// These tests cover smoothing the grain out of a rendered picture: the guides a pixel gives a denoiser, the
/// renderer's own filter, the choice between it and Intel's, and a scene asking for it.
/// </summary>
[TestClass]
public class TestDenoising
{
    private const int Size = 64;

    // A ray from above, looking straight down at the middle of things.
    private static readonly Ray Down = new (new Point(0, 5, 0), new Vector(0, -1, 0));

    private static Material Matte(Color color) => new ()
    {
        Pigment = new SolidPigment(color), Ambient = 0, Diffuse = 1, Specular = 0
    };

    private static Scene SceneOf(Color sky, params Surface[] surfaces)
    {
        Scene scene = new () { Background = new SolidPigment(sky) };

        foreach (Surface surface in surfaces)
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        return scene;
    }

    private static (double Red, double Green, double Blue) ColorOf(GuideSample found) =>
        (found.Red / found.Weight, found.Green / found.Weight, found.Blue / found.Weight);

    private static void AssertNear(
        (double Red, double Green, double Blue) expected, (double Red, double Green, double Blue) actual,
        double tolerance, string what)
    {
        Assert.AreEqual(expected.Red, actual.Red, tolerance, $"{what}: red");
        Assert.AreEqual(expected.Green, actual.Green, tolerance, $"{what}: green");
        Assert.AreEqual(expected.Blue, actual.Blue, tolerance, $"{what}: blue");
    }

    /// <summary>
    /// A Portable FloatMap holds three numbers a pixel as they stand, its rows from the bottom up, and reads back as
    /// it was written.
    /// </summary>
    [TestMethod]
    public void TestAFloatMapReadsBackAsItWasWritten()
    {
        string file = Path.Combine(Path.GetTempPath(), $"map-{Guid.NewGuid():N}.pfm");
        float[] values = Enumerable.Range(0, 3 * 2 * 3).Select(index => index * 0.25f - 1).ToArray();

        try
        {
            PortableFloatMap.Write(file, 3, 2, values);

            byte[] bytes = File.ReadAllBytes(file);
            const string header = "PF\n3 2\n-1.0\n";

            Assert.AreEqual(header, System.Text.Encoding.ASCII.GetString(bytes, 0, header.Length));
            Assert.AreEqual(values[9], BitConverter.ToSingle(bytes, header.Length),
                "the first number written should be the bottom row's first pixel's red");

            (int width, int height, float[] read) = PortableFloatMap.Read(file);

            Assert.AreEqual(3, width);
            Assert.AreEqual(2, height);
            CollectionAssert.AreEqual(values, read);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// A matte surface guides by its own color, its facing and where it is; the sky by its color, held to white,
    /// facing nowhere and met nowhere.
    /// </summary>
    [TestMethod]
    public void TestAMatteSurfaceAndTheSkyGuideByTheirOwnColors()
    {
        Scene scene = SceneOf(new Color(2, 0.5, 0.1), new Plane { Material = Matte(new Color(0.7, 0.2, 0.1)) });
        GuideSample floor = scene.GuideFor(Down);

        AssertNear((0.7, 0.2, 0.1), ColorOf(floor), 1e-9, "the floor's color");
        Assert.AreEqual(1, floor.Met, 1e-12);
        Assert.AreEqual(1, floor.NormalY, 1e-9, "the floor should face up");
        Assert.AreEqual(0, floor.PlaceY, 1e-9, "the floor should be met on it");

        GuideSample sky = scene.GuideFor(new Ray(new Point(0, 5, 0), new Vector(0, 1, 0)));

        AssertNear((1, 0.5, 0.1), ColorOf(sky), 1e-9, "the sky's color, held to white");
        Assert.AreEqual(0, sky.Met, "the sky is met nowhere");
        Assert.AreEqual(0, sky.NormalX * sky.NormalX + sky.NormalY * sky.NormalY + sky.NormalZ * sky.NormalZ);
    }

    /// <summary>
    /// A smooth mirror guides by what it shows, facing as that faces; a rough one by its own color, its blur being
    /// guide enough.
    /// </summary>
    [TestMethod]
    public void TestASmoothMirrorGuidesByWhatItShows()
    {
        // A mirror facing up under the ray, tilted to send it off along +X to a green wall.
        Material mirror = new ()
        {
            Pigment = new SolidPigment(new Color(0.1, 0.1, 0.1)), Ambient = 0, Diffuse = 0, Specular = 0,
            Reflective = 1
        };
        Plane glass = new () { Material = mirror, Transform = Transforms.RotateAroundZ(-45) };
        Plane wall = new ()
        {
            Material = Matte(new Color(0.1, 0.8, 0.2)),
            Transform = Transforms.Translate(4, 0, 0) * Transforms.RotateAroundZ(90)
        };
        GuideSample seen = SceneOf(Colors.Black, glass, wall).GuideFor(Down);

        AssertNear((0.1, 0.8, 0.2), ColorOf(seen), 1e-6, "a mirror's guide");
        Assert.AreEqual(1, Math.Abs(seen.NormalX), 1e-6, "a mirror's guide should face as the wall does");
        Assert.AreEqual(4, seen.PlaceX, 1e-6, "a mirror's guide should be where the wall is");

        mirror.Roughness = 0.3;

        AssertNear((0.1, 0.1, 0.1), ColorOf(SceneOf(Colors.Black, glass, wall).GuideFor(Down)), 1e-6,
            "a rough mirror's guide");
    }

    /// <summary>
    /// Clear glass guides by what is beyond it, tinted as it tints it; its mirroring share by what it mirrors.
    /// </summary>
    [TestMethod]
    public void TestClearGlassGuidesByWhatIsBeyondIt()
    {
        Material clear = new ()
        {
            Pigment = new SolidPigment(new Color(1, 0.5, 1)), Ambient = 0, Diffuse = 0, Specular = 0,
            Transparency = 1, Interior = new Interior { IndexOfRefraction = 1.5, Filter = 1 }
        };
        Cube pane = new ()
        {
            Material = clear, Transform = Transforms.Translate(0, 2, 0) * Transforms.Scale(2, 0.1, 2)
        };
        Plane floor = new () { Material = Matte(new Color(0.2, 0.6, 0.9)) };

        // Through both faces of a filtering pane: its tint twice over.
        AssertNear((0.2, 0.6 * 0.25, 0.9), ColorOf(SceneOf(Colors.Black, pane, floor).GuideFor(Down)), 1e-6,
            "a filtering pane's guide");

        // A pane that mirrors as well, by Schlick's share square on, each face a twenty-fifth of what reaches it; the
        // black sky above is all it mirrors.
        clear.Reflective = 1;
        clear.Interior = new Interior { IndexOfRefraction = 1.5 };

        (double red, double green, double blue) = ColorOf(SceneOf(Colors.Black, pane, floor).GuideFor(Down));
        double passed = 0.96 * 0.96;

        Assert.AreEqual(0.2 * passed, red, 0.01, "a mirroring pane's guide: red");
        Assert.AreEqual(0.6 * passed, green, 0.01, "a mirroring pane's guide: green");
        Assert.AreEqual(0.9 * passed, blue, 0.01, "a mirroring pane's guide: blue");
    }

    /// <summary>
    /// The guides for a flat, square-on picture of a surface of the given colors, a pixel apart at a distance of
    /// one, facing the eye.  A pixel whose color says nought is sky.
    /// </summary>
    private static Guides Flat(
        Func<int, int, double> albedo, Func<int, int, double> distance = null, double mirrors = 0)
    {
        Guides guides = new (Size, Size);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int at = y * Size + x;
                double gray = albedo(x, y);

                if (gray <= 0)
                    continue;

                guides.Albedo[at * 3] = guides.Albedo[at * 3 + 1] = guides.Albedo[at * 3 + 2] = (float) gray;
                guides.Normal[at * 3 + 2] = -1;
                guides.Place[at * 3] = x;
                guides.Place[at * 3 + 1] = y;
                guides.Place[at * 3 + 2] = distance?.Invoke(x, y) ?? 0;
                guides.Footprint[at] = 0.5f;
                guides.Met[at] = 1;
                guides.Mirrors[at] = (float) mirrors;
            }
        }

        return guides;
    }

    /// <summary>
    /// A picture of the given light, with grain of the given spread added by a fixed draw.
    /// </summary>
    private static Canvas Grainy(Func<int, int, double> light, double grain, int seed = 7)
    {
        Random random = new (seed);
        Canvas canvas = new (Size, Size);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // A normal draw by Box and Muller.
                double off = grain * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) *
                             Math.Cos(2 * Math.PI * random.NextDouble());
                double value = light(x, y) + off;

                canvas.SetColor(new Color(value, value, value), x, y);
            }
        }

        return canvas;
    }

    private static Canvas Filtered(Canvas canvas, Guides guides)
    {
        WaveletFilter.Apply(canvas, guides, new SingleThreadScanner());

        return canvas;
    }

    /// <summary>
    /// The mean difference of a picture from the truth over the given columns.
    /// </summary>
    private static double OffBy(Canvas canvas, Func<int, int, double> truth, int from = 0, int to = Size)
    {
        double sum = 0;
        int count = 0;

        for (int y = 0; y < Size; y++)
        {
            for (int x = from; x < to; x++, count++)
                sum += Math.Abs(canvas.GetPixel(x, y).Red - truth(x, y));
        }

        return sum / count;
    }

    /// <summary>
    /// The filter smooths grain out of the light on a surface, keeping how much light there is: here a twentieth of
    /// the light, cut by more than a factor of five, and the whole within a half percent.
    /// </summary>
    [TestMethod]
    public void TestTheFilterSmoothsGrainAndKeepsTheLight()
    {
        static double Truth(int x, int y) => 0.4;

        Canvas grainy = Grainy(Truth, 0.02);
        double before = OffBy(grainy, Truth);
        Canvas smoothed = Filtered(Grainy(Truth, 0.02), Flat((_, _) => 0.5));
        double after = OffBy(smoothed, Truth);
        double mean = Enumerable.Range(0, Size * Size).Average(at => smoothed.GetPixel(at % Size, at / Size).Red);

        Assert.IsTrue(after < before / 5,
            $"the grain should be smoothed: {before:0.0000} before, {after:0.0000} after");
        Assert.AreEqual(0.4, mean, 0.002, "the light should be kept");
    }

    /// <summary>
    /// A pattern in a surface's color stays as sharp as it was: light divided by the color is what is smoothed, and
    /// a checker eight pixels square under even light comes back as the checker.
    /// </summary>
    [TestMethod]
    public void TestTheFilterKeepsAPatternSharp()
    {
        static double Checker(int x, int y) => (x / 8 + y / 8) % 2 == 0 ? 0.2 : 0.8;
        static double Truth(int x, int y) => 0.5 * Checker(x, y);

        double before = OffBy(Grainy(Truth, 0.01), Truth);
        double after = OffBy(Filtered(Grainy(Truth, 0.01), Flat(Checker)), Truth);

        Assert.IsTrue(after < before / 3, $"the checker should keep its edges: {before:0.0000} before, {after:0.0000}");
    }

    /// <summary>
    /// A fine pattern in a surface's color -- each pixel a tenth either way of the surface's gray, as a granite's
    /// grain is -- stays as it was: what is smoothed is the light over the color, which the pattern leaves even.
    /// </summary>
    [TestMethod]
    public void TestTheFilterKeepsAFinePatternSharp()
    {
        static double Speckled(int x, int y) =>
            0.5 + 0.05 * Math.Sin(x * 12.9898 + y * 78.233) * Math.Cos(x * 4.1 - y * 2.7);
        static double Truth(int x, int y) => Speckled(x, y);

        double before = OffBy(Grainy(Truth, 0.01), Truth);
        double after = OffBy(Filtered(Grainy(Truth, 0.01), Flat(Speckled)), Truth);

        Assert.IsTrue(after < before / 3, $"the speckles should be kept: {before:0.0000} before, {after:0.0000} after");
    }

    /// <summary>
    /// A surface that mirrors some light as well as showing its own color -- a paving of slabs and mortar mirroring the
    /// sky -- keeps what it mirrors where it was mirrored: light over color is not all light that fell there, so two
    /// colors far apart are never smoothed together, or the mortar takes the slabs' share of the sky.
    /// </summary>
    [TestMethod]
    public void TestTheFilterKeepsWhatASurfaceMirrors()
    {
        static double Checker(int x, int y) => (x / 8 + y / 8) % 2 == 0 ? 0.2 : 0.8;
        static double Truth(int x, int y) => 0.5 * Checker(x, y) + 0.2;

        double before = OffBy(Grainy(Truth, 0.05), Truth);
        double after = OffBy(Filtered(Grainy(Truth, 0.05), Flat(Checker, mirrors: 0.2)), Truth);

        Assert.IsTrue(after < before / 2,
            $"the mirrored light should stay put: {before:0.0000} before, {after:0.0000}");
    }

    /// <summary>
    /// A shadow's edge is kept: light falling from one to a fifth across a surface of one color, its grain a
    /// fiftieth, comes back with the columns either side of the edge as they were, not blurred into each other.
    /// </summary>
    [TestMethod]
    public void TestTheFilterKeepsAShadowsEdge()
    {
        static double Truth(int x, int y) => x < Size / 2 ? 1 : 0.2;

        Canvas smoothed = Filtered(Grainy(Truth, 0.02), Flat((_, _) => 0.5));
        double atTheEdge = OffBy(smoothed, Truth, Size / 2 - 2, Size / 2 + 2);

        Assert.IsTrue(atTheEdge < 0.02, $"the shadow's edge should be kept: off by {atTheEdge:0.0000} beside it");
    }

    /// <summary>
    /// A picture with no grain in it comes back as it was, light that changes across a surface and all; so does
    /// the sky; and so do a few bright points on a grainy surface that mirrors -- lamps it shows -- which are not
    /// taken for its grain.  On a matte surface the same points can only be grain, and are smoothed away.
    /// </summary>
    [TestMethod]
    public void TestTheFilterLeavesWhatIsNotGrainAlone()
    {
        static double Gradient(int x, int y) => 0.1 + x * 0.01 + y * 0.002;

        Canvas smooth = Filtered(Grainy(Gradient, 0), Flat((x, _) => x < 8 ? 0 : 0.5));

        Assert.AreEqual(0, OffBy(smooth, Gradient), 1e-5, "a picture without grain should come back as it was");

        static double Glinting(int x, int y) => x % 16 == 8 && y % 16 == 8 ? 3 : 0.4;

        Canvas glints = Filtered(Grainy(Glinting, 0.02), Flat((_, _) => 0.5, mirrors: 0.1));
        Canvas sparks = Filtered(Grainy(Glinting, 0.02), Flat((_, _) => 0.5));

        for (int y = 8; y < Size; y += 16)
        {
            for (int x = 8; x < Size; x += 16)
            {
                double kept = glints.GetPixel(x, y).Red;
                double left = sparks.GetPixel(x, y).Red;

                Assert.IsTrue(kept > 2.5, $"the glint at ({x}, {y}) should be kept, not {kept:0.000}");
                Assert.IsTrue(left < 1.5, $"the spark at ({x}, {y}) should be smoothed, not left at {left:0.000}");
            }
        }
    }

    /// <summary>
    /// A near surface's light is never mixed with a far one's behind it, however alike: two halves of the picture,
    /// twenty pixels' widths apart in depth, lit a little differently, each keep their own light up to the edge
    /// between them.
    /// </summary>
    [TestMethod]
    public void TestTheFilterKeepsANearThingFromAFarOne()
    {
        static double Truth(int x, int y) => x < Size / 2 ? 0.40 : 0.44;

        Canvas smoothed = Filtered(
            Grainy(Truth, 0.03), Flat((_, _) => 0.5, (x, _) => x < Size / 2 ? 10 : 30));
        double near = Enumerable.Range(0, Size).Average(y => smoothed.GetPixel(Size / 2 - 1, y).Red);
        double far = Enumerable.Range(0, Size).Average(y => smoothed.GetPixel(Size / 2, y).Red);

        Assert.AreEqual(0.40, near, 0.006, "the near surface's edge should keep its own light");
        Assert.AreEqual(0.44, far, 0.006, "the far surface's edge should keep its own light");
    }

    /// <summary>
    /// The denoiser asked for is the one had: none for off, the filter for the filter, Intel's when its tool is on
    /// the path, the filter in its place for the best when it is not, and Intel's refused when it is asked for and
    /// cannot be had.
    /// </summary>
    [TestMethod]
    public void TestTheDenoiserAskedForIsTheOneHad()
    {
        string empty = Path.Combine(Path.GetTempPath(), $"empty-{Guid.NewGuid():N}");
        string holding = Path.Combine(Path.GetTempPath(), $"holding-{Guid.NewGuid():N}");

        Directory.CreateDirectory(empty);
        Directory.CreateDirectory(holding);
        File.WriteAllText(Path.Combine(holding, OperatingSystem.IsWindows() ? "oidnDenoise.exe" : "oidnDenoise"), "");

        try
        {
            Assert.IsNull(Denoiser.For(DenoiseMethod.Off, holding));
            Assert.AreEqual(DenoiseMethod.Filter, Denoiser.For(DenoiseMethod.Filter, holding).Method);
            Assert.AreEqual(DenoiseMethod.Intel, Denoiser.For(DenoiseMethod.Best, holding).Method);
            Assert.AreEqual(DenoiseMethod.Intel, Denoiser.For(DenoiseMethod.Intel, holding).Method);
            Assert.AreEqual(DenoiseMethod.Filter, Denoiser.For(DenoiseMethod.Best, empty).Method);

            InvalidOperationException refused = Assert.ThrowsExactly<InvalidOperationException>(
                () => Denoiser.For(DenoiseMethod.Intel, empty));

            StringAssert.Contains(refused.Message, "oidnDenoise");
        }
        finally
        {
            Directory.Delete(empty, true);
            Directory.Delete(holding, true);
        }
    }

    /// <summary>
    /// When Intel's tool fails, the render is refused with what it said.
    /// </summary>
    [TestMethod]
    public void TestAFailingIntelToolIsReported()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("a shell script stands in for the tool");

            return;
        }

        string folder = Path.Combine(Path.GetTempPath(), $"failing-{Guid.NewGuid():N}");
        string tool = Path.Combine(folder, "oidnDenoise");

        Directory.CreateDirectory(folder);
        File.WriteAllText(tool, "#!/bin/sh\necho 'no such device' >&2\nexit 3\n");
        File.SetUnixFileMode(tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        try
        {
            static double Light(int x, int y) => 0.4;

            InvalidOperationException failed = Assert.ThrowsExactly<InvalidOperationException>(
                () => Denoiser.For(DenoiseMethod.Intel, folder)
                    .Denoise(Grainy(Light, 0.02), Flat((_, _) => 0.5), new SingleThreadScanner()));

            StringAssert.Contains(failed.Message, "exit code 3");
            StringAssert.Contains(failed.Message, "no such device");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// Intel's denoiser, when it is installed, smooths grain as the filter does: by more than a factor of five,
    /// keeping the light.
    /// </summary>
    [TestMethod]
    public void TestIntelsDenoiserSmoothsGrain()
    {
        if (IntelDenoiser.ToolOn(Environment.GetEnvironmentVariable("PATH")) is null)
            Assert.Inconclusive("Intel's denoiser is not installed");

        static double Truth(int x, int y) => 0.4;

        double before = OffBy(Grainy(Truth, 0.02), Truth);
        Canvas smoothed = Grainy(Truth, 0.02);

        Denoiser.For(DenoiseMethod.Intel).Denoise(smoothed, Flat((_, _) => 0.5), new SingleThreadScanner());

        double after = OffBy(smoothed, Truth);
        double mean = Enumerable.Range(0, Size * Size).Average(at => smoothed.GetPixel(at % Size, at / Size).Red);

        Assert.IsTrue(after < before / 5,
            $"the grain should be smoothed: {before:0.0000} before, {after:0.0000} after");
        Assert.AreEqual(0.4, mean, 0.004, "the light should be kept");
    }

    /// <summary>
    /// The command line's `--denoise` takes the place of what the scene says, and says nothing when it is not given.
    /// </summary>
    [TestMethod]
    public void TestTheCommandLineSaysHowToDenoise()
    {
        RenderContext context = new () { Denoise = DenoiseMethod.Filter };

        context.ApplyOptions(new RenderOptions());
        Assert.AreEqual(DenoiseMethod.Filter, context.Denoise, "a silent command line should leave the scene's");

        context.ApplyOptions(new RenderOptions { DenoiseText = "off" });
        Assert.AreEqual(DenoiseMethod.Off, context.Denoise);

        context.ApplyOptions(new RenderOptions { DenoiseText = "Intel" });
        Assert.AreEqual(DenoiseMethod.Intel, context.Denoise);

        Assert.ThrowsExactly<ArgumentException>(() => new RenderOptions { DenoiseText = "loudly" });
    }

    /// <summary>
    /// A scene asks for denoising in its context block, and it reaches the render: a floor and a ball under forty-eight
    /// lamps held in a tree that takes one pick a place comes out closer to the same scene lit by every lamp when its
    /// own filter smooths it -- by more than half -- and so does Intel's, when it is installed.  (A sky light looked
    /// at in a few directions is no test: a plain sky's are the same at every place, and too few make steps in the
    /// light, not grain.)
    /// </summary>
    [TestMethod]
    public void TestASceneAsksForDenoising()
    {
        Canvas truth = Lamplit("no light tree");
        Canvas grainy = Lamplit("light tree samples 1");
        Canvas smoothed = Lamplit("light tree samples 1  denoise filter");
        double before = Apart(grainy, truth);
        double after = Apart(smoothed, truth);

        Assert.IsTrue(after < before / 2,
            $"the filter should smooth the scene: {before:0.0000} before, {after:0.0000}");

        if (IntelDenoiser.ToolOn(Environment.GetEnvironmentVariable("PATH")) is not null)
        {
            double intel = Apart(Lamplit("light tree samples 1  denoise intel"), truth);

            Assert.IsTrue(intel < before / 2, $"Intel's should smooth it too: {before:0.0000} before, {intel:0.0000}");
        }
    }

    private static Canvas Lamplit(string context)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"denoise-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");

            File.WriteAllText(path,
                $"context {{ no gamma  scale ambient by 0  {context} }}\n" +
                "camera { location [0, 4, -7]  look at [0, 0.5, 0] }\n" +
                "plane { material { pigment [0.6, 0.6, 0.6]  specular 0 } }\n" +
                "sphere { translate [0, 1, 0]  material { pigment [0.8, 0.3, 0.2]  specular 0 } }\n" +
                string.Join("\n", Enumerable.Range(0, 48).Select(index =>
                    $"point light {{ location [{(index % 8 - 3.5) * 1.5}, 2.5, {(index / 8 - 2.5) * 1.5}]  " +
                    "fade distance 1.5  color [0.5, 0.5, 0.5] }")));
            new LanguageParser(path).Parse().Render(new RenderOptions
            {
                OutputFileName = Path.Combine(directory, "out.png"), Width = 48, Height = 36,
                ProgressStyleText = "none"
            });

            return new ImageFile(Path.Combine(directory, "out.png")).Load()[0];
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static double Apart(Canvas one, Canvas other)
    {
        double sum = 0;

        for (int y = 0; y < one.Height; y++)
        {
            for (int x = 0; x < one.Width; x++)
            {
                Color a = one.GetPixel(x, y), b = other.GetPixel(x, y);

                sum += Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue);
            }
        }

        return sum / (one.Width * one.Height * 3);
    }
}
