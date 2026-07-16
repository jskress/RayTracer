using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a superellipsoid: an implicit "rounded box/pillow" surface whose
/// shape ranges continuously from a sphere to a cube to an octahedron, controlled by two
/// exponents.  Unlike this project's other quadric/quartic surfaces, a superellipsoid has no
/// closed-form ray intersection; hits are found by sampling the ray at a handful of
/// analytically-known points and root-finding between samples where the implicit function
/// changes sign.  Ported from POV-Ray's own superellipsoid primitive
/// (<c>source/backend/shape/super.cpp</c>'s <c>Intersect</c>/<c>Normal</c>/<c>evaluate_g</c>/
/// <c>evaluate_superellipsoid</c>, and the precompute step in
/// <c>source/backend/parser/parse.cpp</c>'s <c>Parse_Superellipsoid</c>) -- with one deliberate
/// departure: POV's own <c>check_hit2</c> (a single "does the ray graze the surface here"
/// secant march) can miss a genuine entry/exit pair that both fall inside one same-signed
/// sample interval, which happens visibly near sharp edges (see
/// <see cref="FindHitsWithinSameSignInterval"/> for the fixed-subdivision replacement used
/// here instead).
/// </summary>
public class Superellipsoid : Surface
{
    // The implicit surface is (|x|^(2/e) + |y|^(2/e))^(e/n) + |z|^(2/n) - 1 = 0.  All the ray
    // sampling below stays within this fixed local unit box (padded, exactly matching
    // POV-Ray's own MIN_VALUE/MAX_VALUE for its intersect_box search domain -- note this is
    // deliberately more generous than the tight ±1.0001 box POV uses for external bounding-box
    // culling elsewhere, which this project doesn't need since neither Torus nor Egg override
    // GetDefaultBoundingBox either).
    private const double BoxPadding = 0.01;

    /// <summary>Minimal intersection depth for a valid intersection.</summary>
    private const double DepthTolerance = 1.0e-4;

    /// <summary>If a function value's absolute value is below this, it is regarded as zero.</summary>
    private const double ZeroTolerance = 1.0e-10;

    private const int MaxIterations = 20;

    // The nine planes (Ax + By + Cz = 0) that subdivide the superellipsoid into pieces within
    // which the implicit function is guaranteed monotonic along any given ray -- sampling at
    // these planes (in addition to the ray's own box entry/exit points) turns "root-finding
    // along an arbitrary curve" into "root-finding within a monotonic-ish piece."
    private static readonly (double A, double B, double C)[] Planes =
    [
        (1, 1, 0), (1, -1, 0),
        (1, 0, 1), (1, 0, -1),
        (0, 1, 1), (0, 1, -1),
        (1, 0, 0),
        (0, 1, 0),
        (0, 0, 1)
    ];

    /// <summary>
    /// This property provides the east/west exponent (POV-Ray's "e") of the superellipsoid.
    /// </summary>
    public double EastWest { get; set; }

    /// <summary>
    /// This property provides the north/south exponent (POV-Ray's "n") of the superellipsoid.
    /// </summary>
    public double NorthSouth { get; set; }

    private double _powerX;
    private double _powerY;
    private double _powerZ;
    private BoundingBox _localBox;

    /// <summary>
    /// This method precomputes the values that stay constant for the superellipsoid,
    /// regardless of the ray being tested.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (EastWest <= 0 || NorthSouth <= 0)
            throw new Exception("A superellipsoid's east/west and north/south exponents must both be positive.");

        _powerX = 2.0 / EastWest;
        _powerY = EastWest / NorthSouth;
        _powerZ = 2.0 / NorthSouth;

        _localBox = new BoundingBox();

        _localBox.Add(new Point(-1, -1, -1));
        _localBox.Add(new Point(1, 1, 1));
        _localBox.Expand(BoxPadding);
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the superellipsoid
    /// and, if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double length = ray.Direction.Magnitude;
        Ray localRay = new (ray.Origin, ray.Direction.Unit);
        (double tMin, double tMax) = _localBox.GetIntersections(localRay);

        if (tMin > tMax || tMax < DepthTolerance)
            return;

        // A ray whose origin already lies inside the local search box -- always true for
        // shadow/reflection/refraction rays, which start right on the surface itself -- would
        // otherwise get its first sample pushed forward by a whole DepthTolerance along the
        // ray's own direction.  For a ray that isn't aligned with the surface's outward normal
        // there (a very ordinary thing for a shadow ray heading off toward a light), that push
        // can walk straight back across the surface, undoing the tiny escape epsilon the
        // renderer already applied (Intersection.OverPoint) and manufacturing a false
        // self-intersection.  Starting at the ray's own origin instead (t=0) trusts that
        // epsilon rather than fighting it; the DepthTolerance clamp is still exactly right for
        // a ray genuinely arriving from outside the box, where tMin is meaningfully positive.
        if (tMin < 0)
            tMin = 0;

        List<double> samples = CollectSamplePoints(localRay, tMin, tMax);
        double v0 = EvaluateSuperellipsoid(localRay.At(samples[0]));

        // When a sample lands within ZeroTolerance of the surface, its hit is recorded here,
        // but the near-zero value then carries forward as the next interval's own v0 -- if
        // the true sign flips across that same already-recorded point, the sign-change branch
        // below would otherwise re-detect (and duplicate) it.  Tracking the last hit found and
        // skipping anything within DepthTolerance of it keeps each real crossing single, no
        // matter which of the branches below happens to notice it first.
        double? lastHitT = null;

        void AddHit(double t)
        {
            if (lastHitT is { } last && Math.Abs(t - last) < DepthTolerance)
                return;

            lastHitT = t;
            intersections.Add(new Intersection(this, t / length));
        }

        if (Math.Abs(v0) < ZeroTolerance)
            AddHit(samples[0]);

        for (int i = 1; i < samples.Count; i++)
        {
            double t1 = samples[i];
            double v1 = EvaluateSuperellipsoid(localRay.At(t1));

            if (Math.Abs(v1) < ZeroTolerance)
                AddHit(t1);
            else if (v0 * v1 < 0)
                AddHit(SolveHit(localRay, v0, samples[i - 1], v1, t1));
            else
                FindHitsWithinSameSignInterval(localRay, samples[i - 1], v0, t1, AddHit);

            v0 = v1;
        }
    }

    /// <summary>
    /// This method looks for surface crossings within an interval whose two endpoints happen
    /// to have function values of the same sign -- which does not guarantee the ray misses the
    /// surface there.  Near a sharp edge (where several of the nine subdividing planes converge
    /// closely together), the true entry and exit points of a chord can both fall inside one
    /// such interval, hidden between the two sampled endpoints; a single "does it graze"
    /// heuristic (as POV-Ray's own <c>check_hit2</c> uses) can miss this kind of paired
    /// crossing.  Subdividing the interval into small steps and looking for a sign change (or a
    /// near-zero value) between each consecutive pair reliably finds every crossing instead.
    /// </summary>
    /// <param name="localRay">The ray, in local, unit-direction space.</param>
    /// <param name="t0">The near ray parameter.</param>
    /// <param name="v0">The function value at <paramref name="t0"/>.</param>
    /// <param name="t1">The far ray parameter.</param>
    /// <param name="addHit">The callback to invoke for each ray parameter found to be a hit.</param>
    private void FindHitsWithinSameSignInterval(Ray localRay, double t0, double v0, double t1, Action<double> addHit)
    {
        const int subdivisions = 32;
        double step = (t1 - t0) / subdivisions;
        double previousT = t0;
        double previousV = v0;

        for (int i = 1; i <= subdivisions; i++)
        {
            double currentT = i == subdivisions ? t1 : t0 + i * step;
            double currentV = EvaluateSuperellipsoid(localRay.At(currentT));

            if (Math.Abs(currentV) < ZeroTolerance)
                addHit(currentT);
            else if (previousV * currentV < 0)
                addHit(SolveHit(localRay, previousV, previousT, currentV, currentT));

            previousT = currentT;
            previousV = currentV;
        }
    }

    /// <summary>
    /// This method collects the ray parameters at which to sample the implicit function: the
    /// ray's own entry/exit points on the local search box, plus any of the nine subdividing
    /// planes the ray crosses within that range, sorted ascending.
    /// </summary>
    /// <param name="localRay">The ray, in local, unit-direction space.</param>
    /// <param name="tMin">The ray parameter where it enters the local search box.</param>
    /// <param name="tMax">The ray parameter where it exits the local search box.</param>
    /// <returns>The sorted list of ray parameters to sample at.</returns>
    private static List<double> CollectSamplePoints(Ray localRay, double tMin, double tMax)
    {
        List<double> samples = [tMin, tMax];
        double adjust = DoubleExtensions.Epsilon * (tMax - tMin);
        double loT = tMin - adjust;
        double hiT = tMax + adjust;

        foreach ((double a, double b, double c) in Planes)
        {
            double d = localRay.Direction.X * a + localRay.Direction.Y * b + localRay.Direction.Z * c;

            if (Math.Abs(d) < DoubleExtensions.Epsilon)
                continue;

            double t = -(localRay.Origin.X * a + localRay.Origin.Y * b + localRay.Origin.Z * c) / d;

            if (t >= loT && t <= hiT)
                samples.Add(t);
        }

        samples.Sort();

        return samples;
    }

    /// <summary>
    /// This method homes in on the root of the implicit function between two ray parameters
    /// known to have opposite-signed function values, using a combination of secant and
    /// bisection methods (whichever shrinks the bracket more at each step).
    /// </summary>
    /// <param name="localRay">The ray, in local, unit-direction space.</param>
    /// <param name="v0">The function value at <paramref name="t0"/>.</param>
    /// <param name="t0">The near ray parameter.</param>
    /// <param name="v1">The function value at <paramref name="t1"/>.</param>
    /// <param name="t1">The far ray parameter.</param>
    /// <returns>The ray parameter of the root.</returns>
    private double SolveHit(Ray localRay, double v0, double t0, double v1, double t1)
    {
        for (int i = 0; i < MaxIterations; i++)
        {
            if (Math.Abs(v0) < ZeroTolerance)
                return t0;

            if (Math.Abs(v1) < ZeroTolerance)
                return t1;

            double secantFraction = Math.Abs(v0) / Math.Abs(v1 - v0);
            double tSecant = t0 + secantFraction * (t1 - t0);
            double vSecant = EvaluateSuperellipsoid(localRay.At(tSecant));
            double tBisect = t0 + 0.5 * (t1 - t0);
            double vBisect = EvaluateSuperellipsoid(localRay.At(tBisect));

            if (vSecant * vBisect < 0)
            {
                v0 = vSecant; t0 = tSecant;
                v1 = vBisect; t1 = tBisect;
            }
            else if (Math.Abs(vSecant) < Math.Abs(vBisect))
            {
                if (v0 * vSecant < 0) { v1 = vSecant; t1 = tSecant; }
                else { v0 = vSecant; t0 = tSecant; }
            }
            else
            {
                if (v0 * vBisect < 0) { v1 = vBisect; t1 = tBisect; }
                else { v0 = vBisect; t0 = tBisect; }
            }
        }

        return Math.Abs(v0) < Math.Abs(v1) ? t0 : t1;
    }

    /// <summary>
    /// This method evaluates the superellipsoid's implicit function at the given (local-space)
    /// point: negative inside, positive outside, zero on the surface.
    /// </summary>
    /// <param name="point">The point to evaluate the function at.</param>
    /// <returns>The function's value at the given point.</returns>
    private double EvaluateSuperellipsoid(Point point)
    {
        return EvaluateG(EvaluateG(Math.Abs(point.X), Math.Abs(point.Y), _powerX), Math.Abs(point.Z), _powerZ) - 1;
    }

    /// <summary>
    /// This method computes a generalized power mean of two non-negative values, used to build
    /// up the superellipsoid's implicit function one axis pair at a time.  It is written to
    /// divide by the larger of the two magnitudes first, for numerical safety.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <param name="power">The power to raise the ratio to.</param>
    /// <returns>The generalized power mean of the two values.</returns>
    private static double EvaluateG(double x, double y, double power)
    {
        double g;

        if (x > y)
        {
            g = 1 + Power(y / x, power);

            if (g != 1)
                g = Power(g, 1 / power);

            g *= x;
        }
        else if (y != 0)
        {
            g = 1 + Power(x / y, power);

            if (g != 1)
                g = Power(g, 1 / power);

            g *= y;
        }
        else
            g = 0;

        return g;
    }

    /// <summary>
    /// This method raises a value to a power, fast-pathing small integer powers.
    /// </summary>
    /// <param name="x">The value to raise to a power.</param>
    /// <param name="power">The power to raise the value to.</param>
    /// <returns>The value, raised to the given power.</returns>
    private static double Power(double x, double power)
    {
        int i = (int) power;

        if (power != i)
            return Math.Pow(x, power);

        return i switch
        {
            0 => 1.0,
            1 => x,
            2 => x * x,
            3 => x * x * x,
            4 => x * x * x * x,
            5 => x * x * x * x * x,
            6 => x * x * x * x * x * x,
            _ => Math.Pow(x, power)
        };
    }

    /// <summary>
    /// This method returns the normal for the superellipsoid.  It is assumed that the point
    /// will have been transformed to surface-space coordinates.  The vector returned will also
    /// be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        double x = point.X;
        double y = point.Y;
        double z = point.Z;
        double z2n = 0;

        if (z != 0)
        {
            z2n = Power(Math.Abs(z), _powerZ);
            z = z2n / z;
        }

        double r;
        double nx, ny;
        double nz = z;

        if (Math.Abs(x) > Math.Abs(y))
        {
            r = Power(Math.Abs(y / x), _powerX);
            nx = (1 - z2n) / x;
            ny = y != 0 ? (1 - z2n) * r / y : 0;
        }
        else if (y != 0)
        {
            r = Power(Math.Abs(x / y), _powerX);
            nx = x != 0 ? (1 - z2n) * r / x : 0;
            ny = (1 - z2n) / y;
        }
        else
        {
            // On the Z axis (x == y == 0): by symmetry, the normal must point straight along
            // Z.  POV-Ray's own C++ leaves its "r" multiplier uninitialized in this exact
            // case; special-casing it here avoids porting that undefined behavior.
            return new Vector(0, 0, point.Z < 0 ? -1 : 1);
        }

        if (nz != 0)
            nz *= 1 + r;

        return new Vector(nx, ny, nz).Unit;
    }
}
