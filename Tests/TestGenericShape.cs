using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestGenericShape
{
    /// <summary>
    /// A generic shape lies in its own local X/Y plane at Z = 0, with a fixed +Z normal --
    /// world placement is expected to come entirely from the surface's own transform.
    /// </summary>
    [TestMethod]
    public void TestLiesInTheLocalXyPlaneWithAPlusZNormal()
    {
        GenericShape shape = new ()
        {
            Path = new GeneralPath()
                .MoveTo(1, 1).LineTo(-1, 1).LineTo(-1, -1).LineTo(1, -1).ClosePath()
        };

        shape.PrepareForRendering();

        Assert.IsTrue(new Vector(0, 0, 1).Matches(shape.Normal));
    }

    [TestMethod]
    public void TestRayHitsInsideAStraightEdgedSquare()
    {
        GenericShape shape = new ()
        {
            Path = new GeneralPath()
                .MoveTo(1, 1).LineTo(-1, 1).LineTo(-1, -1).LineTo(1, -1).ClosePath()
        };
        shape.PrepareForRendering();

        Ray ray = new (new Point(0.5, 0.5, -5), Directions.In);
        List<Intersection> intersections = [];

        shape.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(5.0.Near(intersections[0].Distance));
    }

    [TestMethod]
    public void TestRayMissesOutsideAStraightEdgedSquare()
    {
        GenericShape shape = new ()
        {
            Path = new GeneralPath()
                .MoveTo(1, 1).LineTo(-1, 1).LineTo(-1, -1).LineTo(1, -1).ClosePath()
        };
        shape.PrepareForRendering();

        Ray ray = new (new Point(2, 2, -5), Directions.In);
        List<Intersection> intersections = [];

        shape.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    /// <summary>
    /// A shape with a curved edge (built with a quad segment whose control point bulges the
    /// boundary out past where a straight edge would sit) must accept a ray that only lands
    /// inside because of that bulge, and reject one just past it.
    /// </summary>
    [TestMethod]
    public void TestRayHitsInsideTheBulgeOfACurvedEdge()
    {
        // A shape whose right edge bulges from (1,-1) out to (1.4,0) and back to (1,1),
        // closed by straight edges on the other three sides.
        GenericShape shape = new ()
        {
            Path = new GeneralPath()
                .MoveTo(-1, -1)
                .LineTo(1, -1)
                .QuadTo(1.4, 0, 1, 1)
                .LineTo(-1, 1)
                .ClosePath()
        };
        shape.PrepareForRendering();

        // The curve's midpoint (t=0.5) works out to exactly (1.2,0), so 1.1 (clearly inside
        // the bulge, since a straight edge here would sit at x=1) and 1.5 (clearly past it)
        // are used instead, to avoid a point that lands exactly on the boundary.
        Ray insideTheBulge = new (new Point(1.1, 0, -5), Directions.In);
        Ray pastTheBulge = new (new Point(1.5, 0, -5), Directions.In);
        List<Intersection> insideIntersections = [];
        List<Intersection> outsideIntersections = [];

        shape.AddIntersections(insideTheBulge, insideIntersections);
        shape.AddIntersections(pastTheBulge, outsideIntersections);

        Assert.AreEqual(1, insideIntersections.Count);
        Assert.AreEqual(0, outsideIntersections.Count);
    }

    /// <summary>
    /// A concave (L-shaped) profile must reject a ray landing in its notch, even though
    /// that point sits well within the shape's overall bounding box.
    /// </summary>
    [TestMethod]
    public void TestRayMissesInsideTheNotchOfAConcaveShape()
    {
        GenericShape shape = new ()
        {
            Path = new GeneralPath()
                .MoveTo(0, 0).LineTo(2, 0).LineTo(2, 1).LineTo(1, 1).LineTo(1, 2).LineTo(0, 2)
                .ClosePath()
        };
        shape.PrepareForRendering();

        Ray inTheNotch = new (new Point(1.5, 1.5, -5), Directions.In);
        Ray inTheBody = new (new Point(0.5, 0.5, -5), Directions.In);
        List<Intersection> notchIntersections = [];
        List<Intersection> bodyIntersections = [];

        shape.AddIntersections(inTheNotch, notchIntersections);
        shape.AddIntersections(inTheBody, bodyIntersections);

        Assert.AreEqual(0, notchIntersections.Count);
        Assert.AreEqual(1, bodyIntersections.Count);
    }

    [TestMethod]
    public void TestRayParallelToTheShapeMisses()
    {
        GenericShape shape = new ()
        {
            Path = new GeneralPath()
                .MoveTo(1, 1).LineTo(-1, 1).LineTo(-1, -1).LineTo(1, -1).ClosePath()
        };
        shape.PrepareForRendering();

        Ray ray = new (new Point(0, 0, -5), Directions.Up);
        List<Intersection> intersections = [];

        shape.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayPointingAwayMisses()
    {
        GenericShape shape = new ()
        {
            Path = new GeneralPath()
                .MoveTo(1, 1).LineTo(-1, 1).LineTo(-1, -1).LineTo(1, -1).ClosePath()
        };
        shape.PrepareForRendering();

        Ray ray = new (new Point(0.5, 0.5, 5), Directions.In);
        List<Intersection> intersections = [];

        shape.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }
}
