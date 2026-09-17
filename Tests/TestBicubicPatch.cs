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

    /// <summary>
    /// Every ray aimed squarely at the middle of a patch must hit it.
    /// <para>
    /// The subdivision tree stops a branch as soon as its subpatch is flat enough, so a gently
    /// curved patch ends up with neighbouring leaves at DIFFERENT depths: one side of a shared edge
    /// is a single straight chord and the other is two, and between them lies a sliver the surface
    /// does not cover.  Rays through the sliver miss a surface they are pointed straight at, which
    /// renders as single pixels of background scattered through the middle of the shape.
    /// </para>
    /// <para>
    /// These control points are a sail from the `vessels` library, which is where this was found and
    /// is the shape that shows it: tall, narrow, barely curved, and with its top row drawn almost to
    /// a point, so the flatness test succeeds at one depth in some places and a deeper one in others.
    /// A regular dome does NOT show it -- it is either flat enough everywhere at once or curved
    /// enough to run to the step cap everywhere at once, and both of those are uniform.
    /// </para>
    /// <para>
    /// The rays are fired at the middle of the cloth, well inside its edges, where there is no
    /// question of a legitimate miss.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestEveryRayAimedAtTheMiddleOfASailShapedPatchHitsIt()
    {
        Point[,] points = new Point[4, 4];

        points[0, 0] = new Point(0.6, 13.0, 0);
        points[0, 1] = new Point(0.516, 12.781, 0);
        points[0, 2] = new Point(0.432, 12.562, 0);
        points[0, 3] = new Point(0.348, 12.343, 0);
        points[1, 0] = new Point(0.6, 9.35, 0);
        points[1, 1] = new Point(0.0773, 9.204, 0.55);
        points[1, 2] = new Point(-0.4453, 9.058, 0.55);
        points[1, 3] = new Point(-0.968, 8.912, 0);
        points[2, 0] = new Point(0.6, 5.7, 0);
        points[2, 1] = new Point(-0.3613, 5.627, 0.55);
        points[2, 2] = new Point(-1.3227, 5.554, 0.55);
        points[2, 3] = new Point(-2.284, 5.481, 0);
        points[3, 0] = new Point(0.6, 2.05, 0);
        points[3, 1] = new Point(-0.8, 2.05, 0);
        points[3, 2] = new Point(-2.2, 2.05, 0);
        points[3, 3] = new Point(-3.6, 2.05, 0);

        BicubicPatch patch = new () { ControlPoints = points, USteps = 5, VSteps = 5 };

        patch.PrepareForRendering();

        // A triangle well inside the sail: head, tack and clew each pulled in toward the middle.
        (double X, double Y) head = (0.52, 12.2);
        (double X, double Y) tack = (0.42, 2.45);
        (double X, double Y) clew = (-3.1, 2.4);

        // **The rays come from where a camera was, not straight in.**  Fired perpendicular to the
        // cloth every one of them hits, because the sliver between two nearly coplanar leaves is
        // hidden when they overlap in projection.  It opens up when the ray arrives at an angle,
        // which is the only way a ray ever does arrive in a render.
        Point eye = new (13, 8, -18);

        int fired = 0;
        List<(double X, double Y)> missed = [];

        for (int a = 1; a < 240; a++)
        for (int b = 1; a + b < 240; b++)
        {
            double fa = a / 240.0;
            double fb = b / 240.0;
            double x = head.X + fa * (tack.X - head.X) + fb * (clew.X - head.X);
            double y = head.Y + fa * (tack.Y - head.Y) + fb * (clew.Y - head.Y);
            List<Intersection> intersections = [];

            fired++;

            patch.AddIntersections(
                new Ray(eye, (new Point(x, y, 0) - eye).Unit), intersections);

            if (intersections.Count == 0)
                missed.Add((x, y));
        }

        Assert.AreEqual(0, missed.Count,
            $"{missed.Count} of {fired} rays fired at the middle of the sail found nothing; " +
            $"the first was at ({missed.FirstOrDefault().X:F4}, {missed.FirstOrDefault().Y:F4})");
    }

    /// <summary>
    /// A patch must report a crossing that lies behind the ray's origin, not drop it, so a CSG
    /// built from a closed shell of patches can tell inside from outside for a ray that starts
    /// within it -- as a shadow, reflection or refraction ray, cast from within the shell, does.
    /// The flat patch here lies a unit behind the origin, in the Z = 0 plane, and the ray is fired
    /// away from it; the hit at distance -1 must still be reported.  Neither the bounding-sphere
    /// walk (which must not prune the node behind the origin) nor the leaf test (which must not
    /// drop the negative distance) may throw it away.
    /// </summary>
    [TestMethod]
    public void TestHitBehindTheOriginIsReported()
    {
        BicubicPatch patch = CreateFlatPatch();
        Ray ray = new (new Point(1.5, 1.5, 1), new Vector(0, 0, 1));
        List<Intersection> intersections = [];

        patch.AddIntersections(ray, intersections);

        Assert.IsTrue(
            intersections.Any(intersection => intersection.Distance.Near(-1, 0.001)),
            "the crossing behind the origin must be reported");
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
        Vector patchNormal = patch.SurfaceNormalAt(ray.At(patchHits[0].Distance), patchHits[0]);

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

        Vector fineNormal = fine.SurfaceNormalAt(ray.At(fineHits[0].Distance), fineHits[0]);
        Vector veryFineNormal = veryFine.SurfaceNormalAt(ray.At(veryFineHits[0].Distance), veryFineHits[0]);

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
        Vector normal = dome.SurfaceNormalAt(ray.At(intersections[0].Distance), intersections[0]);

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
