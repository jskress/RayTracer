using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a single strand: a fiber of a given width running smoothly through a series of
/// points -- a whisker, an antenna, a hair, a thread -- or along a spline, as a wire hangs.
/// <para>
/// It is the one-strand case of what <c>hair</c> grows by the thousand, and is hit and shaded the same
/// way: as a flat ribbon that always faces the ray, shaded as though it were round.  So unlike a
/// <see cref="Ribbon"/>, which keeps one face and can be seen edge-on, a strand looks the same from every
/// side; and unlike a <see cref="Tube"/>, which really is round, it costs no more than a sheet.
/// </para>
/// </summary>
public class Strand : StrandSurface
{
    /// <summary>
    /// This property holds the points the strand runs through, root first, each with its width there.
    /// </summary>
    public List<RibbonControlPoint> Points { get; } = [];

    /// <summary>
    /// This property holds the spline the strand runs along, in place of points, or <c>null</c>.
    /// </summary>
    public Spline Spline { get; set; }

    /// <summary>
    /// This property holds how wide a strand along a spline is, all along it.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// This method is called once prior to rendering to lay the strand through its points.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        StrandSet strands = new ();

        if (Spline is not null)
        {
            if (Points.Count > 0)
                throw new Exception("A strand runs through its points or along a spline, not both.");

            if (Width <= 0)
                throw new Exception("A strand's width must be more than nought.");

            strands.Add(PiecesOf(Spline), Width);
        }
        else
        {
            if (Points.Count < 2)
                throw new Exception("A strand needs at least two points to run between.");

            strands.Add(
                Points.Select(point => point.Position).ToList(),
                Points.Select(point => point.Width).ToList());
        }

        strands.Seal();

        Strands = strands;
    }

    /// <summary>
    /// This method returns a spline's segments as cubic Bézier pieces, lines and quadratics raised to cubics, so the
    /// strand runs along the very curve the spline is.
    /// </summary>
    private static List<(Point Start, Point First, Point Second, Point End)> PiecesOf(Spline spline)
    {
        List<(Point Start, Point First, Point Second, Point End)> pieces = new (spline.Segments.Count);
        Point current = spline.Start;

        foreach (SplineSegmentSpec segment in spline.Segments)
        {
            Point end = segment.End;
            (Point first, Point second) = (segment.Control1, segment.Control2) switch
            {
                (null, _) => (current + (end - current) / 3, end - (end - current) / 3),
                (not null, null) => (
                    current + (segment.Control1 - current) * (2.0 / 3),
                    end + (segment.Control1 - end) * (2.0 / 3)),
                _ => (segment.Control1, segment.Control2)
            };

            pieces.Add((current, first, second, end));
            current = end;
        }

        return pieces;
    }
}
