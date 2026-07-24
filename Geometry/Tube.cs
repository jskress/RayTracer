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
        if (!Discontinuous)
        {
            SegmentContinuity.Validate(
                Start.Center,
                Segments.Select(spec => (spec.Control1?.Center, spec.Control2?.Center, spec.End.Center)),
                "tube");
        }

        _segments.Clear();

        TubeControlPoint current = Start;

        foreach (TubeSegmentSpec spec in Segments)
        {
            Surface segment = (spec.Control1, spec.Control2) switch
            {
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
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        return Directions.Up;
    }
}
