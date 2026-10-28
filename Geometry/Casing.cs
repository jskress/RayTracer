using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This record holds a box, in a surface's own space, that a part of the surface lies wholly within: a corner
/// and the three edges leaving it, square to each other or not.  A round one holds a ball, or what its
/// transforms stretch a ball into, touching each of its faces at the middle -- a sphere's box.
/// <para>
/// **It is what the light of the glowing surfaces looks at a surface by**, through the ball round each
/// casing.  A surface's box stands square to its own space, which is right for most and wrong for a thin
/// thing running across it: a tube's stroke at a slant, or a curve, fills its box with nothing but a thin
/// line, and a ball round the box is almost all empty.  Such a surface lays its casings along itself.
/// </para>
/// </summary>
/// <param name="Corner">One corner of the box.</param>
/// <param name="First">The first edge leaving the corner.</param>
/// <param name="Second">The second.</param>
/// <param name="Third">The third.</param>
/// <param name="Round">Whether what the box holds is the ball touching its faces.</param>
public readonly record struct Casing(Point Corner, Vector First, Vector Second, Vector Third, bool Round = false)
{
    /// <summary>
    /// This method returns the casing a box is.
    /// </summary>
    /// <param name="box">The box.</param>
    /// <param name="round">Whether what it holds is the ball touching its faces.</param>
    /// <returns>The casing.</returns>
    public static Casing Of(BoundingBox box, bool round = false)
    {
        Point low = box.Minimum;
        Point high = box.Maximum;

        return new Casing(
            low, new Vector(high.X - low.X, 0, 0), new Vector(0, high.Y - low.Y, 0), new Vector(0, 0, high.Z - low.Z),
            round);
    }

    /// <summary>
    /// This method returns the casings a run of tube lies within: the Bézier curve its middle follows, through
    /// the given control points, cut into the given number of pieces, each in a box laid along it and grown by
    /// the tube's widest radius.  A Bézier curve lies within the points that control it, and a piece of one is
    /// a Bézier curve too, with control points of its own; a run of tube is spheres along its middle, the
    /// widest as wide as the widest it is given.
    /// </summary>
    /// <param name="controls">The control points of the tube's middle.</param>
    /// <param name="radius">The tube's widest radius.</param>
    /// <param name="pieces">How many pieces to cut the curve into.</param>
    /// <returns>The casings.</returns>
    public static IEnumerable<Casing> AlongATube(Point[] controls, double radius, int pieces)
    {
        for (int piece = 0; piece < pieces; piece++)
        {
            Point[] part = Between(controls, (double) piece / pieces, (double) (piece + 1) / pieces);

            yield return Along(part, radius);
        }
    }

    /// <summary>
    /// This method returns the control points of the piece of a Bézier curve between two of its places, by de
    /// Casteljau's construction: cut at the far place, then the near part cut at the near place.
    /// </summary>
    private static Point[] Between(Point[] controls, double from, double to)
    {
        Point[] near = CutAt(controls, to).Near;

        return to > 0 ? CutAt(near, from / to).Far : near;
    }

    /// <summary>
    /// This method cuts a Bézier curve in two at a place along it, returning each part's control points.
    /// </summary>
    private static (Point[] Near, Point[] Far) CutAt(Point[] controls, double at)
    {
        int count = controls.Length;
        Point[] near = new Point[count];
        Point[] far = new Point[count];
        Point[] row = (Point[]) controls.Clone();

        for (int level = 0; level < count; level++)
        {
            near[level] = row[0];
            far[count - 1 - level] = row[count - 1 - level];

            for (int index = 0; index < count - 1 - level; index++)
                row[index] = row[index] + (row[index + 1] - row[index]) * at;
        }

        return (near, far);
    }

    /// <summary>
    /// This method returns the box laid along a run from its first point to its last that holds every one of
    /// the points, grown on every side by the given radius.
    /// </summary>
    private static Casing Along(Point[] points, double radius)
    {
        Vector run = points[^1] - points[0];
        Vector along = run.Magnitude > 0 ? run.Unit : new Vector(1, 0, 0);
        (Vector across, Vector up) = Microfacets.FrameAround(along);
        Vector[] axes = [along, across, up];
        double[] least = [double.MaxValue, double.MaxValue, double.MaxValue];
        double[] most = [double.MinValue, double.MinValue, double.MinValue];

        foreach (Point point in points)
        {
            Vector from = point - points[0];

            for (int axis = 0; axis < 3; axis++)
            {
                double reach = from.Dot(axes[axis]);

                least[axis] = Math.Min(least[axis], reach - radius);
                most[axis] = Math.Max(most[axis], reach + radius);
            }
        }

        return new Casing(
            points[0] + along * least[0] + across * least[1] + up * least[2],
            along * (most[0] - least[0]), across * (most[1] - least[1]), up * (most[2] - least[2]));
    }
}
