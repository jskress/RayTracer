using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover a rounded extrusion: every edge of it a bead of the rounding's radius, held to the shape
/// such a solid has -- flat faces inside the outline drawn in by the radius, a quarter round down to the sides,
/// the sides where the outline was drawn, and a corner a sphere's eighth -- wherever that is exact, and to the
/// fitting's tolerance where the outline is curved.
/// </summary>
[TestClass]
public class TestRoundedExtrusion
{
    private const double Radius = 0.25;

    private static GeneralPath Square(double half) => new GeneralPath()
        .MoveTo(-half, -half).LineTo(half, -half).LineTo(half, half).LineTo(-half, half).ClosePath();

    // A circle of four cubics, the usual quarter-circle reach.
    private static GeneralPath Circle(double radius)
    {
        double k = 0.5522847498307936 * radius;

        return new GeneralPath()
            .MoveTo(radius, 0)
            .CubicTo(new TwoDPoint(radius, k), new TwoDPoint(k, radius), new TwoDPoint(0, radius))
            .CubicTo(new TwoDPoint(-k, radius), new TwoDPoint(-radius, k), new TwoDPoint(-radius, 0))
            .CubicTo(new TwoDPoint(-radius, -k), new TwoDPoint(-k, -radius), new TwoDPoint(0, -radius))
            .CubicTo(new TwoDPoint(k, -radius), new TwoDPoint(radius, -k), new TwoDPoint(radius, 0));
    }

    private static Extrusion Rounded(GeneralPath path, double rounded = Radius) =>
        Prepared(new Extrusion { Path = path, MinimumY = 0, MaximumY = 1, Rounded = rounded });

    private static T Prepared<T>(T surface) where T : Surface
    {
        surface.PrepareForRendering();

        return surface;
    }

    // Where a ray first meets the surface, and the normal there; null where it meets nothing.
    private static (Point Point, Vector Normal)? Met(Surface surface, Point from, Vector toward)
    {
        Ray ray = new (from, toward.Unit);
        List<Intersection> hits = [];

        surface.Intersect(ray, hits);

        Intersection hit = hits.Where(candidate => candidate.Distance > 0).OrderBy(candidate => candidate.Distance).FirstOrDefault();

        if (hit is null)
            return null;

        hit.PrepareUsing(ray, hits);

        return (hit.Point, hit.Normal);
    }

    /// <summary>
    /// This tests a rounded slab's edge, along a straight side: flat on top inside the outline drawn in, a
    /// quarter round from there down to the side, and the side standing where the outline was drawn -- its
    /// heights and normals all exactly a quarter circle's.
    /// </summary>
    [TestMethod]
    public void TestARoundedEdgeIsAQuarterRound()
    {
        Extrusion slab = Rounded(Square(2));

        foreach (double x in new[] { 0.0, 1.5, 1.74, 1.8, 1.9, 1.99 })
        {
            (Point point, Vector normal) = Met(slab, new Point(x, 5, 0.3), new Vector(0, -1, 0)).Value;
            double over = Math.Max(0, x - (2 - Radius));
            double height = 1 - Radius + Math.Sqrt(Radius * Radius - over * over);

            Assert.AreEqual(height, point.Y, 1e-9, $"the height at {x}");
            Assert.AreEqual(over / Radius, normal.X, 1e-6, $"the normal at {x}");
        }

        foreach (double y in new[] { 0.3, 0.5, 0.74, 0.8, 0.95 })
        {
            (Point point, Vector normal) = Met(slab, new Point(5, y, 0.3), new Vector(-1, 0, 0)).Value;
            double up = Math.Max(0, y - (1 - Radius));
            double reach = 2 - Radius + Math.Sqrt(Radius * Radius - up * up);

            Assert.AreEqual(reach, point.X, 1e-9, $"the side at {y}");
            Assert.AreEqual(up / Radius, normal.Y, 1e-6, $"the normal at {y}");
        }
    }

    /// <summary>
    /// This tests a rounded slab's corner: the eighth of a ball round the corner of the outline drawn in.
    /// </summary>
    [TestMethod]
    public void TestARoundedCornerIsABallsEighth()
    {
        Extrusion slab = Rounded(Square(2));
        double inner = 2 - Radius;

        foreach (double off in new[] { 0.05, 0.1, 0.15 })
        {
            Point point = Met(slab, new Point(inner + off, 5, inner + off), new Vector(0, -1, 0)).Value.Point;

            Assert.AreEqual(1 - Radius + Math.Sqrt(Radius * Radius - 2 * off * off), point.Y, 1e-9, $"{off} off");
        }

        // Straight across the corner, the side is a quarter cylinder round it.
        Point side = Met(slab, new Point(5, 0.5, 5), new Vector(-1, 0, -1)).Value.Point;

        Assert.AreEqual(inner + Radius * Math.Sqrt(0.5), side.X, 1e-9, "across the corner");
    }

    /// <summary>
    /// This tests a curved outline, a disc: its edge rounded as a straight one is, to within the tolerance the
    /// outline drawn in is fitted to.
    /// </summary>
    [TestMethod]
    public void TestACurvedEdgeIsRoundedToo()
    {
        const double disc = 3;
        Extrusion slab = Rounded(Circle(disc));

        foreach (double angle in new[] { 0.1, 0.7, 2.0, 4.1 })
        {
            foreach (double over in new[] { 0.05, 0.15, 0.24 })
            {
                double reach = disc - Radius + over;
                Point point = Met(slab, new Point(reach * Math.Cos(angle), 5, reach * Math.Sin(angle)), new Vector(0, -1, 0))
                    .Value.Point;

                // The quarter circle's height there; the outline is a cubic circle, itself 0.03% off round.
                Assert.AreEqual(1 - Radius + Math.Sqrt(Radius * Radius - over * over), point.Y, 3e-3,
                    $"{over} over at {angle}");
            }
        }
    }

    /// <summary>
    /// This tests a long, unevenly bending outline -- a cubic run whose drawn-in curve no one cubic follows -- held
    /// to the exact rounding along the outline's own normals: places a given distance in from it, at the height
    /// a quarter round of the rounding's radius stands there, within a few times the fitting's tolerance.
    /// </summary>
    [TestMethod]
    public void TestAnUnevenOutlineIsRoundedTrue()
    {
        // A lozenge: two cubics, each bending hard near one end and hardly at all near the other.
        TwoDPoint a = new (-6, 0), b = new (6, 0);
        GeneralPath outline = new GeneralPath()
            .MoveTo(a)
            .CubicTo(new TwoDPoint(-6, -1.5), new TwoDPoint(4, -3), b)
            .CubicTo(new TwoDPoint(4, 3), new TwoDPoint(-6, 1.5), a);
        Extrusion slab = Rounded(outline);
        double tolerance = 1e-4 * 12;
        int checkedPlaces = 0;

        foreach ((TwoDPoint p0, TwoDPoint p1, TwoDPoint p2, TwoDPoint p3) in new[]
                 {
                     (a, new TwoDPoint(-6, -1.5), new TwoDPoint(4, -3), b),
                     (b, new TwoDPoint(4, 3), new TwoDPoint(-6, 1.5), a)
                 })
        {
            for (double t = 0.08; t < 0.95; t += 0.07)
            {
                double u = 1 - t;
                double x = u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X;
                double z = u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y;
                double dx = 3 * u * u * (p1.X - p0.X) + 6 * u * t * (p2.X - p1.X) + 3 * t * t * (p3.X - p2.X);
                double dz = 3 * u * u * (p1.Y - p0.Y) + 6 * u * t * (p2.Y - p1.Y) + 3 * t * t * (p3.Y - p2.Y);
                double length = Math.Sqrt(dx * dx + dz * dz);
                // The normal pointing in, whichever way the run happens to go.
                (double nx, double nz) = (-dz / length, dx / length);

                if ((x + nx * 0.01) * (x + nx * 0.01) / 36 + (z + nz * 0.01) * (z + nz * 0.01) > x * x / 36 + z * z)
                    (nx, nz) = (-nx, -nz);

                foreach (double over in new[] { 0.05, 0.1, 0.15 })
                {
                    double inward = Radius - over;
                    Point point = Met(slab, new Point(x + nx * inward, 5, z + nz * inward), new Vector(0, -1, 0)).Value.Point;

                    Assert.AreEqual(1 - Radius + Math.Sqrt(Radius * Radius - over * over), point.Y, 3 * tolerance,
                        $"{over} over at t {t:0.##} of the run from {p0}");
                    checkedPlaces++;
                }
            }
        }

        Assert.IsGreaterThan(50, checkedPlaces);
    }

    /// <summary>
    /// This tests a hole through a rounded slab: its rim is rounded down into it as the outside edge is
    /// rounded down off it.
    /// </summary>
    [TestMethod]
    public void TestAHolesRimIsRounded()
    {
        GeneralPath outline = Square(3);

        outline.MoveTo(-1, -1).LineTo(-1, 1).LineTo(1, 1).LineTo(1, -1).ClosePath();

        Extrusion slab = Rounded(outline);

        Assert.IsNull(Met(slab, new Point(0, 5, 0), new Vector(0, -1, 0)), "the hole's middle");

        foreach (double x in new[] { 1.05, 1.15, 1.24, 1.5 })
        {
            Point point = Met(slab, new Point(x, 5, 0), new Vector(0, -1, 0)).Value.Point;
            double over = Math.Max(0, 1 + Radius - x);

            Assert.AreEqual(1 - Radius + Math.Sqrt(Radius * Radius - over * over), point.Y, 1e-9, $"at {x}");
        }
    }

    /// <summary>
    /// This tests a rounded slab as one side of a CSG: a cylinder taken out of it leaves a hole a ray goes
    /// straight down through, and a ray beside the hole still meets the slab's top.
    /// </summary>
    [TestMethod]
    public void TestARoundedSlabIsASolid()
    {
        CsgSurface cut = Prepared(new CsgSurface
        {
            Operation = CsgOperation.Difference,
            Left = new Extrusion { Path = Square(2), MinimumY = 0, MaximumY = 1, Rounded = Radius },
            Right = new Cylinder { MinimumY = -1, MaximumY = 2, Transform = Transforms.Scale(0.5, 1, 0.5) }
        });

        Assert.IsNull(Met(cut, new Point(0, 5, 0), new Vector(0, -1, 0)), "down through the hole");
        Assert.AreEqual(1, Met(cut, new Point(1, 5, 0), new Vector(0, -1, 0)).Value.Point.Y, 1e-9, "beside it");

        // Looking straight across inside it, a ray meets the hole's wall, half a unit from its middle.
        Assert.AreEqual(0.5, Met(cut, new Point(0, 0.5, 0), new Vector(1, 0, 0)).Value.Point.X, 1e-9, "the hole's wall");
    }

    /// <summary>
    /// This tests what a rounded extrusion's parts wear: its material, every one of them, whichever part a ray
    /// meets.
    /// </summary>
    [TestMethod]
    public void TestEveryPartWearsTheMaterial()
    {
        Material red = new () { Pigment = new SolidPigment(new Color(1, 0, 0)) };
        Extrusion slab = Prepared(new Extrusion { Path = Square(2), MinimumY = 0, MaximumY = 1, Rounded = Radius, Material = red });

        foreach ((Point from, Vector toward) in new[]
                 {
                     (new Point(0, 5, 0), new Vector(0, -1, 0)), (new Point(1.9, 5, 0), new Vector(0, -1, 0)),
                     (new Point(5, 0.5, 0), new Vector(-1, 0, 0))
                 })
        {
            Ray ray = new (from, toward);
            List<Intersection> hits = [];

            slab.Intersect(ray, hits);

            Intersection hit = hits.Where(candidate => candidate.Distance > 0).MinBy(candidate => candidate.Distance);

            Assert.IsTrue(ReferenceEquals(red, hit.Surface.Material) || red.Matches(hit.Surface.Material),
                $"the part met from {from} wears its own material");
        }
    }

    /// <summary>
    /// This tests what is refused: a rounding more than half the thickness, one tighter than a curve of the
    /// outline, and a rounded extrusion that also tapers; and that a scene may say it.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysHowRoundAnExtrusionIs()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rounded-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string ErrorFrom(string body)
            {
                string path = Path.Combine(directory, "scene.igl");

                File.WriteAllText(path,
                    "camera { location [0, 4, -6]  look at [0, 0, 0] }\npoint light { location [-4, 6, -6] }\n" + body + "\n");

                StringWriter captured = new ();
                TextWriter was = Console.Out;

                Console.SetOut(captured);

                try
                {
                    ImageRenderer renderer = new LanguageParser(path).Parse();

                    if (renderer is null)
                        return "did not parse";

                    renderer.Render(new RenderOptions
                    {
                        OutputFileName = Path.Combine(directory, "out.png"), Width = 6, Height = 6
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

                string said = captured.ToString();

                return said.Contains("Error") ? said : null;
            }

            const string square = "path { move to -1, -1  line to 1, -1  line to 1, 1  line to -1, 1  close }";
            const string small = "path { move to 0.2, 0  curve 0.2, 0.11, 0.11, 0.2 to 0, 0.2  curve -0.11, 0.2, -0.2, 0.11 to -0.2, 0" +
                                 "  curve -0.2, -0.11, -0.11, -0.2 to 0, -0.2  curve 0.11, -0.2, 0.2, -0.11 to 0.2, 0 }";

            Assert.IsNull(ErrorFrom($"extrusion {{ {square}  min Y 0  max Y 1  rounded 0.2 }}"), "a rounded extrusion");
            Assert.IsNull(ErrorFrom($"Box = extrusion {{ {square}  min Y 0  max Y 1  rounded 0.2 }}\nobject Box"), "a named one");
            StringAssert.Contains(ErrorFrom($"extrusion {{ {square}  min Y 0  max Y 1  rounded 0.6 }}"), "half");
            StringAssert.Contains(ErrorFrom($"extrusion {{ {small}  min Y 0  max Y 1  rounded 0.25 }}"), "tightly");
            // A bar 0.4 wide, rounded by a quarter: its ends too short and its sides too near to be drawn in so far.
            StringAssert.Contains(
                ErrorFrom("extrusion { path { move to -2, -0.2  line to 2, -0.2  line to 2, 0.2  line to -2, 0.2  close }  min Y 0  max Y 1  rounded 0.25 }"),
                "round it by less");
            // A peanut whose waist is 0.3 across, every curve of it gentle: drawn in by a quarter, it crosses itself.
            StringAssert.Contains(
                ErrorFrom("extrusion { path { move to 0, -0.15  curve 1.5, -0.15, 3, -1.5 to 3, 0  curve 3, 1.5, 1.5, 0.15 to 0, 0.15" +
                          "  curve -1.5, 0.15, -3, 1.5 to -3, 0  curve -3, -1.5, -1.5, -0.15 to 0, -0.15 }  min Y 0  max Y 1  rounded 0.25 }"),
                "narrower");
            StringAssert.Contains(ErrorFrom($"tapered extrusion {{ taper 0.5  {square}  min Y 0  max Y 1  rounded 0.2 }}"), "taper");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
