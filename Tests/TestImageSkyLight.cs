using ImageMagick;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover a sky that is a picture lighting a scene: a photograph of a real sky, sun and all.
/// Each is held to an exact answer, since a sky looked at by its light can be wrong by any amount and
/// still look plausible.
/// </summary>
[TestClass]
public class TestImageSkyLight
{
    // The sun the tests' skies hold: a block of texels in a picture 512 across and 256 down, well up the
    // sky and round toward +Z.
    private const int Width = 512;
    private const int Height = 256;
    private const int SunLeft = 100;
    private const int SunTop = 60;
    private const int SunSize = 4;
    private const double SunLight = 3000;

    private string _directory;

    [TestInitialize]
    public void MakeDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"image-sky-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        Directory.Delete(_directory, true);
    }

    /// <summary>
    /// This helper writes a Radiance picture of a sky, which holds light as it is, brighter than white
    /// included, and returns a pigment reading it round a sphere, made ready to be asked about.
    /// </summary>
    private ImagePigment SkyOf(Func<int, int, Color> lightAt)
    {
        string path = Path.Combine(_directory, $"{Guid.NewGuid():N}.hdr");

        using (MagickImage image = new (MagickColors.Black, Width, Height))
        {
            image.ColorSpace = ColorSpace.RGB;

            using (IPixelCollection<float> pixels = image.GetPixels())
            {
                foreach (IPixel<float> pixel in pixels)
                {
                    Color light = lightAt(pixel.X, pixel.Y);

                    pixel.SetChannel(0, (float) (light.Red * 65535));
                    pixel.SetChannel(1, (float) (light.Green * 65535));
                    pixel.SetChannel(2, (float) (light.Blue * 65535));
                }
            }

            image.Write(path, MagickFormat.Hdr);
        }

        ImagePigment pigment = new ()
        {
            ImageReference = new ImageReference { ImageName = Path.GetFileName(path), SourceDirectory = _directory },
            MapType = ImageMapType.Spherical
        };

        pigment.RenderingIsAboutToStart(new RenderContext(), null);

        return pigment;
    }

    private static bool InTheSun(int x, int y) =>
        x is >= SunLeft and < SunLeft + SunSize && y is >= SunTop and < SunTop + SunSize;

    /// <summary>
    /// This helper returns the direction toward the middle of the tests' sun.
    /// </summary>
    private static Vector TowardTheSun()
    {
        double around = 2 * Math.PI * (SunLeft + SunSize / 2.0) / Width;
        double up = Math.PI * (0.5 - (SunTop + SunSize / 2.0) / Height);

        return new Vector(Math.Cos(up) * Math.Cos(around), Math.Sin(up), Math.Cos(up) * Math.Sin(around));
    }

    /// <summary>
    /// This helper builds a scene of a floor under the given sky, the sky both shown and lighting it.
    /// </summary>
    private static Scene FloorUnder(ImagePigment sky, Material material, int samples = 64)
    {
        Plane floor = new () { Material = material };
        Scene scene = new () { Background = sky };

        floor.PrepareForRendering();
        scene.Surfaces.Add(floor);
        scene.Lights.Add(new SkyLight { Pigment = sky, Samples = samples });

        return scene;
    }

    /// <summary>
    /// This helper shades the floor at its origin as seen along the given heading.
    /// </summary>
    private static Color Seeing(Scene scene, Vector heading, int depth = 4) =>
        SeeingAt(scene, Point.Zero, heading, depth);

    /// <summary>
    /// This helper shades the floor at the given point as seen along the given heading.
    /// </summary>
    private static Color SeeingAt(Scene scene, Point where, Vector heading, int depth = 4)
    {
        Ray ray = new (where - heading * 3, heading);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        Assert.IsNotNull(hit, "the ray missed the floor");

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, depth);
    }

    private static Material Matte() => new ()
    {
        Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0.9, Specular = 0
    };

    /// <summary>
    /// This tests the exact answer every sky is held to: a floor under an even sky comes back as bright as
    /// the sky, times what the floor takes.  A picture's samples are picked two ways and each counted by
    /// both, and the counting is wrong by however far this misses.
    /// </summary>
    [TestMethod]
    public void TestAnEvenPictureSkyLightsAFloorToItsOwnDiffuse()
    {
        ImagePigment sky = SkyOf((_, _) => new Color(0.5, 0.5, 0.5));
        Color seen = Seeing(FloorUnder(sky, Matte()), Directions.Down);

        Assert.AreEqual(0.45, seen.Red, 0.0045, $"an even sky of 0.5 over a floor taking 0.9 gave {seen.Red}");
    }

    /// <summary>
    /// This tests that a sun in a picture is found, and lights a floor by exactly as much as it should:
    /// its light times the sky it covers times how squarely it meets the floor, each texel worked out
    /// exactly, over π.  Looked at evenly, thirty-two samples would almost never find a sun a hundred
    /// thousandth of the sky, and the one that did would bring back a hundred times too much.
    /// <para>
    /// Each point turns the directions it looks in by an amount of its own, so any one point is out by a
    /// few percent either way; it is the average over many points that must be exact, and the spread
    /// that must be small.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestASunInAPictureIsFoundAndCountedExactly()
    {
        ImagePigment sky = SkyOf((x, y) => InTheSun(x, y) ? new Color(SunLight, SunLight, SunLight) : Colors.Black);

        // Over each texel, the light falling on a floor is its light times the integral of the sine of
        // the elevation over the patch of sky it covers: round, the texel's width; up, the difference of
        // the squared sines of its edges, halved.
        double around = 2 * Math.PI / Width * SunSize;
        double top = Math.PI * (0.5 - (double) SunTop / Height);
        double bottom = Math.PI * (0.5 - (double) (SunTop + SunSize) / Height);
        double falling = SunLight * around * (Math.Pow(Math.Sin(top), 2) - Math.Pow(Math.Sin(bottom), 2)) / 2;
        double expected = 0.9 * falling / Math.PI;

        Scene scene = FloorUnder(sky, Matte(), 32);
        double[] seen = new double[400];

        for (int index = 0; index < seen.Length; index++)
        {
            Point where = new (index % 20 * 0.37, 0, index / 20 * 0.41);

            seen[index] = SeeingAt(scene, where, Directions.Down).Red;
        }

        double average = seen.Average();
        double spread = Math.Sqrt(seen.Select(value => (value - average) * (value - average)).Average());

        Assert.AreEqual(expected, average, expected * 0.01, $"the sun should give {expected}, and gave {average} on average");
        Assert.IsLessThan(expected * 0.1, spread, $"the sun's light should hardly vary from point to point: {spread}");
    }

    /// <summary>
    /// This tests that a rough surface's highlight and its reflection share a picture's sun fairly.  Each
    /// counted alone would be right on average, and the reflection alone, looking sixteen ways for a sun
    /// a few degrees across, finds it so seldom that it comes out as glitter; shared, sixteen ways must
    /// agree with three thousand.
    /// </summary>
    [TestMethod]
    public void TestARoughSurfaceSharesThePicturesSunWithItsReflection()
    {
        ImagePigment sky = SkyOf((x, y) => InTheSun(x, y) ? new Color(SunLight, SunLight, SunLight) : new Color(0.1, 0.2, 0.4));
        Vector sun = TowardTheSun();
        Vector heading = new (sun.X, -sun.Y, sun.Z);

        Color Rough(int looks) => Seeing(FloorUnder(sky, new Material
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0,
            Reflective = 1, Roughness = 0.25, ReflectionSamples = looks
        }), heading);

        Color few = Rough(16);
        Color many = Rough(3000);

        Assert.IsGreaterThan(1, many.Red, $"the sun should shine in the surface: {many}");
        Assert.AreEqual(many.Red, few.Red, many.Red * 0.05, $"sixteen looks gave {few.Red}, three thousand {many.Red}");
    }

    /// <summary>
    /// This tests that a smooth surface's highlight from a picture of the sky is the highlight of what each
    /// sample looks at: a red sun's highlight is red.  Taken from the light's color as a whole, as it is for
    /// a smooth sky, it was white whatever the sky held, and as bright from a black patch as from the sun.
    /// </summary>
    [TestMethod]
    public void TestASmoothHighlightTakesTheSunsOwnColor()
    {
        ImagePigment sky = SkyOf((x, y) => InTheSun(x, y) ? new Color(SunLight, 0, 0) : Colors.Black);
        Vector sun = TowardTheSun();
        Material shiny = new ()
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0.9, Shininess = 50
        };
        Color seen = Seeing(FloorUnder(sky, shiny), new Vector(sun.X, -sun.Y, sun.Z));

        Assert.IsGreaterThan(1, seen.Red, $"the sun should shine in the surface: {seen}");
        Assert.AreEqual(0, seen.Green, 1e-9, $"a red sun's highlight should be red: {seen}");
    }
}
