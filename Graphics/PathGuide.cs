using RayTracer.Basics;

namespace RayTracer.Graphics;

/// <summary>
/// This class lets a path be walked by distance: given how far along it, where that is and which way
/// the path runs there.  It is what lays a run of text along a path.
/// <para>
/// A path's curves are drawn by a parameter that does not run evenly -- a cubic's middle half may
/// cover far more ground than either quarter at its ends -- so the path is cut into short straight
/// pieces once, finely enough to stay a ten-thousandth of its size from the curves, and the distance
/// along it is taken from those.  Where on a piece a distance falls is then carried back to the curve
/// itself, so points land on the curve and not on the chord, and the way it runs there is the
/// curve's own rather than a piece's, which would turn each letter by a little jolt as it crossed
/// from one piece to the next.
/// </para>
/// <para>
/// The runs of a path are followed one after another, in the order they were drawn; the gap from
/// the end of one to the start of the next counts for nothing.
/// </para>
/// </summary>
public class PathGuide
{
    /// <summary>
    /// How far the pieces may stray from the curves, as a fraction of the larger side of the path's
    /// box.
    /// </summary>
    private const double Tolerance = 1e-4;

    /// <summary>
    /// This property holds how long the path is.
    /// </summary>
    public double Length { get; }

    private readonly List<Piece> _pieces = [];

    /// <summary>
    /// This constructor measures the given path.
    /// </summary>
    /// <param name="path">The path to walk.</param>
    public PathGuide(GeneralPath path)
    {
        List<TwoDPoint> corners = path.Segments.SelectMany(segment => segment.Points).ToList();

        if (corners.Count == 0)
            throw new Exception("A path to lay text along has to have something in it.");

        double size = Math.Max(
            corners.Max(point => point.X) - corners.Min(point => point.X),
            corners.Max(point => point.Y) - corners.Min(point => point.Y));
        double travelled = 0;

        foreach (IPathSegment segment in path.Segments)
        {
            TwoDPoint[] points = segment.Points;
            int pieces = FlattenedPath.PiecesFor(points, Tolerance * size);
            TwoDPoint from = points[0];

            for (int piece = 1; piece <= pieces; piece++)
            {
                double t = piece / (double) pieces;
                TwoDPoint to = piece == pieces ? points[^1] : segment.GetPoint(t);
                double length = (to - from).Magnitude;

                if (length > 0)
                {
                    _pieces.Add(new Piece(segment, (piece - 1) / (double) pieces, t, travelled, length));

                    travelled += length;
                }

                from = to;
            }
        }

        if (travelled <= 0)
            throw new Exception("A path to lay text along has to have some length to it.");

        Length = travelled;
    }

    /// <summary>
    /// This method returns where on the path a distance along it falls, and which way the path runs
    /// there.  A distance before its start or past its end carries straight on from that end, so
    /// text that runs over the end of its path is not lost.
    /// </summary>
    /// <param name="distance">How far along the path.</param>
    /// <returns>The point there, and the unit direction the path runs in there.</returns>
    public (TwoDPoint Point, TwoDVector Direction) At(double distance)
    {
        if (distance <= 0)
        {
            (TwoDPoint start, TwoDVector way) = OnCurve(_pieces[0], 0);

            return (start + way * distance, way);
        }

        if (distance >= Length)
        {
            (TwoDPoint end, TwoDVector way) = OnCurve(_pieces[^1], 1);

            return (end + way * (distance - Length), way);
        }

        int low = 0;
        int high = _pieces.Count - 1;

        while (low < high)
        {
            int middle = (low + high + 1) / 2;

            if (_pieces[middle].Start <= distance)
                low = middle;
            else
                high = middle - 1;
        }

        Piece piece = _pieces[low];

        return OnCurve(piece, (distance - piece.Start) / piece.Length);
    }

    /// <summary>
    /// This method returns the transform that lays something drawn along a straight line onto the
    /// path: the point of it at the given place along that line lands on the path at the given
    /// distance, its line turns to run with the path there, and what was above the line stands on
    /// the path's left.  It is a turn and a move and nothing else, so what it lays keeps its shape.
    /// </summary>
    /// <param name="distance">How far along the path to lay it.</param>
    /// <param name="at">Where along its own line the point that lands there is.</param>
    /// <returns>The transform, for points written as (x, y, 0).</returns>
    public Matrix FrameAt(double distance, double at)
    {
        (TwoDPoint point, TwoDVector way) = At(distance);

        // The path's left, which is where "up" goes: a quarter turn anticlockwise from the way it runs.
        double nx = -way.Y;
        double ny = way.X;

        return new Matrix(
        [
            way.X, nx, 0, point.X - at * way.X,
            way.Y, ny, 0, point.Y - at * way.Y,
            0, 0, 1, 0,
            0, 0, 0, 1
        ]);
    }

    /// <summary>
    /// This method finds the point on the curve a fraction of the way along one of its pieces, and
    /// which way the curve runs there.
    /// </summary>
    private static (TwoDPoint, TwoDVector) OnCurve(Piece piece, double fraction)
    {
        double t = piece.From + (piece.To - piece.From) * fraction;
        TwoDPoint point = piece.Segment.GetPoint(t);
        const double step = 1e-6;
        TwoDVector way = piece.Segment.GetPoint(Math.Min(1, t + step)) -
                         piece.Segment.GetPoint(Math.Max(0, t - step));

        // A curve may stop dead at an end, with its control point sitting on it, and then its own
        // direction there is nothing; the piece's chord is the way it is going all the same.
        if (way.Magnitude < 1e-12)
            way = piece.Segment.GetPoint(piece.To) - piece.Segment.GetPoint(piece.From);

        return (point, way.Unit);
    }

    /// <summary>
    /// One short piece of the path: the stretch of a segment's parameter it covers, how far along the
    /// path it starts and how long it is.
    /// </summary>
    private record Piece(IPathSegment Segment, double From, double To, double Start, double Length);
}
