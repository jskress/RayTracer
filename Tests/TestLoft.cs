using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// The loft: a skin through cross-sections, found exactly.
/// </summary>
[TestClass]
public class TestLoft
{
    // A square, two across, from the middle of its right side counterclockwise.
    private static GeneralPath Square(double half = 1) => new GeneralPath()
        .MoveTo(half, 0).LineTo(half, half).LineTo(-half, half).LineTo(-half, -half).LineTo(half, -half).ClosePath();

    // A circle of radius one in four cubics, from (1, 0) counterclockwise.
    private static GeneralPath Circle()
    {
        const double k = 0.5522847498;

        return new GeneralPath()
            .MoveTo(1, 0)
            .CubicTo(new TwoDPoint(1, k), new TwoDPoint(k, 1), new TwoDPoint(0, 1))
            .CubicTo(new TwoDPoint(-k, 1), new TwoDPoint(-1, k), new TwoDPoint(-1, 0))
            .CubicTo(new TwoDPoint(-1, -k), new TwoDPoint(-k, -1), new TwoDPoint(0, -1))
            .CubicTo(new TwoDPoint(k, -1), new TwoDPoint(1, -k), new TwoDPoint(1, 0))
            .ClosePath();
    }

    private static LoftSection At(GeneralPath outline, double z) =>
        new () { Outline = outline, Transform = Transforms.Translate(0, 0, z) };

    private static List<Intersection> Crossings(Surface surface, Point from, Vector way)
    {
        List<Intersection> crossings = [];

        surface.Intersect(new Ray(from, way), crossings);
        crossings.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        return crossings;
    }

    /// <summary>
    /// Two squares two apart make a box: a ray across it meets its sides where they stand, a ray along it meets its
    /// flat ends, each crossing with its normal facing out -- and a ray from inside meets the side behind it as well as
    /// the one ahead.
    /// </summary>
    [TestMethod]
    public void TestTwoSquaresMakeABox()
    {
        Loft box = new ();

        box.Sections.AddRange([At(Square(), 0), At(Square(), 2)]);

        List<Intersection> across = Crossings(box, new Point(-5, 0.3, 1.2), new Vector(1, 0, 0));

        Assert.AreEqual(2, across.Count);
        Assert.AreEqual(4, across[0].Distance, 1e-9);
        Assert.AreEqual(6, across[1].Distance, 1e-9);
        Assert.IsTrue(box.NormalAt(new Point(-1, 0.3, 1.2), across[0]).Matches(new Vector(-1, 0, 0)));
        Assert.IsTrue(box.NormalAt(new Point(1, 0.3, 1.2), across[1]).Matches(new Vector(1, 0, 0)));

        List<Intersection> along = Crossings(box, new Point(0.2, -0.4, -3), new Vector(0, 0, 1));

        Assert.AreEqual(2, along.Count);
        Assert.AreEqual(3, along[0].Distance, 1e-9);
        Assert.AreEqual(5, along[1].Distance, 1e-9);
        Assert.IsTrue(box.NormalAt(new Point(0.2, -0.4, 0), along[0]).Matches(new Vector(0, 0, -1)));
        Assert.IsTrue(box.NormalAt(new Point(0.2, -0.4, 2), along[1]).Matches(new Vector(0, 0, 1)));

        List<Intersection> inside = Crossings(box, new Point(0, 0, 1), new Vector(0, 1, 0));

        Assert.AreEqual(2, inside.Count);
        Assert.AreEqual(-1, inside[0].Distance, 1e-9);
        Assert.AreEqual(1, inside[1].Distance, 1e-9);
        Assert.IsFalse(box.IsASheet);
    }

    /// <summary>
    /// The skin runs through every section, smoothly between them: through a square three across standing between two
    /// two across, it bulges past the straight line a third of the way between; said to be straight, it runs along it.
    /// </summary>
    [TestMethod]
    public void TestTheSkinRunsThroughEverySection()
    {
        Loft smooth = new ();
        Loft straight = new () { Straight = true };

        smooth.Sections.AddRange([At(Square(), 0), At(Square(1.5), 1), At(Square(), 2)]);
        straight.Sections.AddRange([At(Square(), 0), At(Square(1.5), 1), At(Square(), 2)]);

        Assert.AreEqual(5 - 1.5, Crossings(smooth, new Point(-5, 0, 1), new Vector(1, 0, 0))[0].Distance, 1e-9);
        Assert.AreEqual(5 - 1.5, Crossings(straight, new Point(-5, 0, 1), new Vector(1, 0, 0))[0].Distance, 1e-9);

        double bulged = 5 - Crossings(smooth, new Point(-5, 0, 0.5), new Vector(1, 0, 0))[0].Distance;
        double ruled = 5 - Crossings(straight, new Point(-5, 0, 0.5), new Vector(1, 0, 0))[0].Distance;

        Assert.AreEqual(1.25, ruled, 1e-9, "straight, halfway between, halfway out");
        Assert.IsTrue(bulged > 1.25 + 0.01, $"smooth, it swells past the straight line ({bulged})");
    }

    /// <summary>
    /// Sections drawn with different numbers of segments are matched by how far round each is: a square flowing into a
    /// circle meets it with its corners at the circle's eighths, so halfway up the corner's crease is halfway from the
    /// square's corner to the circle at 45 degrees.
    /// </summary>
    [TestMethod]
    public void TestDifferingSectionsMatchByHowFarRound()
    {
        Loft loft = new () { Straight = true };

        loft.Sections.AddRange([At(Square(), 0), At(Circle(), 2)]);

        double corner = Math.Sqrt(2), circle = 1;
        Point from = new (3, 3, 1);
        Intersection crossing = Crossings(loft, from, new Vector(-1, -1, 0).Unit)[0];

        Assert.AreEqual(3 * Math.Sqrt(2) - (corner + circle) / 2, crossing.Distance, 1e-6);
    }

    /// <summary>
    /// Sections along a spline stand square to it, at their shares of its length, the outline's X and Y laid across it:
    /// two squares along a straight spline running along +X make a box lying along X.
    /// </summary>
    [TestMethod]
    public void TestSectionsAlongASplineStandSquareToIt()
    {
        Spline spline = new () { Start = Point.Zero };

        spline.Segments.Add(new SplineSegmentSpec { End = new Point(4, 0, 0) });

        Loft loft = new () { Spline = spline };

        loft.Sections.AddRange([new LoftSection { Outline = Square(), At = 0 }, new LoftSection { Outline = Square(), At = 1 }]);

        List<Intersection> along = Crossings(loft, new Point(-2, 0.1, 0.2), new Vector(1, 0, 0));

        Assert.AreEqual(2, along.Count);
        Assert.AreEqual(2, along[0].Distance, 1e-9);
        Assert.AreEqual(6, along[1].Distance, 1e-9);

        List<Intersection> down = Crossings(loft, new Point(2, 5, 0.1), new Vector(0, -1, 0));

        Assert.AreEqual(2, down.Count);
        Assert.AreEqual(4, down[0].Distance, 1e-9);
    }

    /// <summary>
    /// A closed loft's normals face out however its sections were drawn and placed: clockwise outlines, and sections
    /// running toward -Z, make the same solid with its normals out.
    /// </summary>
    [TestMethod]
    public void TestNormalsFaceOutHoweverItIsDrawn()
    {
        GeneralPath clockwise = new GeneralPath()
            .MoveTo(1, 0).LineTo(1, -1).LineTo(-1, -1).LineTo(-1, 1).LineTo(1, 1).ClosePath();
        Loft backward = new ();

        backward.Sections.AddRange([At(clockwise, 0), At(clockwise, -2)]);

        foreach ((Point from, Vector way, Vector outward) in new[]
                 {
                     (new Point(-5, 0.2, -1), new Vector(1, 0, 0), new Vector(-1, 0, 0)),
                     (new Point(0.3, 5, -1), new Vector(0, -1, 0), new Vector(0, 1, 0)),
                     (new Point(0.3, 0.1, 4), new Vector(0, 0, -1), new Vector(0, 0, 1))
                 })
        {
            Intersection first = Crossings(backward, from, way)[0];
            Point at = from + way * first.Distance;

            Assert.IsTrue(backward.NormalAt(at, first).Matches(outward), $"from {from}");
        }
    }

    /// <summary>
    /// An open loft is a sheet, with nothing at its ends; so is one of outlines that do not close.  Outlines that close
    /// and ones that do not cannot be lofted together.
    /// </summary>
    [TestMethod]
    public void TestAnOpenLoftIsASheet()
    {
        Loft open = new () { Open = true };

        open.Sections.AddRange([At(Square(), 0), At(Square(), 2)]);

        Assert.IsTrue(open.IsASheet);
        Assert.AreEqual(0, Crossings(open, new Point(0, 0, -3), new Vector(0, 0, 1)).Count, "nothing at its ends");
        Assert.AreEqual(2, Crossings(open, new Point(-5, 0, 1), new Vector(1, 0, 0)).Count);

        GeneralPath arc = new GeneralPath().MoveTo(1, 0).LineTo(0, 1).LineTo(-1, 0);
        Loft mixed = new ();

        mixed.Sections.AddRange([At(arc, 0), At(Square(), 2)]);

        StringAssert.Contains(Refusal(() => mixed.Intersect(new Ray(Point.Zero, new Vector(1, 0, 0)), [])),
            "must all close, or none of them");
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
    /// This helper renders a small scene, and returns the picture, or what went wrong.
    /// </summary>
    private static (Canvas Image, string Output) Render(string scene)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"loft-tests-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "scene.igl");
        string output = Path.Combine(directory, "out.png");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, scene);
        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return (null, captured.ToString());

            renderer.Render(new RenderOptions { OutputFileName = output, Width = 24, Height = 24 });

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
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// A loft is written with its sections, each a path and its placing, and may stand along a named spline, in a
    /// combination, or be made by a primitive called more than once with a block of its own; what cannot be said is
    /// refused.
    /// </summary>
    [TestMethod]
    public void TestALoftIsWrittenAndRefusedAsItShouldBe()
    {
        const string Scene = """
            camera { location [0, 0, -6]  look at [0, 0, 0] }
            point light { location [-4, 6, -8] }
            Box = path { move to 1, 0  line to 1, 1  line to -1, 1  line to -1, -1  line to 1, -1  close }
            Rail = spline { move to 0, -1, 0  line to 0, 1, 0 }
            primitive Post(wide) -> loft {
                return loft {
                    section { path Box  scale wide }
                    section { path Box  scale wide * 0.5  translate Z 2 }
                }
            }
            difference {
                loft { section { path Box  rotate X 90  translate Y -1 }  section { path Box  rotate X 90  translate Y 1 } }
                sphere { scale 0.5  translate Z -1 }
            }
            loft { section { path Box  scale 0.2  at 0 }  section { path Box  scale 0.1  at 1 }  spline Rail  straight  translate X 2 }
            object Post(0.3) { translate X -2 }
            object Post(0.3) { translate [-2, 1, 0] }
            """;

        (Canvas image, string output) = Render(Scene);

        Assert.IsNotNull(image, output);

        StringAssert.Contains(Render("loft { section { path Box } }\nBox = path { move to 0, 0 line to 1, 0 close }").Output,
            "at least two sections");
        StringAssert.Contains(Render("""
            camera { location [0, 0, -6]  look at [0, 0, 0] }
            Box = path { move to 1, 0  line to 1, 1  line to -1, 1  close }
            loft { section { path Box  at 0 }  section { path Box  translate Z 1 } }
            """).Output, "has no spline");
        StringAssert.Contains(Render("""
            camera { location [0, 0, -6]  look at [0, 0, 0] }
            Rail = spline { move to 0, -1, 0  line to 0, 1, 0 }
            Box = path { move to 1, 0  line to 1, 1  line to -1, 1  close }
            loft { section { path Box  at 0 }  section { path Box  at 1.5 }  spline Rail }
            """).Output, "a share of its loft's spline");
    }
}
