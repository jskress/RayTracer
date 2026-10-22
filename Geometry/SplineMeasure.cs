using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class measures a spline by distance, so that a thing can be put a given way along it: half way along is
/// half its length, however its curves were drawn.  Each curve is walked in small steps and the length so far kept
/// at each, and a distance is found among them and then on its curve exactly.  A spline that ends where it began
/// is a loop, and a distance past its end begins round it again; any other holds at its ends.
/// </summary>
public sealed class SplineMeasure
{
    // How many steps each curve is walked in.  The length between two steps is taken as straight, which a curve
    // bending through a right angle in one segment is to within a part in fifty thousand.
    private const int Steps = 64;

    private readonly List<ISplineCurve> _curves;
    private readonly double[] _sofar;

    /// <summary>
    /// This property holds the spline's length.
    /// </summary>
    public double Length { get; }

    /// <summary>
    /// This property notes whether the spline ends where it began, and so is a loop.
    /// </summary>
    public bool Loops { get; }

    public SplineMeasure(Spline spline)
    {
        _curves = spline.GetCurves();

        if (_curves.Count == 0)
            throw new InvalidOperationException("A spline must run somewhere to be followed: it has no segments.");

        _sofar = new double[_curves.Count * Steps + 1];

        Point last = _curves[0].GetPoint(0);

        for (int curve = 0; curve < _curves.Count; curve++)
        {
            for (int step = 1; step <= Steps; step++)
            {
                Point here = _curves[curve].GetPoint((double) step / Steps);
                int at = curve * Steps + step;

                _sofar[at] = _sofar[at - 1] + (here - last).Magnitude;
                last = here;
            }
        }

        Length = _sofar[^1];

        if (Length <= 0)
            throw new InvalidOperationException("A spline must run somewhere to be followed: it has no length.");

        Loops = (_curves[^1].GetPoint(1) - _curves[0].GetPoint(0)).Magnitude <= Length * 1e-9;
    }

    /// <summary>
    /// This method returns the distance a distance stands for along the spline: itself, round again for a loop,
    /// or held at the nearer end for any other.
    /// </summary>
    public double Settled(double distance) => Loops
        ? distance - Length * Math.Floor(distance / Length)
        : Math.Clamp(distance, 0, Length);

    // The curve a distance along falls on, and that curve's own parameter there.
    private (ISplineCurve Curve, double U) Locate(double distance)
    {
        double along = Settled(distance);
        int low = 0, high = _sofar.Length - 1;

        while (high - low > 1)
        {
            int middle = (low + high) / 2;

            if (_sofar[middle] <= along)
                low = middle;
            else
                high = middle;
        }

        double span = _sofar[high] - _sofar[low];
        double share = span > 0 ? (along - _sofar[low]) / span : 0;
        int curve = Math.Min(low / Steps, _curves.Count - 1);
        double step = low - curve * Steps + share;

        return (_curves[curve], Math.Clamp(step / Steps, 0, 1));
    }

    /// <summary>
    /// This method returns the place a distance along the spline.
    /// </summary>
    public Point PointAt(double distance)
    {
        (ISplineCurve curve, double u) = Locate(distance);

        return curve.GetPoint(u);
    }

    /// <summary>
    /// This method returns the way the spline runs a distance along it, of unit length.  Where a curve's own
    /// tangent comes to nothing -- a control point on its end -- the way to the place a little further on is used.
    /// </summary>
    public Vector HeadingAt(double distance)
    {
        (ISplineCurve curve, double u) = Locate(distance);
        Vector tangent = curve.GetTangent(u);

        if (tangent.Magnitude > Length * 1e-12)
            return tangent.Unit;

        double step = Length * 1e-4;
        Vector chord = PointAt(Settled(distance) + step) - PointAt(Settled(distance) - step);

        return chord.Magnitude > 0 ? chord.Unit : new Vector(1, 0, 0);
    }

    /// <summary>
    /// This method returns how the spline's heading turns a distance along it, per unit of distance: its curvature
    /// times the unit normal toward the middle of its bend, nothing where it runs straight.
    /// </summary>
    public Vector BendAt(double distance)
    {
        double step = Length * 1e-4;
        double from = Loops ? distance - step : Math.Max(0, Settled(distance) - step);
        double to = Loops ? distance + step : Math.Min(Length, Settled(distance) + step);

        return to > from ? (HeadingAt(to) - HeadingAt(from)) / (to - from) : new Vector(0, 0, 0);
    }
}
