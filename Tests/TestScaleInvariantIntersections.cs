using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// A surface is handed the ray in its own space, and carrying a world ray there divides its
/// direction by the surface's scale.  Every quantity an intersection test derives from that
/// direction therefore shrinks as the surface grows, so judging any of them against a fixed
/// number loses geometry as a scene is scaled up -- silently, and looking for all the world
/// like a lighting fault rather than like missing geometry.
///
/// These tests all have the same shape, and it is the shape to reach for whenever a tolerance
/// is in question: render -- or here, intersect -- the same geometry at a range of scales, with
/// everything scaled to match, and require the same answer from each.  Scale 1 is the oracle,
/// so no case needs an expected count worked out by hand.
/// </summary>
[TestClass]
public class TestScaleInvariantIntersections
{
    private static readonly double[] Scales = [1, 10, 100, 1000, 10000];

    /// <summary>
    /// This method builds the same geometry at each scale and requires every scale to report the
    /// crossings the unscaled one does, at the same places.
    /// </summary>
    /// <param name="what">What is being tested, for the failure message.</param>
    /// <param name="createSurface">Builds the surface at a given scale.</param>
    /// <param name="createRay">Builds the matching ray at a given scale.</param>
    private static void AssertTheSameAtEveryScale(
        string what, Func<double, Surface> createSurface, Func<double, Ray> createRay)
    {
        double[] expected = CrossingsAt(1, createSurface, createRay);

        foreach (double scale in Scales)
        {
            double[] found = CrossingsAt(scale, createSurface, createRay);

            Assert.AreEqual(expected.Length, found.Length,
                $"{what}: unscaled found {expected.Length} crossing(s) but scale {scale} found " +
                $"{found.Length}; the two are the same geometry, so they must agree.");

            for (int index = 0; index < expected.Length; index++)
            {
                Assert.AreEqual(expected[index], found[index] / scale,
                    Math.Abs(expected[index]) * 1e-6 + 1e-9,
                    $"{what}: crossing {index} moved between scale 1 and scale {scale}.");
            }
        }
    }

    private static double[] CrossingsAt(
        double scale, Func<double, Surface> createSurface, Func<double, Ray> createRay)
    {
        List<Intersection> intersections = [];

        createSurface(scale).Intersect(createRay(scale), intersections);

        return intersections.Select(intersection => intersection.Distance).Order().ToArray();
    }

    /// <summary>
    /// A long pipe, and a ray running down it at a shallow angle to its axis which does reach the
    /// wall.  The wall test is the square of the ray's direction times the square of the sine of
    /// that angle, so judged absolutely the wall went missing as the pipe grew: at a scale of 100
    /// every ray within 5.7 degrees of the axis lost it, leaving a pipe showing only its caps.
    /// </summary>
    [TestMethod]
    public void TestCylinderWallNearItsAxis()
    {
        foreach (double degrees in new[] { 5.0, 1.0, 0.5 })
        {
            double radians = degrees.ToRadians();

            AssertTheSameAtEveryScale(
                $"a cylinder wall met at {degrees} degrees to the axis",
                scale => new Cylinder
                {
                    MinimumY = -200, MaximumY = 200, Closed = false,
                    Transform = Transforms.Scale(scale)
                },
                scale => new Ray(
                    new Point(0, 190 * scale, 0),
                    new Vector(Math.Sin(radians), -Math.Cos(radians), 0)));
        }
    }

    /// <summary>
    /// The same test for a cone, where the quantity goes to nothing when the ray runs along one of
    /// the cone's own slants rather than along its axis.
    /// </summary>
    [TestMethod]
    public void TestConicWallNearItsSlant()
    {
        foreach (double degrees in new[] { 1.0, 0.1, 0.01 })
        {
            double radians = (45 - degrees).ToRadians();

            AssertTheSameAtEveryScale(
                $"a cone met at {degrees} degrees off its slant",
                scale => new Conic
                {
                    MinimumY = -100, MaximumY = 100, Closed = false,
                    Transform = Transforms.Scale(scale)
                },
                scale => new Ray(
                    new Point(-60 * scale, 0, 0),
                    new Vector(Math.Cos(radians), Math.Sin(radians), 0)));
        }
    }

    /// <summary>
    /// A ray running along a cone's slant meets it exactly once, and that single crossing is
    /// subject to the cone's truncation just as the ordinary pair are.  It was not checked against
    /// it, so this branch reported a hit far outside the cone's own extent -- a phantom surface
    /// hanging in space above a cut-off cone.  Nothing showed it while the branch was reached only
    /// by exactly parallel rays, which are a measure-zero set; a scaled cone reached it for a wide
    /// spread of directions and put the phantom on screen.
    /// </summary>
    [TestMethod]
    public void TestConicAlongItsSlantHonorsItsTruncation()
    {
        // Along the slant exactly, from a point that puts the one crossing well above MaximumY.
        Conic cone = new () { MinimumY = -1, MaximumY = 1, Closed = false };
        Ray ray = new (new Point(-60, 0, 0), new Vector(1, 1, 0).Unit);
        List<Intersection> intersections = [];

        cone.Intersect(ray, intersections);

        foreach (Intersection intersection in intersections)
        {
            double y = ray.Origin.Y + intersection.Distance * ray.Direction.Y;

            Assert.IsTrue(y > cone.MinimumY && y < cone.MaximumY,
                $"the cone is cut off at Y = {cone.MaximumY} but reported a crossing at Y = {y}.");
        }
    }

    /// <summary>
    /// A plane is missed only by a ray that runs along it, which is a question about the ray's
    /// angle and not about the size of its Y component.
    /// </summary>
    [TestMethod]
    public void TestPlaneAtAGrazingAngle()
    {
        foreach (double degrees in new[] { 0.1, 0.01, 0.001 })
        {
            double radians = degrees.ToRadians();

            AssertTheSameAtEveryScale(
                $"a plane grazed at {degrees} degrees",
                scale => new Plane { Transform = Transforms.Scale(scale) },
                scale => new Ray(
                    new Point(0, 10 * scale, 0),
                    new Vector(Math.Cos(radians), -Math.Sin(radians), 0)));
        }
    }

    /// <summary>
    /// The triangle's determinant is the ray's direction times twice the triangle's area times the
    /// cosine of the angle between them, so a small triangle in a scaled-up surface put it under a
    /// fixed threshold with the ray nowhere near running along it.  The size here is a 256-square
    /// height field's, which is why a terrain -- built in a unit cube and scaled up, as every
    /// terrain is -- lost its mesh altogether.
    /// </summary>
    [TestMethod]
    public void TestSmallTriangleWhenScaledUp()
    {
        const double edge = 1.0 / 256;

        AssertTheSameAtEveryScale(
            "a height field's triangle",
            scale => new Triangle
            {
                Point1 = new Point(0, 0, 0),
                Point2 = new Point(edge, 0, 0),
                Point3 = new Point(0, 0, edge),
                Transform = Transforms.Scale(scale)
            },
            scale => new Ray(
                new Point(edge / 4 * scale, scale, edge / 4 * scale), new Vector(0, -1, 0)));
    }

    /// <summary>
    /// The same determinant, and the same correction, for the flat surfaces -- which reach it
    /// through <c>FlatSurface.GetPlaneDistance</c> rather than through the triangle.
    /// </summary>
    [TestMethod]
    public void TestParallelogramAtAGrazingAngle()
    {
        foreach (double degrees in new[] { 0.01, 0.001 })
        {
            double radians = degrees.ToRadians();

            // The square is two units across and its normal is unit, so the denominator is the
            // ray's own direction times the sine of this angle and nothing else masks it.  The ray
            // starts just above the square and skims down across it, landing well inside.
            double height = 0.8 * Math.Tan(radians);

            AssertTheSameAtEveryScale(
                $"a parallelogram grazed at {degrees} degrees",
                scale => new Parallelogram
                {
                    Point = new Point(-1, 0, -1),
                    Side1 = new Vector(2, 0, 0),
                    Side2 = new Vector(0, 0, 2),
                    Transform = Transforms.Scale(scale)
                },
                scale => new Ray(
                    new Point(-0.4 * scale, height * scale, 0),
                    new Vector(Math.Cos(radians), -Math.Sin(radians), 0)));
        }
    }

    /// <summary>
    /// A blob's own primitives carry the same two quantities.  A ball's is the square of the ray's
    /// direction outright, so compared against a fixed millionth the whole surface stopped being
    /// there once it was scaled past a thousand; a bond's is that times the square of the sine of
    /// the ray's angle to the bond, which is the cylinder's fault over again.
    /// </summary>
    [TestMethod]
    public void TestBlobAtScale()
    {
        // Across the pair, and then very nearly along the bond, which is what reaches the second.
        (string What, Vector Direction, Point Origin)[] cases =
        [
            ("across a blob", new Vector(0, 0, -1), new Point(0, 3, 10)),
            ("along a blob's bond", new Vector(0, -1, 0.005).Unit, new Point(0.4, 12, 0))
        ];

        foreach ((string what, Vector direction, Point origin) in cases)
        {
            AssertTheSameAtEveryScale(what,
                scale =>
                {
                    Blob blob = new ()
                    {
                        Threshold = 0.125,
                        Components =
                        {
                            new BlobSphereComponent
                            {
                                Center = Point.Zero, Radius = 2, Strength = 1
                            },
                            new BlobCylinderComponent
                            {
                                Start = Point.Zero, End = new Point(0, 6, 0),
                                Radius = 1.5, Strength = 1
                            }
                        },
                        Transform = Transforms.Scale(scale)
                    };

                    blob.PrepareForRendering();

                    return blob;
                },
                scale => new Ray(
                    new Point(origin.X * scale, origin.Y * scale, origin.Z * scale), direction));
        }
    }

    /// <summary>
    /// A bounding box that refuses to divide by a small direction component declares a merely slow
    /// axis to be a parallel one, and answers with infinities of one sign -- which reports a miss,
    /// dropping the box and everything inside it.  The direction here is what a ray carried into a
    /// hugely scaled surface's space looks like.
    /// </summary>
    [TestMethod]
    public void TestBoundingBoxWithAVerySlowAxis()
    {
        // A sphere's own box runs -1 to 1, and Surface.Intersect gates on it.  This ray travels
        // mostly along Z while climbing slowly in Y -- slowly enough that its Y component falls
        // under a fixed millionth -- and it passes clean through the middle of the sphere.
        Sphere ball = new () { BoundingBox = new BoundingBox() };

        ball.BoundingBox.Add(new Point(-1, -1, -1));
        ball.BoundingBox.Add(new Point(1, 1, 1));

        Ray ray = new (new Point(0, -2, -4_000_000), new Vector(0, 5e-7, 1));
        List<Intersection> intersections = [];

        ball.Intersect(ray, intersections);

        Assert.AreEqual(2, intersections.Count,
            "the ray climbs through the sphere, so its box must not discard it as parallel.");
    }
}
