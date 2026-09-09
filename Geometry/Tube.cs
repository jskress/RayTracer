using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a tube: an analytic, CSG-friendly surface formed by sweeping a
/// variable-radius circular cross-section along a chain of control points, connected by
/// either straight lines or quadratic Bezier curves.  It's the right tool for strings,
/// cables, pipes and wires -- anywhere the exactness (and CSG-compatibility) of an analytic
/// surface matters more than the flexibility of an arbitrary swept profile.
/// </summary>
public class Tube : Surface
{
    /// <summary>
    /// This property holds the point the tube starts at.
    /// </summary>
    public TubeControlPoint Start { get; set; }

    /// <summary>
    /// This property holds the ordered list of segments -- each either a straight line or a
    /// quadratic curve -- that carry the tube from its start point to its end.
    /// </summary>
    public List<TubeSegmentSpec> Segments { get; } = [];

    /// <summary>
    /// This property suppresses <see cref="SegmentContinuity"/>'s tangent-continuity check
    /// for this tube, for the (uncommon) case where a sharp kink is actually wanted.
    /// </summary>
    public bool Discontinuous { get; set; }

    /// <summary>
    /// This property exposes the root of the CSG tree built from our segments, once
    /// prepared, so that things like <see cref="SurfaceIterator"/> can walk into it (e.g.,
    /// to propagate our material down to the segments that actually get hit).
    /// </summary>
    public Surface Root => _root;

    private readonly List<Surface> _segments = [];
    private Surface _root;

    /// <summary>
    /// This method is called once prior to rendering.  It builds one <see cref="TubeSegment"/>
    /// (straight) or <see cref="TubeQuadSegment"/> (curved) per entry in <see cref="Segments"/>,
    /// and chains them together with nested CSG unions -- reusing <see cref="CsgSurface"/>'s
    /// already-proven inside/outside logic rather than reinventing interval-merging for what
    /// is, geometrically, just a union of solids that happen to meet exactly at shared
    /// control points.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        SegmentContinuity.Validate(
            Start.Center,
            Segments.Select(spec => (spec.Control1?.Center, spec.Control2?.Center, spec.End.Center)),
            "tube", !Discontinuous);

        _segments.Clear();

        TubeControlPoint current = Start;

        foreach (TubeSegmentSpec spec in Segments)
        {
            // A curved segment whose control points leave it geometrically straight is built as the
            // straight segment it actually is.  This is not tidiness: the envelope solve for a curved
            // segment works from the differences of its control points, and when those collapse the
            // polynomial it samples drops degree and the solve finds nothing -- so the segment
            // renders as its two end spheres and no body at all, with no error to say so.  A bend
            // that works out to nought is a perfectly reasonable thing for a scene to compute, and it
            // should draw as a straight piece rather than disappear.
            bool straight = (spec.Control1, spec.Control2) switch
            {
                (null, _) => false,
                (not null, null) => IsStraight(
                    current.Center, spec.Control1.Center, spec.End.Center),
                (not null, not null) => IsStraight(
                    current.Center, spec.Control1.Center, spec.Control2.Center, spec.End.Center)
            };

            Surface segment = (spec.Control1, spec.Control2) switch
            {
                _ when straight => new TubeSegment
                {
                    Start = current.Center, StartRadius = current.Radius,
                    End = spec.End.Center, EndRadius = spec.End.Radius
                },
                (null, _) => new TubeSegment
                {
                    Start = current.Center, StartRadius = current.Radius,
                    End = spec.End.Center, EndRadius = spec.End.Radius
                },
                (not null, null) => new TubeQuadSegment
                {
                    Start = current.Center, StartRadius = current.Radius,
                    Control = spec.Control1.Center, ControlRadius = spec.Control1.Radius,
                    End = spec.End.Center, EndRadius = spec.End.Radius
                },
                (not null, not null) => new TubeCubicSegment
                {
                    Start = current.Center, StartRadius = current.Radius,
                    Control1 = spec.Control1.Center, Control1Radius = spec.Control1.Radius,
                    Control2 = spec.Control2.Center, Control2Radius = spec.Control2.Radius,
                    End = spec.End.Center, EndRadius = spec.End.Radius
                }
            };

            _segments.Add(segment);
            current = spec.End;
        }

        _root = null;

        foreach (Surface segment in _segments)
        {
            _root = _root is null
                ? segment
                : new CsgSurface { Operation = CsgOperation.Union, Left = _root, Right = segment };
        }

        if (_root is not null)
        {
            _root.Parent = this;

            if (Material is not null)
            {
                foreach (Surface segment in _segments)
                    segment.Material ??= Material;
            }

            _root.PrepareForRendering(SampleTimes);
        }
    }

    /// <summary>
    /// This method reports whether a quadratic segment is really a straight line.
    /// <para>
    /// A quadratic Bezier bends by its second difference, <c>start - 2 * control + end</c>.  Where
    /// that is nought the control point sits exactly halfway between the ends, every quadratic term
    /// drops out, and what is left is a line.  The test is relative to the segment's own length so
    /// that it means the same thing for a twig and for a tree trunk.
    /// </para>
    /// </summary>
    private static bool IsStraight(Point start, Point control, Point end)
    {
        Vector span = end - start;
        Vector bend = (start - control) - (control - end);

        return bend.Magnitude <= span.Magnitude * StraightTolerance;
    }

    /// <summary>
    /// This method reports whether a cubic segment is really a straight line, which it is when both
    /// of its control points lie on the line from its start to its end.
    /// <para>
    /// **The question is where the control points *are*, not what the polynomial's coefficients come
    /// to.**  This asked the latter once -- whether the second and third differences had collapsed --
    /// and those vanish only when the controls are collinear *and evenly spaced*.  Put them at 0.33
    /// and 0.67 of a straight run instead of at exact thirds and the curve is still a perfectly
    /// straight line, but its coefficients are not nought, so it took the curved road and vanished:
    /// two end spheres and no body.  Nothing said so, and a scene that computes its control points
    /// rather than typing them will land on those numbers every time.
    /// </para>
    /// </summary>
    private static bool IsStraight(Point start, Point control1, Point control2, Point end)
    {
        Vector span = end - start;
        double length = span.Magnitude;

        if (length == 0)
            return true;

        Vector along = span / length;
        double limit = length * StraightTolerance;

        return OffTheLine(start, along, control1) <= limit &&
               OffTheLine(start, along, control2) <= limit;
    }

    /// <summary>
    /// This method returns how far a control point sits off the line a segment spans, measured square
    /// to that line so that sliding a control point *along* it counts for nothing.
    /// </summary>
    private static double OffTheLine(Point start, Vector along, Point control)
    {
        Vector offset = control - start;

        return (offset - along * offset.Dot(along)).Magnitude;
    }

    /// <summary>
    /// How far a control point may sit off the straight line, as a fraction of the segment's own
    /// length, and still count as straight.  It is small: this is meant to catch a bend that has
    /// genuinely collapsed, not to flatten a shallow one.
    /// </summary>
    private const double StraightTolerance = 1e-9;

    /// <summary>
    /// This method returns a default bounding box that encloses every segment.
    /// </summary>
    /// <returns>A bounding box enclosing the tube, or <c>null</c>, if it has no segments.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        if (_segments.Count == 0)
            return null;

        BoundingBox box = new BoundingBox();

        foreach (Surface segment in _segments)
            box.Add(segment.BoundingBox);

        return box;
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the tube and, if
    /// so, where.  The actual work is delegated to the CSG union of the tube's segments.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        _root?.Intersect(ray, intersections);
    }

    /// <summary>
    /// This method is never actually invoked: every intersection produced by
    /// <see cref="AddIntersections"/> belongs to one of our child segments, and that child's
    /// own normal is used instead.  It's here only to satisfy the base class.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        return Directions.Up;
    }
}
