using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace Tests;

[TestClass]
public class TestImagePigment
{
    private string _directory;

    [TestInitialize]
    public void MakeDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"image-pigment-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        Directory.Delete(_directory, true);
    }

    /// <summary>
    /// This helper writes an eight-bit PNG of grays as a picture file holds them, and returns an image
    /// pigment that reads it, made ready to be asked about.
    /// </summary>
    private ImagePigment PigmentOf(
        int width, int height, Func<int, int, double> grayAt, ImageMapType mapType, bool once = false,
        RenderContext context = null)
    {
        string name = $"{Guid.NewGuid():N}.png";
        Canvas canvas = new (width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double gray = grayAt(x, y);

                canvas.SetColor(new Color(gray, gray, gray), x, y);
            }
        }

        // Written with no gamma, the file holds exactly the values given, as a picture's would.
        new ImageFile(Path.Combine(_directory, name)).Save(canvas, new RenderContext { ApplyGamma = false });

        ImagePigment pigment = new ()
        {
            ImageReference = new ImageReference { ImageName = name, SourceDirectory = _directory },
            MapType = mapType,
            Once = once
        };

        pigment.RenderingIsAboutToStart(context ?? new RenderContext(), null);

        return pigment;
    }

    /// <summary>
    /// This tests that a mid-gray picture is read as the light it stands for.  Taken as the file held
    /// it, 128 was half of white's light and rendered as 186; it is a fifth of it, and renders as itself.
    /// </summary>
    [TestMethod]
    public void TestAMidGrayPictureIsAFifthOfWhitesLight()
    {
        ImagePigment pigment = PigmentOf(4, 4, (_, _) => 128 / 255.0, ImageMapType.Planar);
        Color color = pigment.GetColorFor(new Point(0.4, 0, 0.6));

        Assert.AreEqual(0.21586, color.Red, 1e-4);
    }

    /// <summary>
    /// This tests that a scene that writes no gamma takes an image's values as they stand, as it takes
    /// its own colors: the value written is the value that reaches the file, so a mid-gray picture under
    /// white light still comes back as itself.
    /// </summary>
    [TestMethod]
    public void TestASceneWithNoGammaTakesAnImageAsItStands()
    {
        ImagePigment pigment = PigmentOf(
            4, 4, (_, _) => 128 / 255.0, ImageMapType.Planar, context: new RenderContext { ApplyGamma = false });
        Color color = pigment.GetColorFor(new Point(0.4, 0, 0.6));

        Assert.AreEqual(128 / 255.0, color.Red, 1e-4);
    }

    /// <summary>
    /// This tests that a cylindrical map reaches below its axis's nought.  The row was found as a
    /// remainder, which is negative for a negative height, and the render died on it.
    /// </summary>
    [TestMethod]
    public void TestACylindricalMapReachesBelowItsAxisNought()
    {
        ImagePigment pigment = PigmentOf(8, 8, (x, y) => (x + 2 * y) / 24.0, ImageMapType.Cylindrical);
        Color below = pigment.GetColorFor(new Point(1, -0.3, 0.2));
        Color tiled = pigment.GetColorFor(new Point(1, 0.7, 0.2));

        Assert.AreEqual(tiled.Red, below.Red, 1e-9);
    }

    /// <summary>
    /// This tests that an image is averaged over the patch a footprint covers.  A checker of single
    /// texels point-sampled comes back black or white; over a patch a dozen texels across it is half of
    /// white's light.
    /// </summary>
    [TestMethod]
    public void TestAnImageIsAveragedOverAFootprint()
    {
        ImagePigment pigment = PigmentOf(64, 64, (x, y) => (x + y) % 2, ImageMapType.Planar);
        Point point = new (0.5 + 11.5 / 64, 0, 0.5 + 3.5 / 64);
        Footprint footprint = new (new Vector(0.2, 0, 0), new Vector(0, 0.3, 0.2));

        Color sampled = pigment.GetColorFor(point);
        Color averaged = pigment.GetColorFor(point, footprint);

        Assert.AreEqual(0, sampled.Red, 1e-9, "a texel's middle, read at a point, is that texel");
        Assert.AreEqual(0.5, averaged.Red, 0.03);
    }

    /// <summary>
    /// This tests that an image drawn once fades out at its edge over the patch a pixel covers, rather
    /// than stopping on a texel, and is clear beyond it.
    /// </summary>
    [TestMethod]
    public void TestAnImageDrawnOnceFadesOutAtItsEdge()
    {
        ImagePigment pigment = PigmentOf(16, 16, (_, _) => 1, ImageMapType.Planar, once: true);
        Footprint footprint = new (new Vector(0.25, 0, 0), new Vector(0, 0, 0.25));

        Color atTheEdge = pigment.GetColorFor(new Point(1, 0, 0.5), footprint);
        Color beyond = pigment.GetColorFor(new Point(1.5, 0, 0.5), footprint);
        Color inside = pigment.GetColorFor(new Point(0.5, 0, 0.5), footprint);

        Assert.AreEqual(0.5, atTheEdge.Alpha, 0.05);
        Assert.AreEqual(1, atTheEdge.Red, 1e-6, "the fade is of opacity, not of color");
        Assert.AreEqual(0, beyond.Alpha, 1e-9);
        Assert.AreEqual(1, inside.Alpha, 1e-9);
    }

    /// <summary>
    /// This tests that a sky read from an image is averaged over what a pixel sees of it.  A ray that
    /// saw nothing reads the sky through a cone as wide as its pixel, and a picture of the sky finer than
    /// that is the moiré a floor's checker would be.
    /// </summary>
    [TestMethod]
    public void TestASkyIsAveragedOverWhatAPixelSees()
    {
        Scene scene = new ()
        {
            Background = PigmentOf(256, 128, (x, y) => (x + y) % 2, ImageMapType.Spherical)
        };

        // Square on to the middle of texel (10, 40), which is black.
        double theta = 2 * Math.PI * 10.5 / 256;
        double up = Math.Sin(Math.PI * ((128 - 40.5) / 128 - 0.5));
        double round = Math.Sqrt(1 - up * up);
        Vector heading = new (round * Math.Cos(theta), up, round * Math.Sin(theta));

        Color narrow = scene.GetColorFor(new Ray(new Point(0, 0, 0), heading, spread: 1e-6));
        Color wide = scene.GetColorFor(new Ray(new Point(0, 0, 0), heading, spread: 0.2));

        Assert.AreEqual(0, narrow.Red, 1e-3);
        Assert.AreEqual(0.5, wide.Red, 0.03);
    }

    /// <summary>
    /// This tests that a pattern sky is read at a point and not over a patch.  The usual one is a
    /// gradient up from the horizon that repeats below it, hidden by the ground; averaged over a patch
    /// straddling the horizon, the top of the sky leaked into the row of pixels along it as a blue line.
    /// </summary>
    [TestMethod]
    public void TestAPatternSkyIsReadAtAPoint()
    {
        PigmentSet map = new ();

        map.AddEntry(new SolidPigment(new Color(1, 0.9, 0.7)));
        map.AddEntry(new SolidPigment(new Color(0.1, 0.3, 0.7)), 1);

        Scene scene = new ()
        {
            Background = new PatternPigment
            {
                Pattern = new GradientPattern { BandType = BandType.LinearY }, PigmentSet = map
            }
        };
        Vector justAbove = new Vector(0, 0.002, 1).Unit;

        Color narrow = scene.GetColorFor(new Ray(new Point(0, 0, 0), justAbove, spread: 1e-6));
        Color wide = scene.GetColorFor(new Ray(new Point(0, 0, 0), justAbove, spread: 0.05));

        Assert.IsTrue(narrow.Matches(wide), $"the horizon read {wide} through a wide pixel and {narrow} at a point");
    }
}
