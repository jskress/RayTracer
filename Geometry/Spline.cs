using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a spline: an ordered chain of control points connected by straight
/// lines, quadratic Bezier curves or cubic Bezier curves through 3D space.  Unlike
/// <see cref="Tube"/>'s own control-point chain, a spline carries no radius -- it's a bare
/// curve, meant to be evaluated (point and tangent) by whatever consumes it, such as a
/// future sweep surface's path through space.
/// </summary>
public class Spline
{
    /// <summary>
    /// This property holds the point the spline starts at.
    /// </summary>
    public Point Start { get; set; }

    /// <summary>
    /// This property holds the ordered list of segments -- each either a straight line, a
    /// quadratic curve or a cubic curve -- that carry the spline from its start point to its
    /// end.
    /// </summary>
    public List<SplineSegmentSpec> Segments { get; } = [];

    /// <summary>
    /// This property suppresses <see cref="SegmentContinuity"/>'s tangent-continuity check
    /// for this spline, for the (uncommon) case where a sharp kink is actually wanted.
    /// </summary>
    public bool Discontinuous { get; set; }

    /// <summary>
    /// This method builds the concrete, evaluable curve for each of our segments, threading
    /// each one's start through from the previous segment's end (or our own start, for the
    /// first one).
    /// </summary>
    /// <returns>The ordered list of curves that make up this spline.</returns>
    public List<ISplineCurve> GetCurves()
    {
        List<ISplineCurve> curves = [];
        Point current = Start;

        foreach (SplineSegmentSpec spec in Segments)
        {
            ISplineCurve curve = (spec.Control1, spec.Control2) switch
            {
                (null, _) => new SplineLine { Start = current, End = spec.End },
                (not null, null) => new SplineQuadCurve
                {
                    Start = current, Control = spec.Control1, End = spec.End
                },
                (not null, not null) => new SplineCubicCurve
                {
                    Start = current, Control1 = spec.Control1, Control2 = spec.Control2,
                    End = spec.End
                }
            };

            curves.Add(curve);
            current = spec.End;
        }

        return curves;
    }
}
