using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These tests cover the bilinear patch: the warped quadrilateral four corners span.
/// <para>
/// The patch used through most of them is the saddle with corners (-1,0,-1), (1,0,-1), (1,1,1) and
/// (-1,0,1) -- three corners on the ground and the fourth lifted a unit.  Working the definition
/// through by hand, that patch is exactly <c>x = 2u - 1</c>, <c>z = 2v - 1</c> and <c>y = u*v</c>,
/// so a point of it is on the surface when <c>y = (x + 1)(z + 1) / 4</c>.  That equation is what
/// the crossings are measured against, rather than any number read back out of the surface.
/// </para>
/// </summary>
[TestClass]
public class TestBilinearPatch
{
    private static BilinearPatch Saddle()
    {
        BilinearPatch patch = new BilinearPatch
        {
            Corners =
            [
                new Point(-1, 0, -1),
                new Point(1, 0, -1),
                new Point(1, 1, 1),
                new Point(-1, 0, 1)
            ]
        };

        patch.PrepareForRendering();

        return patch;
    }

    /// <summary>
    /// The height the saddle stands at, at a given X and Z, worked out from the definition rather
    /// than from the surface.
    /// </summary>
    private static double HeightAt(double x, double z) => (x + 1) * (z + 1) / 4;

    /// <summary>
    /// Four corners in one plane describe a flat quadrilateral, which is what a parallelogram
    /// already draws.  Both are given the same four points and asked the same questions, so the
    /// patch's answer is checked against a surface that was right before it existed.
    /// </summary>
    [TestMethod]
    public void TestAFlatPatchAgreesWithAParallelogram()
    {
        Point corner = new Point(-1, 0.5, -1);
        Vector side1 = new Vector(2, 0, 0);
        Vector side2 = new Vector(0, 0, 2);
        BilinearPatch patch = new BilinearPatch
        {
            Corners = [corner, corner + side1, corner + side1 + side2, corner + side2]
        };
        Parallelogram parallelogram = new Parallelogram
        {
            Point = corner, Side1 = side1, Side2 = side2
        };

        patch.PrepareForRendering();
        parallelogram.PrepareForRendering();

        foreach ((double x, double z) in new[] { (0.0, 0.0), (-0.6, 0.4), (0.75, -0.75), (2.0, 0.0) })
        {
            Ray ray = new Ray(new Point(x, 4, z), Directions.Down);
            List<Intersection> fromPatch = [];
            List<Intersection> fromParallelogram = [];

            patch.AddIntersections(ray, fromPatch);
            parallelogram.AddIntersections(ray, fromParallelogram);

            Assert.AreEqual(fromParallelogram.Count, fromPatch.Count,
                $"At ({x}, {z}) the patch found {fromPatch.Count} crossings and the " +
                $"parallelogram {fromParallelogram.Count}.");

            if (fromParallelogram.Count > 0)
            {
                Assert.IsTrue(fromPatch[0].Distance.Near(fromParallelogram[0].Distance),
                    $"At ({x}, {z}) the patch crossed at {fromPatch[0].Distance} and the " +
                    $"parallelogram at {fromParallelogram[0].Distance}.");
            }
        }
    }

    /// <summary>
    /// A ray dropped straight down must meet the saddle at the height the definition puts it.
    /// Two places are checked rather than one: the middle of a patch is where a great many wrong
    /// parametrisations still happen to give the right answer.
    /// </summary>
    [TestMethod]
    public void TestATwistedPatchIsCrossedWhereTheDefinitionSays()
    {
        BilinearPatch patch = Saddle();

        foreach ((double x, double z) in new[] { (0.0, 0.0), (0.5, -0.5), (-0.5, 0.8) })
        {
            Ray ray = new Ray(new Point(x, 5, z), Directions.Down);
            List<Intersection> intersections = [];

            patch.AddIntersections(ray, intersections);

            Assert.AreEqual(1, intersections.Count,
                $"At ({x}, {z}) the patch was crossed {intersections.Count} times.");

            double wanted = 5 - HeightAt(x, z);

            Assert.IsTrue(intersections[0].Distance.Near(wanted),
                $"At ({x}, {z}) the crossing was at {intersections[0].Distance}, wanted {wanted}.");
        }
    }

    /// <summary>
    /// Every crossing the patch reports, from whatever direction, has to be a point that is really
    /// on the patch.  This is the test that does not depend on any particular ray being worked out
    /// by hand: a fan of them is fired from all round, and each hit is put back into the equation.
    /// </summary>
    [TestMethod]
    public void TestEveryCrossingLandsOnTheSurface()
    {
        BilinearPatch patch = Saddle();
        int found = 0;

        for (int i = 0; i < 12; i++)
        {
            for (int j = 0; j < 12; j++)
            {
                double angle = i * Math.PI / 6;
                Point origin = new Point(4 * Math.Cos(angle), 0.4 + j * 0.22, 4 * Math.Sin(angle));
                Ray ray = new Ray(origin, (new Point(0, 0.25, 0) - origin).Unit);
                List<Intersection> intersections = [];

                patch.AddIntersections(ray, intersections);

                foreach (Intersection intersection in intersections)
                {
                    Point point = ray.At(intersection.Distance);

                    found++;

                    Assert.IsTrue(Math.Abs(point.Y - HeightAt(point.X, point.Z)) < 1e-9,
                        $"A crossing at {point} stands {point.Y} high where the patch is " +
                        $"{HeightAt(point.X, point.Z)}.");
                    Assert.IsTrue(point.X is >= -1.000001 and <= 1.000001 &&
                                  point.Z is >= -1.000001 and <= 1.000001,
                        $"A crossing at {point} is outside the patch's own square.");
                }
            }
        }

        Assert.IsTrue(found > 100, $"Only {found} crossings were found, which is too few to say much.");
    }

    /// <summary>
    /// A straight line can meet a saddle twice, and both crossings have to be reported -- a patch
    /// solved as though it were flat could never give two.
    /// <para>
    /// The ray is not hunted for but worked out.  Level the saddle off at a height h and what is
    /// left is the curve <c>(x + 1)(z + 1) = 4h</c>, a hyperbola; a straight line drawn in that
    /// same level plane can cut a hyperbola twice.  At h = 0.25 the curve is
    /// <c>(x + 1)(z + 1) = 1</c>, and the line <c>x + z = 0.4</c> meets it at x = 0.8633 and
    /// x = -0.4633 -- both comfortably inside the patch's square rather than on its edge, where a
    /// crossing counted once or twice would prove nothing.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestARayMayCrossTwice()
    {
        BilinearPatch patch = Saddle();
        Ray ray = new Ray(new Point(-3, 0.25, 3.4), new Vector(1, 0, -1).Unit);
        List<Intersection> intersections = [];

        patch.AddIntersections(ray, intersections);

        Assert.AreEqual(2, intersections.Count,
            $"The saddle was crossed {intersections.Count} times, wanted twice.");

        double[] crossings = intersections
            .Select(intersection => intersection.Distance)
            .Order()
            .ToArray();

        Assert.IsTrue(Math.Abs(crossings[1] - crossings[0]) > 1e-6,
            "The two crossings are the same one reported twice.");

        foreach (double distance in crossings)
        {
            Point point = ray.At(distance);

            Assert.IsTrue(Math.Abs(point.Y - HeightAt(point.X, point.Z)) < 1e-9,
                $"A crossing at {point} is not on the patch.");
            Assert.IsTrue(Math.Abs(point.Y - 0.25) < 1e-9,
                $"A crossing at {point} left the level the ray travels in.");
            Assert.IsTrue(point.X is > -1 and < 1 && point.Z is > -1 and < 1,
                $"A crossing at {point} is on the patch's edge rather than inside it.");
        }
    }

    /// <summary>
    /// A crossing behind the ray's origin still has to be reported, or the patch cannot be seen
    /// through a refracting surface or used in a CSG.
    /// </summary>
    [TestMethod]
    public void TestCrossingsBehindTheOriginAreReported()
    {
        BilinearPatch patch = Saddle();
        Ray ray = new Ray(new Point(0, -2, 0), Directions.Down);
        List<Intersection> intersections = [];

        patch.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count, "The patch was not crossed at all.");
        Assert.IsTrue(intersections[0].Distance.Near(-2.25),
            $"The crossing behind the origin was reported at {intersections[0].Distance}, " +
            "wanted -2.25.");
    }

    /// <summary>
    /// The normal has to be square to the surface, and that is measured rather than asserted: two
    /// points a little either side of the crossing, worked out from the patch's own definition,
    /// give a direction that lies in the surface, and the normal must have nothing along it.
    /// </summary>
    [TestMethod]
    public void TestTheNormalIsSquareToTheSurface()
    {
        BilinearPatch patch = Saddle();
        Ray ray = new Ray(new Point(0.4, 5, -0.3), Directions.Down);
        List<Intersection> intersections = [];

        patch.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);

        Point point = ray.At(intersections[0].Distance);
        Vector normal = patch.NormalAt(point, intersections[0]);
        const double step = 1e-4;

        foreach ((double dx, double dz) in new[] { (step, 0.0), (0.0, step), (step, step) })
        {
            Point ahead = new Point(
                point.X + dx, HeightAt(point.X + dx, point.Z + dz), point.Z + dz);
            Point behind = new Point(
                point.X - dx, HeightAt(point.X - dx, point.Z - dz), point.Z - dz);
            Vector along = (ahead - behind).Unit;

            Assert.IsTrue(Math.Abs(normal.Dot(along)) < 1e-6,
                $"The normal {normal} has {normal.Dot(along)} of itself along {along}, which lies " +
                "in the surface.");
        }
    }

    /// <summary>
    /// The same saddle, shrunk until the solver has to think about how big it is.
    /// <para>
    /// Nothing else here would notice a solver that gave up on small patches, because everything
    /// else is a unit or two across.  The quadratic's three coefficients are each a pair of the
    /// patch's own edges multiplied together, so they fall with the *square* of its size: at a
    /// two-thousandth of a unit the leading one is under a millionth for **any** direction at all,
    /// and a solver testing it against a fixed number takes every such patch for one with no warp in
    /// it, solving a straight line where there is a curve.
    /// </para>
    /// <para>
    /// The ray has to lean.  Dropped straight down on this patch the leading coefficient is exactly
    /// nought -- the warp runs along Z, and a vertical ray has nothing along it -- so the straight
    /// line really is the right answer and a vertical ray cannot tell the two apart.  This one comes
    /// in at a slant, and its crossing is worked out from the surface's own equation.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestAPatchSmallEnoughToLoseItsCoefficients()
    {
        const double size = 0.0005;

        BilinearPatch patch = new BilinearPatch
        {
            Corners =
            [
                new Point(-size, 0, -size),
                new Point(size, 0, -size),
                new Point(size, size, size),
                new Point(-size, 0, size)
            ]
        };

        patch.PrepareForRendering();

        Ray ray = new Ray(
            new Point(-0.003, 0.002, -0.003),
            new Vector(0.6337242505, -0.4436069754, 0.6337242505));
        List<Intersection> intersections = [];

        patch.AddIntersections(ray, intersections);

        Assert.IsTrue(intersections.Count > 0, "The small patch was not crossed at all.");

        foreach (Intersection intersection in intersections)
        {
            Point point = ray.At(intersection.Distance);

            // Scaling every corner by "size" scales the surface with them, so the height is that
            // fraction of what the unit saddle stands at.
            double height = size * HeightAt(point.X / size, point.Z / size);

            Assert.IsTrue(Math.Abs(point.Y - height) < 1e-12,
                $"A crossing at {point} stands {point.Y} high where the patch is {height} -- the " +
                "solver put it somewhere the ray does not go.");
        }
    }

    /// <summary>
    /// Given a normal at each corner, the patch shades by blending them -- so at a corner it must
    /// give back that corner's own normal, near enough.  This is what catches the corners and the
    /// normals being paired up in the wrong order, which is otherwise invisible: any order gives a
    /// smooth-looking result, just the wrong one.
    /// </summary>
    [TestMethod]
    public void TestEachCornerKeepsItsOwnNormal()
    {
        Vector[] normals =
        [
            new Vector(-1, 4, -1).Unit,
            new Vector(3, 4, -1).Unit,
            new Vector(3, 4, 2).Unit,
            new Vector(-1, 4, 2).Unit
        ];
        BilinearPatch patch = new BilinearPatch
        {
            Corners =
            [
                new Point(-1, 0, -1),
                new Point(1, 0, -1),
                new Point(1, 0, 1),
                new Point(-1, 0, 1)
            ],
            CornerNormals = normals
        };

        patch.PrepareForRendering();

        (double X, double Z)[] corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)];

        for (int index = 0; index < 4; index++)
        {
            // Just inside the corner rather than on it, so the crossing is unambiguously the
            // patch's own.
            Ray ray = new Ray(
                new Point(corners[index].X * 0.998, 4, corners[index].Z * 0.998), Directions.Down);
            List<Intersection> intersections = [];

            patch.AddIntersections(ray, intersections);

            Assert.AreEqual(1, intersections.Count, $"Corner {index} was not crossed.");

            Vector normal = patch.NormalAt(ray.At(intersections[0].Distance), intersections[0]);
            double along = Math.Abs(normal.Dot(normals[index]));

            Assert.IsTrue(along > 0.999,
                $"At corner {index} the normal was {normal}, which lies {along} along the " +
                $"{normals[index]} that corner was given.");
        }
    }

    /// <summary>
    /// What corner normals are actually for: two patches meeting along an edge, each handed the
    /// same normals at the two corners they share, must agree about the normal all the way along
    /// it -- so a sheet built of several of them shades as one surface rather than showing a crease
    /// at every seam.
    /// <para>
    /// The two are warped differently on purpose.  Left to work their normals out from their own
    /// shapes they disagree sharply at the seam, and the test checks that too: a pair that agreed
    /// anyway would prove nothing about the blending.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestNormalsMakeNeighbouringPatchesAgree()
    {
        Point sharedNear = new Point(1, 0, -1);
        Point sharedFar = new Point(1, 0.5, 1);
        Vector normalNear = new Vector(0.1, 1, -0.2).Unit;
        Vector normalFar = new Vector(-0.3, 1, 0.15).Unit;

        BilinearPatch Build(Point[] corners, Vector[] normals)
        {
            BilinearPatch patch = new BilinearPatch { Corners = corners, CornerNormals = normals };

            patch.PrepareForRendering();

            return patch;
        }

        Point[] leftCorners =
            [new Point(-1, 0, -1), sharedNear, sharedFar, new Point(-1, 0, 1)];
        Point[] rightCorners =
            [sharedNear, new Point(3, 0.6, -1), new Point(3, 0, 1), sharedFar];
        Vector[] leftNormals =
            [new Vector(0.4, 1, -0.1).Unit, normalNear, normalFar, new Vector(-0.2, 1, 0.4).Unit];
        Vector[] rightNormals =
            [normalNear, new Vector(-0.5, 1, 0.3).Unit, new Vector(0.25, 1, -0.4).Unit, normalFar];

        const double z = 0.2;
        const double nudge = 0.002;

        Vector Sample(BilinearPatch patch, double x)
        {
            Ray ray = new Ray(new Point(x, 5, z), Directions.Down);
            List<Intersection> intersections = [];

            patch.AddIntersections(ray, intersections);

            Assert.AreEqual(1, intersections.Count, $"Nothing was crossed at x={x}.");

            return patch.NormalAt(ray.At(intersections[0].Distance), intersections[0]);
        }

        Vector smoothLeft = Sample(Build(leftCorners, leftNormals), 1 - nudge);
        Vector smoothRight = Sample(Build(rightCorners, rightNormals), 1 + nudge);
        Vector plainLeft = Sample(Build(leftCorners, null), 1 - nudge);
        Vector plainRight = Sample(Build(rightCorners, null), 1 + nudge);

        double smoothAgreement = Math.Abs(smoothLeft.Dot(smoothRight));
        double plainAgreement = Math.Abs(plainLeft.Dot(plainRight));

        Assert.IsTrue(plainAgreement < 0.99,
            $"The two patches agree to {plainAgreement} without normals, so this pair cannot show " +
            "that giving them any made a difference.");
        Assert.IsTrue(smoothAgreement > 0.9999,
            $"Across the seam the normals lie {smoothAgreement} along one another, which is not " +
            $"the one surface: {smoothLeft} against {smoothRight}.");
    }

    /// <summary>
    /// Corner normals change the shading and nothing else.  The patch keeps the shape its corners
    /// give it, so every crossing stays exactly where it was.
    /// </summary>
    [TestMethod]
    public void TestNormalsDoNotMoveTheSurface()
    {
        BilinearPatch plain = Saddle();
        BilinearPatch shaded = new BilinearPatch
        {
            Corners = plain.Corners,
            CornerNormals =
            [
                new Vector(0.3, 1, -0.2).Unit,
                new Vector(-0.4, 1, 0.1).Unit,
                new Vector(0.2, 1, 0.5).Unit,
                new Vector(-0.1, 1, -0.3).Unit
            ]
        };

        shaded.PrepareForRendering();

        Ray ray = new Ray(new Point(0.35, 5, -0.2), Directions.Down);
        List<Intersection> fromPlain = [];
        List<Intersection> fromShaded = [];

        plain.AddIntersections(ray, fromPlain);
        shaded.AddIntersections(ray, fromShaded);

        Assert.AreEqual(fromPlain.Count, fromShaded.Count);
        Assert.AreEqual(fromPlain[0].Distance, fromShaded[0].Distance,
            "Giving the patch normals moved where the ray crosses it.");
    }

    /// <summary>
    /// Every point of the patch is a mix of its corners with weights that are never negative, so
    /// the box around those corners holds all of it.
    /// </summary>
    [TestMethod]
    public void TestTheBoundingBoxHoldsTheCorners()
    {
        BoundingBox box = Saddle().BoundingBox;

        Assert.IsTrue(box.Minimum.X <= -1 && box.Maximum.X >= 1, $"X: {box.Minimum} to {box.Maximum}");
        Assert.IsTrue(box.Minimum.Y <= 0 && box.Maximum.Y >= 1, $"Y: {box.Minimum} to {box.Maximum}");
        Assert.IsTrue(box.Minimum.Z <= -1 && box.Maximum.Z >= 1, $"Z: {box.Minimum} to {box.Maximum}");
    }
}
