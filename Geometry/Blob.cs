using Complex = System.Numerics.Complex;
using MathNet.Numerics;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Pigments;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a "blob" -- a surface defined implicitly by a set of components
/// (spheres and/or cylinders), each contributing a smoothly-falling-off density field.  The
/// surface is the isosurface where the sum of all components' fields equals the blob's
/// threshold, so nearby components smoothly merge into one another rather than just
/// touching or overlapping.
/// </summary>
public class Blob : Surface
{
    /// <summary>
    /// This property holds the components that make up the blob.
    /// </summary>
    public List<IBlobComponent> Components { get; } = [];

    /// <summary>
    /// This property holds the threshold value: the field level at which the blob's
    /// surface lies.
    /// </summary>
    public double Threshold { get; set; } = 1;

    /// <summary>
    /// The primitives, each remembering which component it came from.
    /// <para>
    /// The component is kept because a component is not one primitive: a cylinder is a body and two
    /// hemispherical caps, and all three of them have to answer as one when the question is which
    /// component a point belongs to.  Told only the primitives, a bond's rounded ends would take a
    /// different color from its middle.
    /// </para>
    /// </summary>
    private (int Component, IBlobPrimitive Primitive)[] _primitives = [];

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _primitives = Components
            .SelectMany((component, index) => component
                .GetPrimitives()
                .Select(primitive => (Component: index, Primitive: primitive)))
            .ToArray();
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the blob and, if
    /// so, where.  Each primitive contributes a sextic polynomial (in the ray's distance
    /// parameter) while it's within range; we sweep through the sorted set of every
    /// primitive's entry/exit points, maintaining a running sum of whichever primitives are
    /// currently active, and solve for where that sum crosses the threshold within each
    /// resulting sub-interval.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        List<(double Enter, double Exit, double[] Polynomial)> active = [];

        foreach ((_, IBlobPrimitive primitive) in _primitives)
        {
            (double Enter, double Exit)? interval = primitive.GetBoundingInterval(ray);

            if (interval == null)
                continue;

            (double t0, double t1, double t2) = primitive.GetDistanceSquaredCoefficients(ray);
            (double d0, double d1, double d2, double d3) = BlobFieldMath.GetDensityCoefficients(
                primitive.Strength, primitive.RadiusSquared);
            double[] polynomial = BlobFieldMath.GetFieldPolynomial(t0, t1, t2, d0, d1, d2, d3);

            active.Add((interval.Value.Enter, interval.Value.Exit, polynomial));
        }

        if (active.Count == 0)
            return;

        List<(double T, bool Entering, double[] Polynomial)> events = active
            .SelectMany(a => new[] { (a.Enter, true, a.Polynomial), (a.Exit, false, a.Polynomial) })
            .OrderBy(e => e.Item1)
            .ToList();

        double[] coefficients = [-Threshold, 0, 0, 0, 0, 0, 0];

        for (int index = 0; index < events.Count; index++)
        {
            (double t, bool entering, double[] polynomial) = events[index];

            for (int i = 0; i < coefficients.Length; i++)
                coefficients[i] += entering ? polynomial[i] : -polynomial[i];

            bool sameAsNext = index + 1 < events.Count && t.Near(events[index + 1].T);

            if (sameAsNext)
                continue;

            double intervalEnd = index + 1 < events.Count ? events[index + 1].T : t;

            if (intervalEnd > t)
                SolveInterval(ray, coefficients, t, intervalEnd, intersections);
        }
    }

    /// <summary>
    /// This method solves the current, accumulated field polynomial for roots that fall
    /// within the given sub-interval, adding an intersection for each one found.
    /// </summary>
    /// <param name="ray">The ray we are testing.</param>
    /// <param name="coefficients">The field polynomial's coefficients, in ascending order.</param>
    /// <param name="intervalStart">The start of the sub-interval to accept roots in.</param>
    /// <param name="intervalEnd">The end of the sub-interval to accept roots in.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    private void SolveInterval(
        Ray ray, double[] coefficients, double intervalStart, double intervalEnd,
        List<Intersection> intersections)
    {
        if (coefficients.All(coefficient => coefficient.Near(0)))
            return;

        foreach (double t in RealRootsOf(coefficients, intervalStart, intervalEnd))
        {
            if (t >= intervalStart - DoubleExtensions.Epsilon &&
                t <= intervalEnd + DoubleExtensions.Epsilon &&
                IsGenuineRoot(coefficients, t))
                intersections.Add(new Intersection(this, Polished(coefficients, t)));
        }
    }

    /// <summary>
    /// This method takes a root the solver found and closes the last of the way in on it, with a
    /// step or two of Newton's method against the field polynomial.
    /// <para>
    /// **This is what keeps a blob from shadowing itself in speckles.**  A shadow ray starts from
    /// the hit point nudged a millionth of a unit along the normal, and that nudge is the whole of
    /// what stops the surface from blocking its own light.  A millionth is ample when the hit point
    /// is exact -- but a blob's is not handed to it, it is *solved for*, and the eigenvalue solver
    /// leaves an error that can be larger than the nudge.  The point then sits fractionally *inside*
    /// the surface, the shadow ray sets off from within the solid, and every such pixel comes out
    /// dark.  It shows worst where the surface runs nearly along the shadow ray -- the underside of
    /// a bond, or anywhere a plane component lays out a slab for the light to graze across.
    /// </para>
    /// <para>
    /// Two passes are enough: Newton doubles the digits each time, and the solver's answer is
    /// already close.
    /// </para>
    /// </summary>
    /// <param name="coefficients">The field polynomial's coefficients, in ascending order.</param>
    /// <param name="t">The root to polish.</param>
    /// <returns>The root, as close as the arithmetic will put it.</returns>
    private static double Polished(double[] coefficients, double t)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            double value = 0;
            double slope = 0;

            for (int index = coefficients.Length - 1; index > 0; index--)
            {
                value = value * t + coefficients[index];
                slope = slope * t + coefficients[index] * index;
            }

            value = value * t + coefficients[0];

            // Flat here, so there is no step to take -- which is a tangency, where the root is a
            // double one and Newton has nothing to offer anyway.
            if (slope == 0)
                break;

            double stepped = t - value / slope;

            if (!double.IsFinite(stepped))
                break;

            t = stepped;
        }

        return t;
    }

    /// <summary>
    /// This method checks that a value the solver handed back really is a root, by putting it back
    /// into the polynomial and seeing whether it comes out at nothing.
    /// <para>
    /// **It has to be asked, because the solver will hand back values that are not roots at all.**
    /// The sweep adds each component's polynomial as that component switches on and subtracts it
    /// again as it switches off, and those do not cancel exactly: over a stretch of ray where nothing
    /// is in range, what ought to be the plain constant <c>-threshold</c> comes out as that constant
    /// with dust of about 1e-16 in its top coefficients.  Handed that as a *sextic*, the solver takes
    /// the dust for a leading term and returns six values that are roots of nothing -- the polynomial
    /// stands at -0.3 at them.  Ones that land inside the interval are taken for surface points, and
    /// since no component is in range there, the normal has no gradient to be built from and the
    /// pixel comes out as a dark speck.  A blob showed 159 of them in one frame.
    /// </para>
    /// <para>
    /// The check is against the size of the terms rather than against a fixed number, because the
    /// polynomial's own scale varies enormously along a ray -- and trimming the small coefficients
    /// away instead, which is what <see cref="TubeCurveMath"/> does for its own version of this,
    /// is *not* safe here: a ray grazing along a slab has roots tens of units out, where a
    /// coefficient too small to see beside the others still decides the answer.  Trying it put a
    /// hole in the horizon.
    /// </para>
    /// </summary>
    /// <param name="coefficients">The field polynomial's coefficients, in ascending order.</param>
    /// <param name="t">The value to check.</param>
    /// <returns><c>true</c>, if the polynomial really does vanish there.</returns>
    public static bool IsGenuineRoot(double[] coefficients, double t)
    {
        double value = 0;
        double scale = 0;
        double power = 1;

        foreach (double coefficient in coefficients)
        {
            value += coefficient * power;
            scale += Math.Abs(coefficient * power);
            power *= t;
        }

        // A genuine root leaves the terms cancelling to nothing beside their own sizes; one of these
        // impostors leaves the whole of the constant standing.
        return Math.Abs(value) <= scale * 1e-6;
    }

    /// <summary>
    /// This method finds the real roots of the field polynomial, preferring the exact solver
    /// but never depending on it.
    /// <para>
    /// The exact solver finds a polynomial's roots as the eigenvalues of its companion matrix,
    /// and that iteration is not guaranteed to converge.  It genuinely does fail here: a blob
    /// is a *sextic* since its falloff was cubed, and a sextic is quite capable of carrying two
    /// conjugate root pairs that agree to a dozen digits, which is precisely the near-degenerate
    /// cluster a shifted QR iteration stalls on.  Measured, it happened on two pixels out of
    /// 235,200 -- and it took the whole render down with it, because an exception thrown here is
    /// an exception thrown out of the scanner.
    /// </para>
    /// <para>
    /// So a failure falls back to walking the interval for sign changes and bisecting each one.
    /// That is slower and it is not exact, but it *always terminates*, and it is only ever asked
    /// for the roots inside one bounded interval, which is all this surface ever wanted.  What it
    /// cannot see is a root the polynomial merely touches without crossing, or a pair closer
    /// together than one step of the walk; both are a grazing sliver of surface, and losing one on
    /// the rare ray where the exact solver has already given up is a far better answer than
    /// abandoning the picture.
    /// </para>
    /// </summary>
    /// <param name="coefficients">The field polynomial's coefficients, in ascending order.</param>
    /// <param name="intervalStart">The start of the sub-interval roots are wanted in.</param>
    /// <param name="intervalEnd">The end of the sub-interval roots are wanted in.</param>
    /// <returns>The real roots that were found.</returns>
    public static IEnumerable<double> RealRootsOf(
        double[] coefficients, double intervalStart, double intervalEnd)
    {
        Complex[] roots;

        try
        {
            roots = new Polynomial(coefficients).Roots();
        }
        catch (Exception)
        {
            return RootsByBisection(coefficients, intervalStart, intervalEnd);
        }

        return roots
            .Where(root => root.Imaginary.Near(0))
            .Select(root => root.Real);
    }

    /// <summary>
    /// This method finds roots the way that cannot fail: it walks the interval, and wherever the
    /// polynomial changes sign between one step and the next it has a root bracketed, which it
    /// then closes in on by halving.
    /// </summary>
    /// <param name="coefficients">The field polynomial's coefficients, in ascending order.</param>
    /// <param name="intervalStart">The start of the sub-interval to walk.</param>
    /// <param name="intervalEnd">The end of the sub-interval to walk.</param>
    /// <returns>The roots that were bracketed and closed in on.</returns>
    private static IEnumerable<double> RootsByBisection(
        double[] coefficients, double intervalStart, double intervalEnd)
    {
        // Fine enough to separate the roots of a sextic across one primitive's span, and the cost
        // does not signify when this runs on a ray in a hundred thousand.
        const int Steps = 128;

        // An interval may be unbounded: a plane component whose slab a ray runs along the length of
        // is switched on for the whole of that ray and never off again.  A walk needs somewhere to
        // start and stop, and Cauchy's bound gives one that costs nothing -- every root of a
        // polynomial lies within one plus the largest of the other coefficients over the leading
        // one, so clamping to it cannot pass over a root.
        if (double.IsInfinity(intervalStart) || double.IsInfinity(intervalEnd))
        {
            double bound = BoundOnRootsOf(coefficients);

            intervalStart = Math.Max(intervalStart, -bound);
            intervalEnd = Math.Min(intervalEnd, bound);
        }

        double step = (intervalEnd - intervalStart) / Steps;
        double lowT = intervalStart;
        double low = ValueAt(coefficients, lowT);

        for (int index = 1; index <= Steps; index++)
        {
            double highT = index == Steps ? intervalEnd : intervalStart + step * index;
            double high = ValueAt(coefficients, highT);

            if (low == 0)
                yield return lowT;
            else if (low < 0 != high < 0)
                yield return Bisect(coefficients, lowT, highT, low);

            lowT = highT;
            low = high;
        }

        if (low == 0)
            yield return lowT;
    }

    /// <summary>
    /// This method closes in on a root already known to lie between two points, by repeatedly
    /// halving the gap and keeping whichever half still straddles it.  Fifty-odd halvings take a
    /// bracket down to the last bit a double has, so the loop is capped there rather than trusted
    /// to a tolerance.
    /// </summary>
    /// <param name="coefficients">The field polynomial's coefficients, in ascending order.</param>
    /// <param name="lowT">One side of the bracket.</param>
    /// <param name="highT">The other side of the bracket.</param>
    /// <param name="low">The polynomial's value at <paramref name="lowT"/>.</param>
    /// <returns>The root.</returns>
    private static double Bisect(double[] coefficients, double lowT, double highT, double low)
    {
        const int Halvings = 60;

        for (int index = 0; index < Halvings; index++)
        {
            double middleT = (lowT + highT) / 2;

            if (middleT <= lowT || middleT >= highT)
                break;

            double middle = ValueAt(coefficients, middleT);

            if (middle == 0)
                return middleT;

            if (low < 0 != middle < 0)
                highT = middleT;
            else
                (lowT, low) = (middleT, middle);
        }

        return (lowT + highT) / 2;
    }

    /// <summary>
    /// This method returns a radius that every real root of the polynomial is known to lie within,
    /// by Cauchy's bound: one plus the largest of the trailing coefficients divided by the leading
    /// one.  A leading coefficient of nought would make that meaningless, so the highest one that
    /// is actually there is the one used.
    /// </summary>
    /// <param name="coefficients">The field polynomial's coefficients, in ascending order.</param>
    /// <returns>A radius containing every root.</returns>
    private static double BoundOnRootsOf(double[] coefficients)
    {
        int leading = coefficients.Length - 1;

        while (leading > 0 && coefficients[leading].Near(0))
            leading--;

        if (leading == 0)
            return 1;

        double largest = 0;

        for (int index = 0; index < leading; index++)
            largest = Math.Max(largest, Math.Abs(coefficients[index]));

        return 1 + largest / Math.Abs(coefficients[leading]);
    }

    /// <summary>
    /// This method evaluates the field polynomial at a point, by Horner's method.
    /// </summary>
    /// <param name="coefficients">The field polynomial's coefficients, in ascending order.</param>
    /// <param name="t">The point to evaluate it at.</param>
    /// <returns>The polynomial's value there.</returns>
    private static double ValueAt(double[] coefficients, double t)
    {
        double value = 0;

        for (int index = coefficients.Length - 1; index >= 0; index--)
            value = value * t + coefficients[index];

        return value;
    }

    /// <summary>
    /// A blob's crossings are solved for rather than written down, so a ray leaving one needs to
    /// start further off itself than the usual nudge puts it.  Without this the surface shadows
    /// itself in speckles wherever the light grazes along it -- the underside of a bond, or a slab
    /// laid out by a plane component.  A hundred millionths is still far too small to let light past
    /// anything real.
    /// </summary>
    public override double SelfOffsetScale => 100;

    /// <summary>
    /// This method dresses the blob's material in a pigment that mixes its components' own, when any
    /// of them asked for one.  Whatever the material already wore is kept underneath, and is what
    /// components that named no pigment go on being colored with.
    /// <para>
    /// The check for one already in place is not tidiness: a process that renders twice -- and the
    /// tests do -- would otherwise wrap the wrap, and each layer would mix the mixture again.
    /// </para>
    /// </summary>
    public override void MaterialIsSettled()
    {
        if (Material.Pigment is not BlobPigment &&
            Components.Any(component => component.Pigment is not null))
            Material.Pigment = new BlobPigment(this, Material.Pigment);
    }

    /// <summary>
    /// This method reports how much of the field at a point each component is answerable for, which
    /// is what a per-component pigment mixes its colors in the proportion of.
    /// <para>
    /// It writes into a span the caller owns rather than handing back a list, because this runs on
    /// every shaded point of every ray, and a list per shade is the kind of allocation that has cost
    /// this renderer ten times its speed before now.
    /// </para>
    /// <para>
    /// **Only components that add to the field get a vote.**  A component of negative strength is
    /// there to carve into its neighbours, and it has no color of its own to contribute -- letting
    /// it weigh in would mean mixing colors in negative proportions, and normalizing across a total
    /// that passes through nought on its way from one sign to the other.
    /// </para>
    /// </summary>
    /// <param name="point">The point to weigh the components at, in surface space.</param>
    /// <param name="weights">Where to put the weights, one slot per component, in order.</param>
    /// <returns>The total of the weights, which is nought where nothing is adding to the field.</returns>
    public double GetComponentWeights(Point point, Span<double> weights)
    {
        double total = 0;

        weights.Clear();

        foreach ((int component, IBlobPrimitive primitive) in _primitives)
        {
            (double Density, Vector Gradient)? result = primitive.EvaluateAt(point);

            if (result is null || result.Value.Density <= 0)
                continue;

            weights[component] += result.Value.Density;
            total += result.Value.Density;
        }

        return total;
    }

    /// <summary>
    /// This method returns the normal for the blob at the given point.  It is assumed that
    /// the point will have been transformed to surface-space coordinates.  The vector
    /// returned will also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        Vector gradient = new (0, 0, 0);

        foreach ((_, IBlobPrimitive primitive) in _primitives)
        {
            (double Density, Vector Gradient)? result = primitive.EvaluateAt(point);

            if (result != null)
                gradient += result.Value.Gradient;
        }

        return gradient.Magnitude.Near(0) ? Directions.Up : gradient.Unit;
    }
}
