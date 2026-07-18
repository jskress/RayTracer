using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Geometry.LSystems;

namespace Tests;

[TestClass]
public class TestLSystemPolygons
{
    // Build an L-system from an axiom alone (generation 0, so the axiom is the production
    // verbatim) and prepare it, so we can inspect the geometry its polygons filled.  A right
    // angle keeps the traced outlines easy to reason about.
    private static LSystem Prepare(
        string axiom, LSystemRendererType renderer = LSystemRendererType.Pipes)
    {
        LSystem lsystem = new LSystem
        {
            Axiom = axiom,
            Generations = 0,
            RenderingControls = new LSystemRenderingControls
            {
                RendererType = renderer,
                Angle = 90.0.ToRadians(),
                Length = 1,
                Diameter = 0.1
            }
        };

        lsystem.PrepareForRendering();

        return lsystem;
    }

    private static List<Triangle> TrianglesOf(LSystem lsystem)
    {
        return new SurfaceIterator(lsystem.Surfaces).Surfaces.OfType<Triangle>().ToList();
    }

    [TestMethod]
    public void TestATracedOutlineIsFilled()
    {
        // A unit square traced with silent moves: four corners, so two triangles.
        LSystem lsystem = Prepare("{f+f+f+f}");

        Assert.AreEqual(2, TrianglesOf(lsystem).Count);
    }

    [TestMethod]
    public void TestBothMoveCommandsContributeCorners()
    {
        // 'F' draws where 'f' does not, but both leave a corner behind.
        Assert.AreEqual(2, TrianglesOf(Prepare("{f+f+f+f}")).Count);
        Assert.AreEqual(2, TrianglesOf(Prepare("{F+F+F+F}")).Count);
    }

    [TestMethod]
    public void TestTheNonRecordingMoveContributesNoCorner()
    {
        // 'G' moves and draws exactly as 'F' does, but leaves no corner -- so a square traced with
        // one of its sides walked by 'G' is a triangle's worth of corners, not a square's.
        LSystem lsystem = Prepare("{f+f+f+G}");

        Assert.AreEqual(1, TrianglesOf(lsystem).Count);
    }

    [TestMethod]
    public void TestARecordedVertexAddsACornerWithoutMoving()
    {
        // '{' itself records nothing, so an outline wanting the point it was anchored at opens
        // with '.' -- the documented "{." idiom.  Here that anchor plus three moves trace a unit
        // square, where the three moves alone would only manage a triangle.
        Assert.AreEqual(2, TrianglesOf(Prepare("{.f+f+f}")).Count);
        Assert.AreEqual(1, TrianglesOf(Prepare("{f+f+f}")).Count);
    }

    [TestMethod]
    public void TestCornerCountsCarryThroughToTheFill()
    {
        // A hexagon at sixty degrees: six corners, so four triangles.  (Concave outlines are the
        // triangulator's own responsibility and are covered by TestPolygonTriangulator; what
        // matters here is that every corner the turtle traces reaches it.)
        LSystem lsystem = new LSystem
        {
            Axiom = "{f+f+f+f+f+f}",
            Generations = 0,
            RenderingControls = new LSystemRenderingControls { Angle = 60.0.ToRadians() }
        };

        lsystem.PrepareForRendering();

        List<Triangle> triangles = TrianglesOf(lsystem);

        Assert.AreEqual(4, triangles.Count);

        foreach (Triangle triangle in triangles)
        {
            double area = (triangle.Point2 - triangle.Point1)
                .Cross(triangle.Point3 - triangle.Point1).Magnitude / 2;

            Assert.IsTrue(area > 0, "A degenerate triangle came out of the fill.");
        }
    }

    [TestMethod]
    public void TestAnOutlineWithTooFewCornersFillsNothing()
    {
        Assert.AreEqual(0, TrianglesOf(Prepare("{}")).Count);
        Assert.AreEqual(0, TrianglesOf(Prepare("{f}")).Count);
        Assert.AreEqual(0, TrianglesOf(Prepare("{f+f}")).Count);
    }

    [TestMethod]
    public void TestAFilledOutlineIsNotCulledByTheBoundingBox()
    {
        // An outline traced with 'f' draws no segments at all, so nothing but the corners
        // themselves tells the group how far it reaches.  A ray aimed into the middle of the blade
        // has to find it; if the corners were left out of the bounding box, the group would cull
        // the ray before any triangle was ever tested.
        LSystem lsystem = Prepare("{f+f+f+f}");
        List<Intersection> intersections = [];

        lsystem.Intersect(new Ray(new Point(0.5, 1, 0.5), new Vector(0, -1, 0)), intersections);

        Assert.IsTrue(intersections.Count > 0);
    }

    [TestMethod]
    public void TestPolygonsFillUnderEitherRenderer()
    {
        // Polygons are the base renderer's business, so the stem renderer must not matter.
        Assert.AreEqual(2, TrianglesOf(Prepare("{f+f+f+f}", LSystemRendererType.Pipes)).Count);
        Assert.AreEqual(2, TrianglesOf(Prepare("{f+f+f+f}", LSystemRendererType.Tubes)).Count);
    }
}
