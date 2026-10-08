using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;

namespace Tests;

[TestClass]
public class TestMetallic
{
    private static Material Gold(double metallic)
    {
        return new Material
        {
            Pigment = new SolidPigment(new Color(1, 0.8, 0.2)),
            Metallic = metallic
        };
    }

    [TestMethod]
    public void TestNoMetallicLeavesTheLightsColorAlone()
    {
        // A dielectric reflects the light's own color, so the tint has to be a no-op.
        Color tint = Gold(0).GetMetallicTint(new Color(1, 0.8, 0.2), 1);

        Assert.IsTrue(tint.Matches(Colors.White), tint.ToString());
    }

    [TestMethod]
    public void TestFullMetallicTakesTheSurfaceColorHeadOn()
    {
        // Meeting the surface head on, the empirical Fresnel term is ~0, so a fully metallic
        // surface tints all the way to its own color.
        Color surface = new (1, 0.8, 0.2);
        Color tint = Gold(1).GetMetallicTint(surface, 1);

        Assert.IsTrue(tint.Matches(surface), tint.ToString());
    }

    [TestMethod]
    public void TestTheTintFallsAwayAtGrazingAngles()
    {
        // At grazing incidence everything becomes a colorless mirror, metal included, so the
        // tint has to return to white however metallic the surface is.
        Color tint = Gold(1).GetMetallicTint(new Color(1, 0.8, 0.2), 0);

        Assert.IsTrue(tint.Matches(Colors.White), tint.ToString());
    }

    [TestMethod]
    public void TestTheTintIsStillNearlyFullWellOffTheNormal()
    {
        // The falloff is not linear in angle: the fitted curve stays close to zero across most of
        // the surface and only climbs near the silhouette.  At 45 degrees the tint should still be
        // most of the way to the surface's color, not halfway.
        Color surface = new (1, 0, 0);
        Color tint = Gold(1).GetMetallicTint(surface, Math.Cos(Math.PI / 4));

        Assert.IsTrue(tint.Green < 0.05, $"Expected a nearly full tint, got {tint}.");
    }

    [TestMethod]
    public void TestAMetallicHighlightTakesTheSurfaceColor()
    {
        // The visible payoff: a white light on a gold sphere makes a gold highlight, where the
        // same light on a dielectric makes a white one.
        Sphere plain = new () { Material = Gold(0) };
        Sphere metal = new () { Material = Gold(1) };
        PointLight light = new () { Location = new Point(0, 0, -10) };
        Point point = new (0, 0, -1);
        Vector eye = new (0, 0, -1);
        Vector normal = new (0, 0, -1);

        Color plainColor = light.ApplyPhong(point, eye, normal, plain, Colors.White);
        Color metalColor = light.ApplyPhong(point, eye, normal, metal, Colors.White);

        // Both are lit the same; only the highlight differs, and it drags the blue channel down
        // toward the gold's own small blue component.
        Assert.IsTrue(metalColor.Blue < plainColor.Blue,
            $"Expected the metallic highlight to be less blue: {metalColor} vs {plainColor}.");
        Assert.IsTrue(metalColor.Red.Near(plainColor.Red),
            $"Red is already full in the gold, so it should not change: {metalColor} vs {plainColor}.");
    }

    [TestMethod]
    public void TestMetallicIsIgnoredWithoutAHighlight()
    {
        // With no specular term there is no highlight to tint, so metallic must make no
        // difference at all -- the same rule POV-Ray applies.
        Material plain = Gold(0);
        Material metal = Gold(1);

        plain.Specular = metal.Specular = 0;

        Sphere plainSphere = new () { Material = plain };
        Sphere metalSphere = new () { Material = metal };
        PointLight light = new () { Location = new Point(0, 0, -10) };
        Point point = new (0, 0, -1);
        Vector eye = new (0, 0, -1);
        Vector normal = new (0, 0, -1);

        Color plainColor = light.ApplyPhong(point, eye, normal, plainSphere, Colors.White);
        Color metalColor = light.ApplyPhong(point, eye, normal, metalSphere, Colors.White);

        Assert.IsTrue(plainColor.Matches(metalColor), $"{plainColor} vs {metalColor}");
    }

    /// <summary>
    /// A bare <c>metallic</c> means fully metallic wherever it is written in a material, not only as the
    /// last thing in it: followed by another entry, it does not take that entry's word for its amount.  A
    /// red ball that mirrors a white sky shows it -- red if it is both metallic and reflective, white if
    /// the metal was lost, black if the reflection was.
    /// </summary>
    [TestMethod]
    public void TestABareMetallicMayBeFollowedByAnotherEntry()
    {
        Color red = new (1, 0, 0);

        AssertNear(red, RenderedBall("metallic  reflective 1"), "metallic before reflective");
        AssertNear(red, RenderedBall("reflective 1  metallic"), "metallic last");
        AssertNear(red, RenderedBall("metallic  specular 0  reflective 1"), "two entries after it");
        AssertNear(red, RenderedBall("reflective 1  metallic  pigment [1, 0, 0]"), "a pigment after it");
        AssertNear(red, RenderedBall("reflective 1  metallic  interior { ior 1 }"), "an interior after it");
        AssertNear(red, RenderedBall("reflective 1  metallic  fresnel"), "fresnel after it");

        // The amount after the next entry's word is that entry's, not the metal's: fully metallic and
        // reflective 0.8 is a darker red, where a metal 0.8 metallic would let some white through.
        AssertNear(new Color(0.8, 0, 0), RenderedBall("metallic  reflective 0.8"), "the amount is the next entry's");
    }

    /// <summary>
    /// <c>metallic</c> still takes an amount, written out or named -- even named with a word the
    /// language uses for something else, as long as it is not a material's own.
    /// </summary>
    [TestMethod]
    public void TestMetallicStillTakesAnAmount()
    {
        Color half = new (1, 0.5, 0.5);

        AssertNear(half, RenderedBall("metallic 0.5  reflective 1"), "a number");
        AssertNear(half, RenderedBall("metallic amount  reflective 1", "amount = 0.5"), "a name");
        AssertNear(half, RenderedBall("metallic height  reflective 1", "height = 0.5"), "a keyword's name");
        AssertNear(half, RenderedBall("metallic amount * 1  reflective 1", "amount = 0.5"), "an expression");
    }

    /// <summary>
    /// This method renders a red ball that mirrors a white sky, wearing the given finish, and returns the
    /// color in its middle.
    /// </summary>
    private static Color RenderedBall(string finish, string before = "")
    {
        string directory = Path.Combine(Path.GetTempPath(), $"metallic-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            File.WriteAllText(path,
                "context { no gamma }\n" +
                $"{before}\n" +
                "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
                "background White\n" +
                "sphere { material { pigment [1, 0, 0]  ambient 0  diffuse 0  specular 0  " +
                finish + " } }");

            new LanguageParser(path).Parse().Render(new RenderOptions
            {
                OutputFileName = output, Width = 9, Height = 9, ProgressStyleText = "none"
            });

            return new ImageFile(output).Load()[0].GetPixel(4, 4);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// This method asserts that a color read back from an eight-bit image matches the one expected.
    /// </summary>
    private static void AssertNear(Color expected, Color actual, string what)
    {
        const double level = 2.0 / 255;

        Assert.AreEqual(expected.Red, actual.Red, level, $"{what}: red, {actual} for {expected}");
        Assert.AreEqual(expected.Green, actual.Green, level, $"{what}: green, {actual} for {expected}");
        Assert.AreEqual(expected.Blue, actual.Blue, level, $"{what}: blue, {actual} for {expected}");
    }
}
