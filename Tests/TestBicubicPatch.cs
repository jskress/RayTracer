using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestBicubicPatch
{
    /// <summary>
    /// This helper builds a perfectly flat 4x4 grid of control points spanning
    /// (0,0,0)..(3,3,0), already prepared for rendering.
    /// </summary>
    private static BicubicPatch CreateFlatPatch()
    {
        Point[,] points = new Point[4, 4];

        for (int i = 0; i < 4; i++)
        for (int j = 0; j < 4; j++)
            points[i, j] = new Point(i, j, 0);

        BicubicPatch patch = new () { ControlPoints = points };

        patch.PrepareForRendering();

        return patch;
    }

    /// <summary>
    /// This helper builds a symmetric "dome" patch: the four corners sit at Z=0, and the
    /// interior 2x2 block of control points is raised to Z=1, already prepared for rendering.
    /// </summary>
    private static BicubicPatch CreateDomePatch(int uSteps = 3, int vSteps = 3)
    {
        Point[,] points = new Point[4, 4];

        for (int i = 0; i < 4; i++)
        for (int j = 0; j < 4; j++)
            points[i, j] = new Point(i, j, (i is 1 or 2) && (j is 1 or 2) ? 1.0 : 0.0);

        BicubicPatch patch = new () { ControlPoints = points, USteps = uSteps, VSteps = vSteps };

        patch.PrepareForRendering();

        return patch;
    }

    [TestMethod]
    public void TestFlatPatchMatchesParallelogram()
    {
        BicubicPatch patch = CreateFlatPatch();
        Parallelogram parallelogram = new ()
        {
            Point = new Point(0, 0, 0), Side1 = new Vector(3, 0, 0), Side2 = new Vector(0, 3, 0)
        };
        Ray ray = new (new Point(1.2, 0.7, -5), Directions.In);
        List<Intersection> patchHits = [];
        List<Intersection> parallelogramHits = [];

        patch.AddIntersections(ray, patchHits);
        parallelogram.AddIntersections(ray, parallelogramHits);

        Assert.AreEqual(1, patchHits.Count);
        Assert.AreEqual(1, parallelogramHits.Count);
        Assert.IsTrue(patchHits[0].Distance.Near(parallelogramHits[0].Distance, 0.0001));

        // The normal's direction (like Triangle's) follows from the winding of the control
        // points rather than always facing back toward the camera -- with U increasing along X
        // and V increasing along Y here, U cross V points along +Z.
        Vector patchNormal = patch.SurfaceNormaAt(ray.At(patchHits[0].Distance), patchHits[0]);

        Assert.IsTrue(Directions.In.Matches(patchNormal));
    }

    [TestMethod]
    public void TestRayMissesPatch()
    {
        BicubicPatch patch = CreateFlatPatch();
        Ray ray = new (new Point(10, 10, -5), Directions.In);
        List<Intersection> intersections = [];

        patch.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestHitDistanceConvergesAsStepsIncrease()
    {
        // An off-center ray (avoiding the dome's own symmetric center, where several leaves'
        // corners can coincide) should land at very nearly the same distance and normal
        // regardless of how finely the patch is subdivided, since the tessellation quality
        // doesn't change the true underlying surface -- only its approximation of it.  A
        // moderately coarse setting is within a reasonable distance of a fine one; two settings
        // fine enough to both already be flat enough (per Flatness) should match almost exactly.
        Ray ray = new (new Point(1.37, 1.62, -5), Directions.In);
        BicubicPatch moderate = CreateDomePatch(2, 2);
        BicubicPatch fine = CreateDomePatch(6, 6);
        BicubicPatch veryFine = CreateDomePatch(8, 8);
        List<Intersection> moderateHits = [];
        List<Intersection> fineHits = [];
        List<Intersection> veryFineHits = [];

        moderate.AddIntersections(ray, moderateHits);
        fine.AddIntersections(ray, fineHits);
        veryFine.AddIntersections(ray, veryFineHits);

        Assert.AreEqual(1, moderateHits.Count);
        Assert.AreEqual(1, fineHits.Count);
        Assert.AreEqual(1, veryFineHits.Count);
        Assert.IsTrue(moderateHits[0].Distance.Near(fineHits[0].Distance, 0.05));
        Assert.IsTrue(fineHits[0].Distance.Near(veryFineHits[0].Distance, 0.0001));

        Vector fineNormal = fine.SurfaceNormaAt(ray.At(fineHits[0].Distance), fineHits[0]);
        Vector veryFineNormal = veryFine.SurfaceNormaAt(ray.At(veryFineHits[0].Distance), veryFineHits[0]);

        Assert.IsTrue((fineNormal - veryFineNormal).Magnitude < 0.0001);
    }

    [TestMethod]
    public void TestNormalOnDomePointsGenerallyOutward()
    {
        BicubicPatch dome = CreateDomePatch();
        Ray ray = new (new Point(1.4, 1.55, -5), Directions.In);
        List<Intersection> intersections = [];

        dome.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);

        // With this grid's winding (U along X, V along Y), U cross V points toward +Z, the same
        // direction the dome itself bulges -- see TestFlatPatchMatchesParallelogram's comment.
        Vector normal = dome.SurfaceNormaAt(ray.At(intersections[0].Distance), intersections[0]);

        Assert.IsTrue(normal.Z > 0, "The dome's normal should point the same way it bulges.");
        Assert.IsTrue(1.0.Near(normal.Magnitude));
    }

    [TestMethod]
    public void TestDegenerateControlPointsDoNotCrash()
    {
        Point[,] points = new Point[4, 4];

        for (int i = 0; i < 4; i++)
        for (int j = 0; j < 4; j++)
            points[i, j] = Point.Zero;

        BicubicPatch patch = new () { ControlPoints = points };

        patch.PrepareForRendering();

        Ray ray = new (new Point(0, 0, -5), Directions.In);
        List<Intersection> intersections = [];

        patch.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestMissingControlPointsThrow()
    {
        BicubicPatch patch = new ();

        Assert.ThrowsExactly<Exception>(patch.PrepareForRendering);
    }

    [TestMethod]
    public void TestNonPositiveFlatnessThrows()
    {
        BicubicPatch patch = CreateFlatPatch();

        patch.Flatness = 0;

        Assert.ThrowsExactly<Exception>(patch.PrepareForRendering);
    }
}
