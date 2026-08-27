using Complex = System.Numerics.Complex;
using MathNet.Numerics;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Instructions;
using RayTracer.Instructions.Pigments;
using RayTracer.Instructions.Surfaces;
using RayTracer.Instructions.Transforms;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// This class tests the three things a blob component may carry beyond its own shape: a plane as the
/// shape itself, a transform to see that shape through, and a pigment of its own.
/// </summary>
[TestClass]
public class TestBlobComponents
{
    /// <summary>
    /// The whole premise of a plane component: the squared distance from a plane must come out as a
    /// quadratic in the ray's own parameter, since a blob's solver can take apart nothing else.  A
    /// ray dropping onto the XZ plane from four up, at forty-five degrees, is a case the numbers can
    /// be written down for.
    /// </summary>
    [TestMethod]
    public void TestAPlanesSquaredDistanceIsQuadraticAlongARay()
    {
        BlobPlanePrimitive plane = new (Point.Zero, Directions.Up, 1, 1);
        Ray ray = new (new Point(0, 4, 0), new Vector(1, -1, 0).Unit);
        (double t0, double t1, double t2) = plane.GetDistanceSquaredCoefficients(ray);

        // The height runs as 4 - t/sqrt(2), so its square is 16 - 8t/sqrt(2) + t^2/2, and the middle
        // coefficient is reported as half of itself.
        Assert.AreEqual(16, t0, 1e-9);
        Assert.AreEqual(-4 / Math.Sqrt(2), t1, 1e-9);
        Assert.AreEqual(0.5, t2, 1e-9);
    }

    /// <summary>
    /// A ray crossing the slab is in range over exactly the stretch between its two faces.
    /// </summary>
    [TestMethod]
    public void TestARayCrossingTheSlabIsInRangeBetweenItsFaces()
    {
        BlobPlanePrimitive plane = new (Point.Zero, Directions.Up, 2, 1);
        Ray ray = new (new Point(0, 5, 0), Directions.Down);
        (double Enter, double Exit)? interval = plane.GetBoundingInterval(ray);

        Assert.IsNotNull(interval);
        Assert.AreEqual(3, interval.Value.Enter, 1e-9);
        Assert.AreEqual(7, interval.Value.Exit, 1e-9);
    }

    /// <summary>
    /// **The case a render cannot easily be made to show.**  A ray running along the slab rather
    /// than across it never leaves it, so the honest interval is unbounded -- and something has to
    /// cope with that, since a shadow ray cast along a floor a blob has merged into is exactly this.
    /// A parallel ray outside the slab, by contrast, is never in range at all.
    /// </summary>
    [TestMethod]
    public void TestARayRunningAlongTheSlabIsInRangeForever()
    {
        BlobPlanePrimitive plane = new (Point.Zero, Directions.Up, 2, 1);
        (double Enter, double Exit)? inside = plane.GetBoundingInterval(
            new Ray(new Point(0, 1, 0), new Vector(1, 0, 0)));
        (double Enter, double Exit)? outside = plane.GetBoundingInterval(
            new Ray(new Point(0, 9, 0), new Vector(1, 0, 0)));

        Assert.IsNotNull(inside);
        Assert.IsTrue(double.IsNegativeInfinity(inside.Value.Enter));
        Assert.IsTrue(double.IsPositiveInfinity(inside.Value.Exit));
        Assert.IsNull(outside);
    }

    /// <summary>
    /// And the unbounded interval has to survive the sweep through the components and come out as a
    /// pair of real crossings, which is what proves the infinity is carried rather than merely not
    /// crashing.  A blob of one plane is a slab of solid, so a ray fired across it enters and leaves
    /// where the field passes the threshold -- at 1 - cbrt(threshold) of the radius, either side.
    /// </summary>
    [TestMethod]
    public void TestABlobOfOnePlaneIsASlabOfSolid()
    {
        Blob blob = new ()
        {
            Threshold = 0.125,
            Components =
            {
                new BlobPlaneComponent
                {
                    Point = Point.Zero, Normal = Directions.Up, Radius = 2, Strength = 1
                }
            }
        };
        List<Intersection> intersections = [];

        blob.PrepareForRendering();
        blob.Intersect(new Ray(new Point(0, 5, 0), Directions.Down), intersections);

        // strength * (1 - d^2/R^2)^3 = threshold gives d = R / sqrt(2), so the faces sit at
        // +/- sqrt(2), which the ray reaches at 5 - sqrt(2) and 5 + sqrt(2).
        List<double> distances = intersections.Select(i => i.Distance).Order().ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.AreEqual(5 - Math.Sqrt(2), distances[0], 1e-6);
        Assert.AreEqual(5 + Math.Sqrt(2), distances[1], 1e-6);
    }

    /// <summary>
    /// A slab's normal points straight out of the plane, on whichever side the point is.
    /// </summary>
    [TestMethod]
    public void TestASlabsNormalPointsOutOfThePlane()
    {
        BlobPlanePrimitive plane = new (Point.Zero, Directions.Up, 2, 1);
        (double Density, Vector Gradient)? above = plane.EvaluateAt(new Point(3, 1, -4));
        (double Density, Vector Gradient)? below = plane.EvaluateAt(new Point(3, -1, -4));

        Assert.IsNotNull(above);
        Assert.IsNotNull(below);
        Assert.IsTrue(above.Value.Gradient.Unit.Matches(Directions.Up));
        Assert.IsTrue(below.Value.Gradient.Unit.Matches(Directions.Down));

        // Equally far out on either side, so equally dense.
        Assert.AreEqual(above.Value.Density, below.Value.Density, 1e-12);
    }

    /// <summary>
    /// A transformed sphere component is an ellipsoid, and it must be one all the way through -- the
    /// crossings in the stretched direction as far out as the stretch, and the ones across it
    /// untouched.  This is the test that the ray's parameter survives the transform: if it did not,
    /// the distances would come back scaled by the stretch.
    /// </summary>
    [TestMethod]
    public void TestATransformedSphereComponentIsAnEllipsoid()
    {
        Blob blob = new ()
        {
            Threshold = 0.125,
            Components =
            {
                new BlobSphereComponent
                {
                    Center = Point.Zero, Radius = 2, Strength = 1,
                    Transform = Transforms.Scale(3, 1, 1)
                }
            }
        };
        List<Intersection> along = [];
        List<Intersection> across = [];

        blob.PrepareForRendering();
        blob.Intersect(new Ray(new Point(-20, 0, 0), new Vector(1, 0, 0)), along);
        blob.Intersect(new Ray(new Point(0, -20, 0), new Vector(0, 1, 0)), across);

        // The untransformed isosurface sits at R / sqrt(2) = sqrt(2); tripled along X and left alone
        // along Y.
        double radius = Math.Sqrt(2);

        Assert.AreEqual(2, along.Count);
        Assert.AreEqual(2, across.Count);
        Assert.AreEqual(20 - radius * 3, along.Select(i => i.Distance).Min(), 1e-6);
        Assert.AreEqual(20 - radius, across.Select(i => i.Distance).Min(), 1e-6);
    }

    /// <summary>
    /// The normal on a squashed component must be the ellipsoid's, not the sphere's it was made
    /// from.  It is the one thing that goes the other way through the transform, and getting it
    /// wrong shades a stretched blob as though it were still round -- which looks plausible enough
    /// to miss.  On an ellipsoid stretched along X, the normal at the point where it crosses the
    /// forty-five degree line leans *toward Y*, away from the point's own direction.
    /// </summary>
    [TestMethod]
    public void TestTheNormalOnAStretchedComponentIsTheEllipsoids()
    {
        BlobTransformedPrimitive primitive = new (
            new BlobSpherePrimitive(Point.Zero, 2, 1), Transforms.Scale(3, 1, 1));

        // A point on the stretched surface: (3x, y) for a point (x, y) on the round one.
        double radius = Math.Sqrt(2);
        double part = radius / Math.Sqrt(2);
        (double Density, Vector Gradient)? result = primitive.EvaluateAt(new Point(part * 3, part, 0));

        Assert.IsNotNull(result);

        Vector normal = result.Value.Gradient.Unit;

        // The world surface is the ellipsoid X^2/9 + Y^2 = r^2, whose gradient is (2X/9, 2Y).  At
        // X = 3Y that puts the normal's Y three times its X.  A sphere's normal at the same point
        // would lean the other way about, at a third, so this tells the two apart nine-fold.
        Assert.AreEqual(3.0, normal.Y / normal.X, 1e-6);
        Assert.AreEqual(1.0 / 3.0, new Vector(part * 3, part, 0).Unit.Y /
            new Vector(part * 3, part, 0).Unit.X, 1e-6,
            "the point's own direction should be the third that the ellipsoid's normal is not");
    }

    /// <summary>
    /// **The test that actually pins the transpose down, and a lesson about the one above it.**  A
    /// scale is a *diagonal* matrix, and a diagonal matrix is its own transpose -- so no test built
    /// on one can tell a normal carried through the inverse from one carried through the inverse
    /// transposed.  Swapping them leaves every scaling test green.  A shear is the cheapest transform
    /// where the two genuinely differ.
    /// <para>
    /// So this asks the field itself rather than the implementation: the density is sampled either
    /// side of a point along each axis, and the gradient that comes out of that arithmetic is
    /// compared with the one the primitive reports.  A normal points the way the field *falls*, so
    /// the two must be opposite.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestTheNormalOnAShearedComponentFollowsTheField()
    {
        BlobTransformedPrimitive primitive = new (
            new BlobSpherePrimitive(Point.Zero, 2, 1),
            Transforms.Shear(0.6, 0, 0, 0, 0, 0));
        Point point = new (0.7, 0.5, -0.3);
        (double Density, Vector Gradient)? result = primitive.EvaluateAt(point);

        Assert.IsNotNull(result);

        const double Step = 1e-6;
        double[] slopes = new double[3];

        for (int axis = 0; axis < 3; axis++)
        {
            Vector along = new (axis == 0 ? Step : 0, axis == 1 ? Step : 0, axis == 2 ? Step : 0);

            slopes[axis] = (DensityAt(primitive, point + along) -
                            DensityAt(primitive, point - along)) / (2 * Step);
        }

        Vector measured = new Vector(slopes[0], slopes[1], slopes[2]).Unit;
        Vector reported = result.Value.Gradient.Unit;

        Assert.IsTrue(reported.Matches(-measured),
            $"the reported normal {reported} does not follow the field, whose own slope points " +
            $"{-measured}; a normal carried through the inverse rather than its transpose looks " +
            "exactly like this, and no test built on a scale alone can see it");
    }

    /// <summary>
    /// This method reads the field at a point, treating anywhere out of range as nothing.
    /// </summary>
    private static double DensityAt(IBlobPrimitive primitive, Point point)
    {
        return primitive.EvaluateAt(point)?.Density ?? 0;
    }

    /// <summary>
    /// The weights a per-component pigment mixes by: at a component's own center it is the only one
    /// contributing, and half way between two matched components they must weigh the same.
    /// </summary>
    [TestMethod]
    public void TestComponentWeightsFollowTheField()
    {
        Blob blob = new ()
        {
            Threshold = 0.125,
            Components =
            {
                new BlobSphereComponent { Center = new Point(-1, 0, 0), Radius = 2, Strength = 1 },
                new BlobSphereComponent { Center = new Point(1, 0, 0), Radius = 2, Strength = 1 }
            }
        };

        blob.PrepareForRendering();

        double[] weights = new double[2];
        double atCenter = blob.GetComponentWeights(new Point(-1, 0, 0), weights);

        Assert.AreEqual(1.0, weights[0] / atCenter, 1e-9);

        double between = blob.GetComponentWeights(Point.Zero, weights);

        Assert.AreEqual(0.5, weights[0] / between, 1e-9);
        Assert.AreEqual(0.5, weights[1] / between, 1e-9);
    }

    /// <summary>
    /// A component of negative strength shapes the blob but must not vote on its color.  Letting it
    /// would mean mixing colors in negative proportions across a total that passes through nought,
    /// and the visible result is a dark band where the carving reaches.
    /// </summary>
    [TestMethod]
    public void TestACarvingComponentDoesNotVoteOnColor()
    {
        Blob blob = new ()
        {
            Threshold = 0.125,
            Components =
            {
                new BlobSphereComponent { Center = Point.Zero, Radius = 2, Strength = 1 },
                new BlobSphereComponent { Center = new Point(1, 0, 0), Radius = 2, Strength = -1 }
            }
        };

        blob.PrepareForRendering();

        double[] weights = new double[2];
        double total = blob.GetComponentWeights(new Point(0.5, 0, 0), weights);

        Assert.IsTrue(total > 0, "the adding component alone should carry the whole weight");
        Assert.AreEqual(0, weights[1], 1e-12);
        Assert.AreEqual(total, weights[0], 1e-12);
    }

    /// <summary>
    /// And the pigment itself: two components carrying colors of their own must mix them in the
    /// proportion each contributes, so half way between a red one and a blue one is exactly half of
    /// each -- and at the middle of the red one, all red.
    /// </summary>
    [TestMethod]
    public void TestComponentPigmentsMixByTheirShareOfTheField()
    {
        Blob blob = new ()
        {
            Threshold = 0.125,
            Material = new Material(),
            Components =
            {
                new BlobSphereComponent
                {
                    Center = new Point(-1, 0, 0), Radius = 2, Strength = 1,
                    Pigment = new SolidPigment(Colors.Red)
                },
                new BlobSphereComponent
                {
                    Center = new Point(1, 0, 0), Radius = 2, Strength = 1,
                    Pigment = new SolidPigment(Colors.Blue)
                }
            }
        };

        blob.PrepareForRendering();
        blob.MaterialIsSettled();

        Assert.IsInstanceOfType<BlobPigment>(blob.Material.Pigment);

        Color middle = blob.Material.Pigment.GetColorFor(blob, Point.Zero);
        Color atRed = blob.Material.Pigment.GetColorFor(blob, new Point(-1, 0, 0));

        Assert.AreEqual(0.5, middle.Red, 1e-9);
        Assert.AreEqual(0.5, middle.Blue, 1e-9);
        Assert.AreEqual(0, middle.Green, 1e-9);
        Assert.IsTrue(atRed.Matches(Colors.Red));
    }

    /// <summary>
    /// A component that names no pigment goes on being colored by the blob's own, so mixing one that
    /// does with one that does not gives a blend of the two rather than a hole.
    /// </summary>
    [TestMethod]
    public void TestAComponentWithNoPigmentKeepsTheBlobsOwn()
    {
        Blob blob = new ()
        {
            Threshold = 0.125,
            Material = new Material { Pigment = new SolidPigment(Colors.Green) },
            Components =
            {
                new BlobSphereComponent
                {
                    Center = new Point(-1, 0, 0), Radius = 2, Strength = 1,
                    Pigment = new SolidPigment(Colors.Red)
                },
                new BlobSphereComponent { Center = new Point(1, 0, 0), Radius = 2, Strength = 1 }
            }
        };

        blob.PrepareForRendering();
        blob.MaterialIsSettled();

        Color middle = blob.Material.Pigment.GetColorFor(blob, Point.Zero);

        // Half of red and half of the blob's own green -- which is the named HTML green, and so is
        // half of a full one, rather than the pure green it is easy to assume it must be.
        Assert.AreEqual(0.5, middle.Red, 1e-9);
        Assert.AreEqual(Colors.Green.Green / 2, middle.Green, 1e-9);
    }

    /// <summary>
    /// **A second render must not wrap the wrapping.**  A process that renders twice -- the exit
    /// code tests do -- would otherwise mix the mixture again, each pass adding a layer, which is
    /// the very shape of a compounding bug this renderer has been bitten by before.
    /// </summary>
    [TestMethod]
    public void TestTheBlendIsNotPutOnTwice()
    {
        Blob blob = new ()
        {
            Threshold = 0.125,
            Material = new Material(),
            Components =
            {
                new BlobSphereComponent
                {
                    Center = Point.Zero, Radius = 2, Strength = 1,
                    Pigment = new SolidPigment(Colors.Red)
                }
            }
        };

        blob.PrepareForRendering();
        blob.MaterialIsSettled();

        Pigment first = blob.Material.Pigment;

        blob.PrepareForRendering();
        blob.MaterialIsSettled();

        Assert.AreSame(first, blob.Material.Pigment);
    }

    /// <summary>
    /// A blob none of whose components carry a color is left entirely alone, which is what keeps
    /// every scene written before any of this existed rendering exactly as it did.
    /// </summary>
    [TestMethod]
    public void TestABlobWithNoComponentColorsIsUntouched()
    {
        Pigment pigment = new SolidPigment(Colors.Green);
        Blob blob = new ()
        {
            Threshold = 0.125,
            Material = new Material { Pigment = pigment },
            Components = { new BlobSphereComponent { Center = Point.Zero, Radius = 2, Strength = 1 } }
        };

        blob.PrepareForRendering();
        blob.MaterialIsSettled();

        Assert.AreSame(pigment, blob.Material.Pigment);
    }

    /// <summary>
    /// **A blob must not take the solver's word for it.**  The sweep adds each component's
    /// polynomial as that component switches on and subtracts it as it switches off, and those do
    /// not cancel exactly: over a stretch of ray where nothing is in range, what ought to be the
    /// plain constant <c>-threshold</c> comes out as that constant with dust of about 1e-16 left in
    /// its top coefficients.  Handed that as a *sextic*, the solver takes the dust for a leading
    /// term and hands back values that are roots of nothing -- the polynomial stands at the whole of
    /// <c>-threshold</c> at them.  Ones landing inside the interval are taken for surface points,
    /// where no component is in range, so the normal has no gradient to be built from and the pixel
    /// comes out as a dark speck.  One frame carried 159 of them.
    /// </summary>
    [TestMethod]
    public void TestDustInTheTopCoefficientsIsNotMistakenForARoot()
    {
        double[] dusty = [-0.3, 0, 0, 0, 0, 0, -1.11e-16];

        // The premise, asserted rather than assumed: the polynomial is nowhere near nought here.
        // This is the shape of a value taken from a render that had gone wrong.
        Assert.AreEqual(-0.3, dusty.Select((c, i) => c * Math.Pow(1.67929284695121, i)).Sum(), 1e-9);

        Assert.IsFalse(Blob.IsGenuineRoot(dusty, 1.67929284695121),
            "a value the polynomial does not vanish at was accepted as a root");
    }

    /// <summary>
    /// And the same guard must let a real root through, or a blob would have no surface at all.  The
    /// arithmetic never lands exactly on nought, so what it has to tolerate is a root that comes out
    /// a few last bits away from one.
    /// </summary>
    [TestMethod]
    public void TestAGenuineRootIsStillAccepted()
    {
        // (t + 1)(t - 0.5)(t - 1.5) again, and its roots, nudged by the last bit a double holds.
        double[] cubic = [0.75, -1.25, -1.0, 1.0];

        foreach (double root in (double[]) [-1.0, 0.5, 1.5])
        {
            Assert.IsTrue(Blob.IsGenuineRoot(cubic, root), $"the root at {root} was thrown away");
            Assert.IsTrue(Blob.IsGenuineRoot(cubic, Math.BitIncrement(root)),
                $"the root at {root} was thrown away when it came back a single bit high");
        }
    }

    /// <summary>
    /// A blob's crossing is solved for rather than written down, so it asks for rays leaving it to
    /// start further off itself than a shape whose crossing is exact.  Without that it shadows
    /// itself in speckles wherever the light grazes along it.
    /// </summary>
    [TestMethod]
    public void TestABlobAsksForARoomierSelfOffsetThanAnExactShape()
    {
        Assert.IsTrue(new Blob().SelfOffsetScale > new Sphere().SelfOffsetScale,
            "a surface whose crossings are solved for needs more room than one whose are exact");
        Assert.AreEqual(1, new Sphere().SelfOffsetScale,
            "every shape with a closed-form crossing must keep the offset it has always had, or " +
            "every scene in the gallery shifts");
    }

    /// <summary>
    /// **Every sort of component must actually receive the properties they share.**  Strength,
    /// pigment and transform are resolved once in the base and read back out of it, and this asks
    /// each sort of component in turn whether all three arrived.
    /// <para>
    /// What it does *not* catch, and it is worth saying so rather than letting the next reader
    /// assume otherwise: a component resolver that declares one of these properties over again,
    /// hiding the base's.  One did, and the compiler warned about it.  This test cannot see such a
    /// thing because it assigns through the base -- which is exactly what the parser does too, and
    /// exactly why the hiding was harmless rather than a silent loss.  The build warning is the
    /// guard there; this is the guard for the properties genuinely going astray.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestTheSharedPropertiesReachEverySortOfComponent()
    {
        Pigment pigment = new SolidPigment(Colors.Red);
        Matrix transform = Transforms.Scale(2, 3, 4);

        BlobSphereComponent sphere = (BlobSphereComponent) Resolved(
            new BlobSphereComponentResolver { RadiusResolver = Literal(1.0) }, pigment, transform);
        BlobCylinderComponent cylinder = (BlobCylinderComponent) Resolved(
            new BlobCylinderComponentResolver
            {
                StartResolver = new LiteralResolver<Point> { Value = Point.Zero },
                EndResolver = new LiteralResolver<Point> { Value = new Point(1, 0, 0) },
                RadiusResolver = Literal(1.0)
            }, pigment, transform);
        BlobPlaneComponent plane = (BlobPlaneComponent) Resolved(
            new BlobPlaneComponentResolver
            {
                NormalResolver = new LiteralResolver<Vector> { Value = Directions.Up },
                RadiusResolver = Literal(1.0)
            }, pigment, transform);

        foreach (IBlobComponent component in (IBlobComponent[]) [sphere, cylinder, plane])
        {
            string what = component.GetType().Name;

            Assert.AreEqual(7.25, component.Strength, 1e-12, $"{what} lost its strength");
            Assert.AreSame(pigment, component.Pigment, $"{what} lost its pigment");
            Assert.AreSame(transform, component.Transform, $"{what} lost its transform");
        }
    }

    /// <summary>
    /// This method gives a component resolver the properties every component shares -- the way the
    /// parser does, through the base -- and resolves it.
    /// </summary>
    private static IBlobComponent Resolved<TComponent>(
        BlobComponentResolver<TComponent> resolver, Pigment pigment, Matrix transform)
        where TComponent : BlobComponent, new()
    {
        resolver.StrengthResolver = Literal(7.25);
        resolver.PigmentResolver = new TestPigmentResolver { Pigment = pigment };
        resolver.TransformResolver = new TestTransformResolver { Matrix = transform };

        return (IBlobComponent) resolver.ResolveToObject(new RenderContext(), new Variables());
    }

    /// <summary>
    /// This method wraps a constant in a resolver.
    /// </summary>
    private static Resolver<double> Literal(double value)
    {
        return new LiteralResolver<double> { Value = value };
    }

    /// <summary>
    /// A pigment resolver that hands back the pigment it was given.
    /// </summary>
    private class TestPigmentResolver : IPigmentResolver
    {
        public Pigment Pigment { get; init; }

        public Pigment ResolveToPigment(RenderContext context, Variables variables) => Pigment;

        public object ResolveToObject(RenderContext context, Variables variables) => Pigment;

        public object Clone() => this;
    }

    /// <summary>
    /// A transform resolver that hands back the matrix it was given.
    /// </summary>
    private class TestTransformResolver : TransformResolver
    {
        public Matrix Matrix { get; init; }

        public override Matrix Resolve(RenderContext context, Variables variables) => Matrix;
    }
}
