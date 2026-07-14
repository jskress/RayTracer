using Complex = System.Numerics.Complex;
using MathNet.Numerics;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

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

    private readonly List<IBlobPrimitive> _primitives = [];

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _primitives.Clear();
        _primitives.AddRange(Components.SelectMany(component => component.GetPrimitives()));
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the blob and, if
    /// so, where.  Each primitive contributes a quartic polynomial (in the ray's distance
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

        foreach (IBlobPrimitive primitive in _primitives)
        {
            (double Enter, double Exit)? interval = primitive.GetBoundingInterval(ray);

            if (interval == null)
                continue;

            (double t0, double t1, double t2) = primitive.GetDistanceSquaredCoefficients(ray);
            (double c0, double c1, double c2) = BlobFieldMath.GetDensityCoefficients(
                primitive.Strength, primitive.RadiusSquared);
            double[] polynomial = BlobFieldMath.GetFieldPolynomial(t0, t1, t2, c0, c1, c2);

            active.Add((interval.Value.Enter, interval.Value.Exit, polynomial));
        }

        if (active.Count == 0)
            return;

        List<(double T, bool Entering, double[] Polynomial)> events = active
            .SelectMany(a => new[] { (a.Enter, true, a.Polynomial), (a.Exit, false, a.Polynomial) })
            .OrderBy(e => e.Item1)
            .ToList();

        double[] coefficients = [-Threshold, 0, 0, 0, 0];

        for (int index = 0; index < events.Count; index++)
        {
            (double t, bool entering, double[] polynomial) = events[index];

            for (int i = 0; i < 5; i++)
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

        Polynomial polynomial = new (coefficients);

        foreach (Complex root in polynomial.Roots())
        {
            if (!root.Imaginary.Near(0))
                continue;

            double t = root.Real;

            if (t >= intervalStart - DoubleExtensions.Epsilon && t <= intervalEnd + DoubleExtensions.Epsilon)
                intersections.Add(new Intersection(this, t));
        }
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

        foreach (IBlobPrimitive primitive in _primitives)
        {
            (double Density, Vector Gradient)? result = primitive.EvaluateAt(point);

            if (result != null)
                gradient += result.Value.Gradient;
        }

        return gradient.Magnitude.Near(0) ? Directions.Up : gradient.Unit;
    }
}
