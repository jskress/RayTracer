namespace RayTracer.Graphics;

/// <summary>
/// This class holds a path flattened into straight edges, so that the two questions a decal asks
/// of its outline at every point it paints are cheap to answer: is this point inside, and how much
/// of the patch around it is.
/// <para>
/// A <see cref="GeneralPath"/> answers the first exactly, curve by curve, and that is the right
/// thing for a surface built from one -- an extrusion's walls are the curves themselves.  A decal
/// needs the second answer as well, and the distance to a cubic has no closed form, so the curves
/// are cut into lines once, when the decal is made, finely enough that the difference is far below
/// anything a pixel can show: a ten-thousandth of the outline's own size.
/// </para>
/// </summary>
public class FlattenedPath
{
    /// <summary>
    /// How far the lines may stray from the curves they stand for, as a fraction of the larger
    /// side of the outline's box.
    /// </summary>
    private const double Tolerance = 1e-4;

    /// <summary>
    /// This property holds the smallest X any edge reaches.
    /// </summary>
    public double MinX { get; } = double.MaxValue;

    /// <summary>
    /// This property holds the smallest Y any edge reaches.
    /// </summary>
    public double MinY { get; } = double.MaxValue;

    /// <summary>
    /// This property holds the largest X any edge reaches.
    /// </summary>
    public double MaxX { get; } = double.MinValue;

    /// <summary>
    /// This property holds the largest Y any edge reaches.
    /// </summary>
    public double MaxY { get; } = double.MinValue;

    /// <summary>
    /// This property reports how many straight edges the outline was cut into.
    /// </summary>
    public int EdgeCount => _edges.Length / 4;

    // Each edge as four numbers in a row -- start X and Y, then end X and Y -- since every question
    // asked of the outline walks all of them, and a flat array is the cheapest thing there is to walk.
    private readonly double[] _edges;

    /// <summary>
    /// This constructor flattens the given path.
    /// </summary>
    /// <param name="path">The path to flatten.</param>
    public FlattenedPath(GeneralPath path)
    {
        List<TwoDPoint[]> segments = path.Segments.Select(segment => segment.Points).ToList();

        foreach (TwoDPoint point in segments.SelectMany(points => points))
        {
            MinX = Math.Min(MinX, point.X);
            MinY = Math.Min(MinY, point.Y);
            MaxX = Math.Max(MaxX, point.X);
            MaxY = Math.Max(MaxY, point.Y);
        }

        double tolerance = Tolerance * Math.Max(MaxX - MinX, MaxY - MinY);
        List<double> edges = [];

        for (int index = 0; index < segments.Count; index++)
        {
            IPathSegment segment = path.Segments[index];
            TwoDPoint[] points = segments[index];
            int pieces = PiecesFor(points, tolerance);
            TwoDPoint from = points[0];

            for (int piece = 1; piece <= pieces; piece++)
            {
                TwoDPoint to = piece == pieces ? points[^1] : segment.GetPoint(piece / (double) pieces);

                edges.AddRange([from.X, from.Y, to.X, to.Y]);

                from = to;
            }
        }

        _edges = edges.ToArray();
    }

    /// <summary>
    /// This method works out how many straight pieces a segment must be cut into to keep within the
    /// tolerance.
    /// <para>
    /// A chord across a stretch <c>h</c> of a curve's parameter strays from it by at most an eighth
    /// of <c>h²</c> times the largest second derivative there.  For a quadratic that derivative is
    /// fixed, twice the second difference of its control points; for a cubic it is at most six times
    /// the larger of its two.  Solving for the number of equal stretches gives what is here.
    /// </para>
    /// </summary>
    /// <param name="points">The segment's defining points, ends included.</param>
    /// <param name="tolerance">How far a piece may stray from the curve.</param>
    /// <returns>The number of pieces to cut the segment into.</returns>
    internal static int PiecesFor(TwoDPoint[] points, double tolerance)
    {
        if (points.Length < 3)
            return 1;

        double bend = 0;

        for (int index = 0; index + 2 < points.Length; index++)
        {
            double x = points[index].X - 2 * points[index + 1].X + points[index + 2].X;
            double y = points[index].Y - 2 * points[index + 1].Y + points[index + 2].Y;

            bend = Math.Max(bend, Math.Sqrt(x * x + y * y));
        }

        if (bend <= 0 || tolerance <= 0)
            return 1;

        double factor = points.Length == 3 ? 0.25 : 0.75;
        int pieces = (int) Math.Ceiling(Math.Sqrt(factor * bend / tolerance));

        return Math.Clamp(pieces, 1, 1024);
    }

    /// <summary>
    /// This method returns how much of a patch of the plane the outline covers, between 0 and 1.
    /// <para>
    /// The patch is a parallelogram centered on the point, with the two given edges -- the footprint
    /// of a pixel, laid into the outline's plane.  Over a straight stretch of outline the share of
    /// such a patch inside it climbs in a straight line across the patch's width, from none where the
    /// patch only touches the outline to all where it only just stops touching, so that is what is
    /// worked out: the distance from the point to the nearest edge, against the patch's width
    /// measured in the direction of that edge.  That is what keeps a decal's edge crisp and clean at
    /// any distance without asking the antialiasing to find it.
    /// </para>
    /// <para>
    /// A patch with no size asks only whether the point is inside, as an even-odd count of the edges
    /// to its right, the same rule <see cref="GeneralPath.Contains(TwoDPoint)"/> follows.
    /// </para>
    /// </summary>
    /// <param name="x">The point's X coordinate.</param>
    /// <param name="y">The point's Y coordinate.</param>
    /// <param name="acrossX">The X extent of one edge of the patch.</param>
    /// <param name="acrossY">The Y extent of that edge.</param>
    /// <param name="alongX">The X extent of the other edge of the patch.</param>
    /// <param name="alongY">The Y extent of that edge.</param>
    /// <returns>The fraction of the patch the outline covers.</returns>
    public double CoverageAt(
        double x, double y, double acrossX = 0, double acrossY = 0, double alongX = 0, double alongY = 0)
    {
        double halfX = (Math.Abs(acrossX) + Math.Abs(alongX)) / 2;
        double halfY = (Math.Abs(acrossY) + Math.Abs(alongY)) / 2;

        // Nearly every point a decal is asked about lies nowhere near it, so the box is asked first.
        if (x < MinX - halfX || x > MaxX + halfX || y < MinY - halfY || y > MaxY + halfY)
            return 0;

        bool sized = halfX > 0 || halfY > 0;
        bool inside = false;
        double nearest = double.MaxValue;
        double towardX = 0;
        double towardY = 0;

        for (int index = 0; index < _edges.Length; index += 4)
        {
            double x0 = _edges[index];
            double y0 = _edges[index + 1];
            double x1 = _edges[index + 2];
            double y1 = _edges[index + 3];

            // An edge counts if it crosses the point's level to the right of it.  Taking one end as
            // above-or-level and the other as below is what keeps a vertex sitting exactly level with
            // the point from being counted by both edges that meet there.
            if (y0 > y != y1 > y && x0 + (y - y0) * (x1 - x0) / (y1 - y0) > x)
                inside = !inside;

            if (!sized)
                continue;

            double dx = x1 - x0;
            double dy = y1 - y0;
            double length = dx * dx + dy * dy;
            double t = length > 0 ? Math.Clamp(((x - x0) * dx + (y - y0) * dy) / length, 0, 1) : 0;
            double ox = x0 + t * dx - x;
            double oy = y0 + t * dy - y;
            double distance = ox * ox + oy * oy;

            if (distance < nearest)
            {
                nearest = distance;
                towardX = ox;
                towardY = oy;
            }
        }

        if (!sized || nearest == double.MaxValue)
            return inside ? 1 : 0;

        nearest = Math.Sqrt(nearest);

        if (nearest == 0)
            return 0.5;

        double nx = towardX / nearest;
        double ny = towardY / nearest;
        double width = Math.Abs(acrossX * nx + acrossY * ny) + Math.Abs(alongX * nx + alongY * ny);

        if (width <= 0)
            return inside ? 1 : 0;

        return Math.Clamp(0.5 + (inside ? nearest : -nearest) / width, 0, 1);
    }
}
