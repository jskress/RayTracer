using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// The strand: a fiber hit as a ribbon that always faces the ray and shaded as though it were round, and
/// the compact set that holds a great many of them.
/// </summary>
[TestClass]
public class TestStrand
{
    /// <summary>
    /// A straight strand is hit where it lies, within half its width of the ray and no further.
    /// </summary>
    [TestMethod]
    public void TestAStraightStrandIsHitWithinHalfItsWidth()
    {
        Strand strand = StraightStrand(0.2, 0.2);

        Assert.AreEqual(5, OnlyHit(strand, 0, 0).Distance, 1e-9);
        Assert.AreEqual(5, OnlyHit(strand, 0.4, 0.09).Distance, 1e-9);
        Assert.AreEqual(0, Hits(strand, 0.4, 0.11).Count, "a ray past half the width hit");
        Assert.AreEqual(0, Hits(strand, 1.2, 0).Count, "a ray past the strand's end hit");
    }

    /// <summary>
    /// A strand narrows evenly from one width to the other along its length.
    /// </summary>
    [TestMethod]
    public void TestAStrandTapersAlongItsLength()
    {
        // From 0.2 at x = -1 to nought at x = 1, so 0.05 at x = 0.5: half of it is 0.025.
        Strand strand = StraightStrand(0.2, 0);

        Assert.AreEqual(1, Hits(strand, 0.5, 0.024).Count);
        Assert.AreEqual(0, Hits(strand, 0.5, 0.026).Count);
    }

    /// <summary>
    /// A strand is shaded as a cylinder would be: facing the ray at its middle, and turned toward an edge
    /// by as much as a cylinder turns there -- thirty degrees, halfway out.
    /// </summary>
    [TestMethod]
    public void TestAStrandIsShadedAsThoughItWereRound()
    {
        Strand strand = StraightStrand(0.2, 0.2);
        HairIntersection middle = OnlyHit(strand, 0, 0);
        HairIntersection halfway = OnlyHit(strand, 0, 0.05);

        Assert.IsTrue(strand.SurfaceNormalAt(null, middle).Matches(new Vector(0, 0, -1)));
        Assert.IsTrue(strand.SurfaceNormalAt(null, halfway).Matches(new Vector(0, 0.5, -Math.Sqrt(0.75))));
        // The strands keep their numbers in single precision, so these hold to six places.
        Assert.AreEqual(0, middle.Across, 1e-6);
        Assert.AreEqual(0.5, Math.Abs(halfway.Across), 1e-6);
        Assert.AreEqual(0.5, middle.Along, 1e-6);
    }

    /// <summary>
    /// A crossing behind the ray's start is reported, as every surface owes an ordinary query, and a
    /// shadow's window leaves out what lies beyond the light.
    /// </summary>
    [TestMethod]
    public void TestCrossingsBehindAndBeyondAreKeptOrLeftAsAsked()
    {
        Strand strand = StraightStrand(0.2, 0.2);
        List<Intersection> behind = [];
        StrandSet strands = Set((new Point(-1, 0, 0), new Point(1, 0, 0), 0.2));

        strand.AddIntersections(new Ray(new Point(0, 0, 3), new Vector(0, 0, 1)), behind);
        Assert.AreEqual(1, behind.Count);
        Assert.AreEqual(-3, behind[0].Distance, 1e-9);

        List<Intersection> shadow = [];

        strands.Intersect(new Ray(new Point(0, 0, -5), new Vector(0, 0, 1)), strand, shadow, 0, 4);
        Assert.AreEqual(0, shadow.Count, "a crossing past the light was kept");
    }

    /// <summary>
    /// A curved strand is hit where the curve lies, not where its chord does -- and a ray exactly through
    /// the point where two of its segments meet crosses it once, not once for each.
    /// </summary>
    [TestMethod]
    public void TestACurvedStrandIsHitOnTheCurve()
    {
        // A half circle of radius one in the X/Y plane, through five points; its top is at y = 1.
        Strand strand = new ();

        for (int index = 0; index <= 4; index++)
        {
            double angle = Math.PI * index / 4;

            strand.Points.Add(new RibbonControlPoint
            {
                Position = new Point(-Math.Cos(angle), Math.Sin(angle), 0), Width = 0.02
            });
        }

        strand.PrepareForRendering();

        Assert.AreEqual(1, Hits(strand, 0, 0.995).Count, "the top of the curve was missed");
        Assert.AreEqual(0, Hits(strand, 0, 0.95).Count, "the chord was hit rather than the curve");
        Assert.AreEqual(1, Hits(strand, Math.Sqrt(0.5), Math.Sqrt(0.5)).Count, "the curve was missed at 45 degrees");
    }

    /// <summary>
    /// A set of many strands finds exactly what each of its strands finds alone: its tree passes over
    /// nothing it should look at.
    /// </summary>
    [TestMethod]
    public void TestASetFindsWhatItsStrandsFindAlone()
    {
        Random random = new (7);
        List<List<Point>> paths = [];
        StrandSet all = new ();

        for (int index = 0; index < 300; index++)
        {
            Point root = new (random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2);
            List<Point> path = [root];

            for (int step = 0; step < 3; step++)
            {
                Point last = path[^1];

                path.Add(new Point(
                    last.X + random.NextDouble() * 0.6 - 0.3, last.Y + random.NextDouble() * 0.6,
                    last.Z + random.NextDouble() * 0.6 - 0.3));
            }

            paths.Add(path);
            all.Add(path, [0.08, 0.06, 0.04, 0.01]);
        }

        all.Seal();

        StrandSurface owner = new Strand();

        for (int ray = 0; ray < 400; ray++)
        {
            Ray probe = new (
                new Point(random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2, -6),
                new Vector(random.NextDouble() * 0.4 - 0.2, random.NextDouble() * 0.4 - 0.2, 1));
            List<double> together = Distances(all, probe, owner);
            List<double> alone = [];

            foreach (List<Point> path in paths)
            {
                StrandSet one = new ();

                one.Add(path, [0.08, 0.06, 0.04, 0.01]);
                one.Seal();
                alone.AddRange(Distances(one, probe, owner));
            }

            alone.Sort();
            CollectionAssert.AreEqual(alone, together, $"ray {ray}");
        }
    }

    private static List<double> Distances(StrandSet strands, Ray ray, StrandSurface owner)
    {
        List<Intersection> hits = [];

        strands.Intersect(ray, owner, hits);

        return hits.Select(hit => hit.Distance).Order().ToList();
    }

    /// <summary>
    /// This method returns a strand running straight along X from -1 to 1, with the given widths at
    /// either end.
    /// </summary>
    private static Strand StraightStrand(double start, double end)
    {
        Strand strand = new ();

        strand.Points.Add(new RibbonControlPoint { Position = new Point(-1, 0, 0), Width = start });
        strand.Points.Add(new RibbonControlPoint { Position = new Point(1, 0, 0), Width = end });
        strand.PrepareForRendering();

        return strand;
    }

    private static StrandSet Set(params (Point From, Point To, double Width)[] strands)
    {
        StrandSet set = new ();

        foreach ((Point from, Point to, double width) in strands)
            set.Add([from, to], [width, width]);

        set.Seal();

        return set;
    }

    /// <summary>
    /// This method returns the crossings of a ray looking along +Z from z = -5 at the given place.
    /// </summary>
    private static List<Intersection> Hits(Surface surface, double x, double y)
    {
        List<Intersection> hits = [];

        surface.AddIntersections(new Ray(new Point(x, y, -5), new Vector(0, 0, 1)), hits);

        return hits;
    }

    private static HairIntersection OnlyHit(Surface surface, double x, double y)
    {
        List<Intersection> hits = Hits(surface, x, y);

        Assert.AreEqual(1, hits.Count, $"crossings at ({x}, {y})");

        return (HairIntersection) hits[0];
    }
}
