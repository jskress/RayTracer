using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// These tests cover an extrusion whose outline is scaled as it rises, which turns the solid into
/// a cone over that outline rather than a prism.
/// </summary>
[TestClass]
public class TestTaperedExtrusion
{
    /// <summary>
    /// This builds a square outline two units across, centered on the origin.
    /// </summary>
    private static GeneralPath Square()
    {
        return new GeneralPath()
            .MoveTo(-1, -1)
            .LineTo(1, -1)
            .LineTo(1, 1)
            .LineTo(-1, 1)
            .ClosePath();
    }

    private static Extrusion MakeExtrusion(double taper)
    {
        Extrusion extrusion = new Extrusion
        {
            Path = Square(),
            MinimumY = 0,
            MaximumY = 2,
            Taper = taper
        };

        extrusion.PrepareForRendering();

        return extrusion;
    }

    /// <summary>
    /// The width at a height is what the whole shape rests on, so it is measured directly.  A
    /// square two units across, tapering to half by the top, must be 1.5 across at the middle --
    /// so a ray fired through that middle crosses at -0.75 and +0.75, and nowhere else.
    /// </summary>
    [TestMethod]
    public void TestWidthAtHeight()
    {
        Extrusion extrusion = MakeExtrusion(0.5);
        Ray ray = new Ray(new Point(-5, 1, 0), Directions.Right);
        List<Intersection> intersections = [];

        extrusion.AddIntersections(ray, intersections);

        double[] distances = intersections
            .Select(intersection => intersection.Distance)
            .Order()
            .ToArray();

        Assert.AreEqual(2, distances.Length, $"Got [{string.Join(", ", distances)}]");
        Assert.IsTrue(distances[0].Near(4.25), $"Near wall at {distances[0]}, wanted 4.25");
        Assert.IsTrue(distances[1].Near(5.75), $"Far wall at {distances[1]}, wanted 5.75");
    }

    /// <summary>
    /// The taper is checked at three heights at once, since a wall that leaned by some other
    /// amount -- or not at all -- would still pass a single measurement taken halfway up.
    /// </summary>
    [TestMethod]
    public void TestWidthTapersWithHeight()
    {
        Extrusion extrusion = MakeExtrusion(0.5);

        foreach ((double y, double halfWidth) in new[] { (0.5, 0.875), (1.0, 0.75), (1.5, 0.625) })
        {
            Ray ray = new Ray(new Point(-5, y, 0), Directions.Right);
            List<Intersection> intersections = [];

            extrusion.AddIntersections(ray, intersections);

            double[] distances = intersections
                .Select(intersection => intersection.Distance)
                .Order()
                .ToArray();

            Assert.AreEqual(2, distances.Length, $"At y={y}, got {distances.Length} crossings.");
            Assert.IsTrue(distances[0].Near(5 - halfWidth),
                $"At y={y}, near wall at {distances[0]}, wanted {5 - halfWidth}.");
            Assert.IsTrue(distances[1].Near(5 + halfWidth),
                $"At y={y}, far wall at {distances[1]}, wanted {5 + halfWidth}.");
        }
    }

    /// <summary>
    /// A leaning wall must lean its normal with it.  The wall on the left runs from x=-1 at the
    /// bottom to x=-0.5 at the top over a rise of 2, so its outward normal is across that, in the
    /// ratio of -1 to 0.25.  Testing this on a wall the taper draws in is the point: an untilted
    /// normal would still be perpendicular to the wall's own horizontal cross-section, so only
    /// the rise tells the two apart.
    /// </summary>
    [TestMethod]
    public void TestNormalLeansWithTheWall()
    {
        Extrusion extrusion = MakeExtrusion(0.5);
        Ray ray = new Ray(new Point(-5, 1, 0), Directions.Right);
        List<Intersection> intersections = [];

        extrusion.AddIntersections(ray, intersections);

        Intersection nearest = intersections.MinBy(intersection => intersection.Distance);

        Assert.IsNotNull(nearest);

        Vector normal = extrusion.NormalAt(ray.At(nearest.Distance), nearest);
        Vector wanted = new Vector(-1, 0.25, 0).Unit;

        Assert.IsTrue(normal.X.Near(wanted.X) && normal.Y.Near(wanted.Y) && normal.Z.Near(wanted.Z),
            $"Normal was {normal}, wanted {wanted}.");
    }

    /// <summary>
    /// A taper of nought brings the outline to a point, and the sides must meet there.  A ray just
    /// under the apex crosses the shape; one just above it misses entirely.
    /// </summary>
    [TestMethod]
    public void TestTaperToAPoint()
    {
        Extrusion extrusion = MakeExtrusion(0);
        List<Intersection> below = [];
        List<Intersection> above = [];

        extrusion.AddIntersections(new Ray(new Point(-5, 1.9, 0), Directions.Right), below);
        extrusion.AddIntersections(new Ray(new Point(-5, 2.1, 0), Directions.Right), above);

        Assert.AreEqual(2, below.Count, "A ray below the apex should cross the shape.");
        Assert.AreEqual(0, above.Count, "A ray above the apex should miss the shape.");

        // Counting the crossings is not enough on its own: a shape that widened upward instead of
        // closing would give the same two below and the same none above, since the miss is really
        // the ray passing over the top.  What only a shape closing to a point gives is a width
        // down to almost nothing just under the apex -- a twentieth of the way from the top, and
        // so a twentieth of the outline's size.
        double[] distances = below
            .Select(intersection => intersection.Distance)
            .Order()
            .ToArray();

        Assert.IsTrue(distances[0].Near(4.95), $"Near wall at {distances[0]}, wanted 4.95.");
        Assert.IsTrue(distances[1].Near(5.05), $"Far wall at {distances[1]}, wanted 5.05.");
    }

    /// <summary>
    /// A tapered extrusion must still report crossings behind the ray's origin, or it cannot be
    /// used in a CSG or seen through a refracting surface.
    /// </summary>
    [TestMethod]
    public void TestCrossingsBehindTheOrigin()
    {
        Extrusion extrusion = MakeExtrusion(0.5);
        List<Intersection> intersections = [];

        // This origin is inside the shape, so one wall is behind it.
        extrusion.AddIntersections(new Ray(new Point(0, 1, 0), Directions.Right), intersections);

        double[] distances = intersections
            .Select(intersection => intersection.Distance)
            .Order()
            .ToArray();

        Assert.AreEqual(2, distances.Length, $"Got [{string.Join(", ", distances)}]");
        Assert.IsTrue(distances[0].Near(-0.75), $"Behind-origin crossing at {distances[0]}.");
        Assert.IsTrue(distances[1].Near(0.75), $"Ahead crossing at {distances[1]}.");
    }

    /// <summary>
    /// The box has to hold the outline at both the size it starts and the size it ends, and a
    /// taper that spreads is the case that catches a box built only from the bottom.
    /// </summary>
    [TestMethod]
    public void TestBoundingBox()
    {
        BoundingBox drawnIn = MakeExtrusion(0.5).BoundingBox;
        BoundingBox spread = MakeExtrusion(2).BoundingBox;

        // A box carries a little padding of its own, so each edge is asked to hold the shape and
        // to stop just past it rather than to land on an exact number.
        Assert.IsTrue(drawnIn.Minimum.X is <= -1 and > -1.01, $"Drawn in: {drawnIn.Minimum}");
        Assert.IsTrue(drawnIn.Maximum.X is >= 1 and < 1.01, $"Drawn in: {drawnIn.Maximum}");
        Assert.IsTrue(spread.Minimum.X is <= -2 and > -2.01, $"Spread: {spread.Minimum}");
        Assert.IsTrue(spread.Maximum.X is >= 2 and < 2.01, $"Spread: {spread.Maximum}");
    }

    /// <summary>
    /// A taper of 1 is not a taper, and must leave the shape exactly as it was -- not nearly, but
    /// to the last bit, since every extrusion already in a scene goes through this same code.
    /// </summary>
    [TestMethod]
    public void TestNoTaperIsUnchanged()
    {
        Extrusion tapered = MakeExtrusion(1);
        Extrusion plain = new Extrusion
        {
            Path = Square(),
            MinimumY = 0,
            MaximumY = 2
        };

        plain.PrepareForRendering();

        Ray ray = new Ray(new Point(-5, 1, 0.25), new Vector(1, 0.1, 0.05).Unit);
        List<Intersection> fromTapered = [];
        List<Intersection> fromPlain = [];

        tapered.AddIntersections(ray, fromTapered);
        plain.AddIntersections(ray, fromPlain);

        Assert.AreEqual(fromPlain.Count, fromTapered.Count);

        for (int index = 0; index < fromPlain.Count; index++)
        {
            Assert.AreEqual(fromPlain[index].Distance, fromTapered[index].Distance,
                $"Crossing {index} moved.");
        }
    }
}
