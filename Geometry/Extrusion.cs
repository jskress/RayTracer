using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents an extrusion.  It is defined by one or more closed paths in 2D
/// that are extruded along the Y axis.
/// </summary>
public class Extrusion : ExtrudedSurface
{
    /// <summary>
    /// This attribute holds the path that represents the outline of the extrusion in the
    /// X/Z plane.
    /// </summary>
    public GeneralPath Path { get; set; }

    /// <summary>
    /// This attribute holds how much the outline is scaled by the time the extrusion reaches its
    /// top.  It is 1 for an ordinary extrusion, whose walls run straight up; less than 1 draws the
    /// outline in as it rises, and 0 brings it to a point.  Greater than 1 spreads it.
    /// <para>
    /// The scaling is about the Y axis, so where the outline sits in relation to the origin decides
    /// what the taper leans towards.  An outline drawn around the origin narrows into itself; one
    /// drawn off to the side leans over as it narrows.
    /// </para>
    /// </summary>
    public double Taper { get; set; } = 1;

    private readonly List<ExtrusionPathSurface> _surfaces = [];
    private readonly List<TaperedExtrusionPathSurface> _taperedSurfaces = [];

    private Parallelogram _top;
    private Parallelogram _bottom;
    private double _taperRate;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (Taper < 0)
            throw new Exception("An extrusion's taper must not be negative.");

        // The rate is how much of the outline's own size is gained or lost per unit of height.  It
        // is nought for an ordinary extrusion, and that is what the walls are chosen by: a taper of
        // exactly 1 goes down the untapered path, which is the same arithmetic this shape has always
        // done and so renders the same to the last bit.
        _taperRate = (Taper - 1) / (MaximumY - MinimumY);

        _surfaces.Clear();
        _taperedSurfaces.Clear();

        if (IsTapered)
        {
            _taperedSurfaces.AddRange(Path.NormalizeFor3D().Segments
                .Select(segment => new TaperedExtrusionPathSurface(
                    segment, MinimumY, MaximumY, _taperRate)));
        }
        else
        {
            _surfaces.AddRange(Path.NormalizeFor3D().Segments
                .Select(segment => new ExtrusionPathSurface(segment, MinimumY, MaximumY)));
        }

        _top = null;
        _bottom = null;

        if (Closed)
        {
            // A taper of nought brings the top to a single point, and a cap over a point is no cap
            // at all, so it is left off rather than built with no area for a ray to find.
            if (Taper > 0)
                _top = CapAt(MaximumY, Taper);

            _bottom = CapAt(MinimumY, 1);
        }
    }

    /// <summary>
    /// This property notes whether this extrusion's walls lean rather than run straight up.
    /// </summary>
    private bool IsTapered => _taperRate != 0;

    /// <summary>
    /// This method builds the cap that closes the extrusion at the given height, sized to what the
    /// outline has been scaled to by the time it gets there.
    /// </summary>
    /// <param name="y">The height to put the cap at.</param>
    /// <param name="scale">What the outline is scaled by there.</param>
    /// <returns>The parallelogram the cap lies in.</returns>
    private Parallelogram CapAt(double y, double scale)
    {
        return new Parallelogram
        {
            Point = new Point(Path.MinX * scale, y, Path.MinY * scale),
            Side1 = new Vector((Path.MaxX - Path.MinX) * scale, 0, 0),
            Side2 = new Vector(0, 0, (Path.MaxY - Path.MinY) * scale)
        };
    }

    /// <summary>
    /// This method is used to produce a default bounding box for this shape.
    /// </summary>
    /// <returns>A default bounding box, if any, for the surface.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        // The cross-section is the outline scaled by something between 1 and the taper, so the box
        // has to hold the outline at both.  Adding the two corners at each scale is enough for
        // that: scaling by a number that is never negative leaves the smaller corner the smaller
        // one, so no corner can overtake another and be missed.  Untapered, both scales are 1 and
        // the same two corners are simply added twice.
        return new BoundingBox()
            .Add(new Point(Path.MinX, MinimumY, Path.MinY))
            .Add(new Point(Path.MaxX, MaximumY, Path.MaxY))
            .Add(new Point(Path.MinX * Taper, MinimumY, Path.MinY * Taper))
            .Add(new Point(Path.MaxX * Taper, MaximumY, Path.MaxY * Taper));
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        if (Closed)
        {
            AddCapIntersections(ray, intersections, _top, Taper);
            AddCapIntersections(ray, intersections, _bottom, 1);
        }

        if (IsTapered)
        {
            AddTaperedIntersections(ray, intersections);

            return;
        }

        TwoDRay projectedRay = TwoDRay.ProjectedToXz(ray);

        intersections.AddRange(_surfaces
            .Select(surface => surface.GetTwoDIntersections(ray, projectedRay))
            .SelectMany(intersectionData => intersectionData)
            .Where(intersection => intersection != null)
            .Select(intersection => intersection.FromXz(this)));
    }

    /// <summary>
    /// This method adds the crossings the ray makes with the leaning walls of a tapered extrusion.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    private void AddTaperedIntersections(Ray ray, List<Intersection> intersections)
    {
        TaperedProjection projection = new TaperedProjection(ray, MinimumY, _taperRate);

        // A ray that projects to a point rather than a line is one running along the taper's own
        // axis, straight at the apex.  It meets no wall at all; it comes and goes through the caps.
        if (projection.Line is null)
            return;

        foreach (TaperedExtrusionPathSurface surface in _taperedSurfaces)
            surface.AddIntersections(this, ray, projection, intersections);
    }

    /// <summary>
    /// This method is used to add any intersections our ray has with the cap at the given
    /// height.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add intersections to.</param>
    /// <param name="cap">The parallelogram we are suing for a cap.</param>
    private void AddCapIntersections(
        Ray ray, List<Intersection> intersections, Parallelogram cap, double scale)
    {
        if (cap is null)
            return;

        double intersection = cap.GetIntersection(ray);

        if (!double.IsNaN(intersection))
        {
            Point point = ray.At(intersection);
            TwoDPoint twoDPoint = TwoDPoint.ProjectedToXz(point);

            // The outline is asked about at its own size, so a cap that has been scaled has to
            // take the hit back down to that size before asking.  An untapered cap is left alone
            // rather than divided by one, which saves making a point to throw away.
            if (scale != 1)
                twoDPoint = new TwoDPoint(twoDPoint.X / scale, twoDPoint.Y / scale);

            if (Path.Contains(twoDPoint))
                intersections.Add(new Intersection(this, intersection));
        }
    }

    /// <summary>
    /// This method returns the normal for the extrusion.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        return intersection is PrecomputedNormalIntersection precomputed
            ? precomputed.PrecomputedNormal
            : point.Y < MinimumY + (MaximumY - MinimumY) / 2
                ? Directions.Down
                : Directions.Up;
    }

}
