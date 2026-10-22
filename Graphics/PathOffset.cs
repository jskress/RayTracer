namespace RayTracer.Graphics;

/// <summary>
/// This class draws an outline in, or out, by a distance: every run of it moved that far to its left, which for
/// an outline whose outer runs go counter-clockwise and whose holes go clockwise -- as one made ready for 3D is
/// (<see cref="GeneralPath.NormalizeFor3D"/>) -- is into the solid it bounds.  It is what a rounded extrusion
/// is built from: the outline drawn in by the rounding, and back out again with its corners rounded.
/// <para>
/// **A straight run moves exactly; a curve is fitted.**  What a Bézier curve becomes, moved a distance square
/// to itself all along, is not a Bézier curve, so each is matched by a cubic with the same ends and the same
/// directions there, its control points as far out along those directions as fits best by least squares, and
/// halved and matched again until no place on it is further than the tolerance from where it should be.
/// </para>
/// <para>
/// **Corners are where runs meet at an angle.**  Turning toward the side the outline moves to, the moved runs
/// cross, and each is cut off where they do; turning away, they part, and an arc round the corner joins them --
/// which is what rounds a box's corners when it is drawn back out.  A curve tighter than the distance cannot be
/// moved that far at all, and is refused rather than turned inside out.
/// </para>
/// </summary>
internal static class PathOffset
{
    private readonly record struct V2(double X, double Y)
    {
        public static V2 operator +(V2 a, V2 b) => new (a.X + b.X, a.Y + b.Y);
        public static V2 operator -(V2 a, V2 b) => new (a.X - b.X, a.Y - b.Y);
        public static V2 operator *(V2 a, double s) => new (a.X * s, a.Y * s);
        public double Dot(V2 b) => X * b.X + Y * b.Y;
        public double Cross(V2 b) => X * b.Y - Y * b.X;
        public double Length => Math.Sqrt(X * X + Y * Y);
        public V2 Unit => this * (1 / Length);
        public V2 Left => new (-Y, X);
    }

    // A piece of a moved run: a cubic, or a straight line between its ends.
    private readonly record struct Piece(V2 P0, V2 P1, V2 P2, V2 P3, bool Straight)
    {
        public V2 At(double t)
        {
            if (Straight)
                return P0 + (P3 - P0) * t;

            double u = 1 - t;

            return P0 * (u * u * u) + P1 * (3 * u * u * t) + P2 * (3 * u * t * t) + P3 * (t * t * t);
        }

        public V2 Velocity(double t)
        {
            if (Straight)
                return P3 - P0;

            double u = 1 - t;

            return (P1 - P0) * (3 * u * u) + (P2 - P1) * (6 * u * t) + (P3 - P2) * (3 * t * t);
        }

        public V2 Acceleration(double t) =>
            Straight ? new V2(0, 0) : (P2 - P1 * 2 + P0) * (6 * (1 - t)) + (P3 - P2 * 2 + P1) * (6 * t);

        // The way it runs at a place, of unit length, looking a little further in where it momentarily stops.
        public V2 Direction(double t)
        {
            V2 velocity = Velocity(t);

            if (velocity.Length > 1e-12 * (P3 - P0).Length + 1e-300)
                return velocity.Unit;

            V2 nudged = Velocity(t < 0.5 ? t + 1e-6 : t - 1e-6);

            return nudged.Length > 0 ? nudged.Unit : (P3 - P0).Unit;
        }

        public (Piece Before, Piece After) SplitAt(double t)
        {
            if (Straight)
            {
                V2 middle = At(t);

                return (Line(P0, middle), Line(middle, P3));
            }

            V2 a = P0 + (P1 - P0) * t, b = P1 + (P2 - P1) * t, c = P2 + (P3 - P2) * t;
            V2 d = a + (b - a) * t, e = b + (c - b) * t;
            V2 f = d + (e - d) * t;

            return (new Piece(P0, a, d, f, false), new Piece(f, e, c, P3, false));
        }
    }

    // How many times a curve may be halved in following it moved: 256 pieces of one curve at the most.
    private const int MostHalvings = 8;

    private static Piece Line(V2 from, V2 to) => new (from, from + (to - from) * (1.0 / 3), from + (to - from) * (2.0 / 3), to, true);

    /// <summary>
    /// This method returns an outline moved the given distance to the left of each of its runs -- into the
    /// solid, for an outline made ready for 3D -- or to the right for a distance below nought.
    /// </summary>
    /// <param name="path">The outline, every run of it closed.</param>
    /// <param name="distance">How far to move it, to the left of each run.</param>
    /// <param name="tolerance">How far a moved curve may stray from where it should be.</param>
    /// <returns>The outline moved.</returns>
    public static GeneralPath Offset(GeneralPath path, double distance, double tolerance)
    {
        GeneralPath result = new ();

        foreach (SubPath run in path.FindAllSubPaths())
        {
            List<Piece> source = run.Path.Segments.Select(PieceOf).Where(piece => (piece.P3 - piece.P0).Length > 0 ||
                !piece.Straight).ToList();

            if (source.Count == 0)
                continue;

            // A run left open is taken as closed by a straight line, as the even-odd rule takes it.
            if ((source[^1].P3 - source[0].P0).Length > tolerance)
                source.Add(Line(source[^1].P3, source[0].P0));

            List<List<Piece>> moved = source.Select(piece => Moved(piece, distance, tolerance)).ToList();
            List<Piece> joined = [];

            // Each corner, the last piece of one moved run against the first of the next, round the whole run.
            for (int index = 0; index < source.Count; index++)
            {
                int next = (index + 1) % source.Count;
                V2 coming = source[index].Direction(1);
                V2 going = source[next].Direction(0);
                double turn = Math.Atan2(coming.Cross(going), coming.Dot(going));

                if (Math.Abs(turn) < 1e-9)
                    continue;

                // Turning toward the side moved to, the moved runs cross; away, they part.
                if (turn * distance > 0)
                {
                    (Piece cutEnd, Piece cutStart) = Crossing(moved[index][^1], moved[next][0], distance);

                    moved[index][^1] = cutEnd;
                    moved[next][0] = cutStart;
                }
                else
                    moved[index].AddRange(Arc(source[index].P3, moved[index][^1].P3, moved[next][0].P0, turn, Math.Abs(distance)));
            }

            foreach (List<Piece> pieces in moved)
                joined.AddRange(pieces);

            Write(result, joined);
        }

        return result;
    }

    /// <summary>
    /// This method returns an outline with each of its corners that turns toward the left of its runs rounded
    /// off by an arc of the given radius -- the corners an outline made ready for 3D turns outward at -- and the
    /// rest of it exactly as it was.  It is the outline drawn in by the radius and back out again, without
    /// fitting anything but the arcs: each one centered where the outline drawn in meets itself at that corner.
    /// </summary>
    /// <param name="path">The outline, every run of it closed.</param>
    /// <param name="radius">How far to round its corners.</param>
    /// <param name="tolerance">How far a curve drawn in, to find a corner's middle, may stray.</param>
    /// <returns>The outline with its corners rounded.</returns>
    public static GeneralPath RoundedCorners(GeneralPath path, double radius, double tolerance)
    {
        GeneralPath result = new ();

        foreach (SubPath run in path.FindAllSubPaths())
        {
            List<Piece> pieces = run.Path.Segments.Select(PieceOf).Where(piece => (piece.P3 - piece.P0).Length > 0 ||
                !piece.Straight).ToList();

            if (pieces.Count == 0)
                continue;

            if ((pieces[^1].P3 - pieces[0].P0).Length > tolerance)
                pieces.Add(Line(pieces[^1].P3, pieces[0].P0));

            List<List<Piece>> kept = pieces.Select(piece => new List<Piece> { piece }).ToList();

            for (int index = 0; index < pieces.Count; index++)
            {
                int next = (index + 1) % pieces.Count;
                V2 coming = kept[index][^1].Direction(1);
                V2 going = kept[next][0].Direction(0);
                double turn = Math.Atan2(coming.Cross(going), coming.Dot(going));

                if (turn < 1e-9)
                    continue;

                // The corner's middle, where the runs drawn in meet; the arc touches each run square to it.
                Piece before = kept[index][^1], after = kept[next][0];
                (Piece movedBefore, Piece _) = Crossing(Moved(before, radius, tolerance)[^1], Moved(after, radius, tolerance)[0], radius);
                V2 middle = movedBefore.P3;
                double s = Nearest(before, middle, 1), t = Nearest(after, middle, 0);

                kept[index][^1] = before.SplitAt(s).Before;
                kept[next][0] = after.SplitAt(t).After;
                kept[index].AddRange(Arc(middle, kept[index][^1].P3, kept[next][0].P0, turn, radius));
            }

            Write(result, kept.SelectMany(list => list).ToList());
        }

        return result;
    }

    // Writes a run of pieces into a path, closing it exactly where it began.
    private static void Write(GeneralPath result, List<Piece> joined)
    {
        TwoDPoint first = new (joined[0].P0.X, joined[0].P0.Y);

        result.MoveTo(first);

        // Each piece ends where the next starts, near enough; the last is made to end exactly where the run
        // began, so that closing it adds no sliver of a line.
        for (int index = 0; index < joined.Count; index++)
        {
            Piece piece = joined[index];
            TwoDPoint end = index == joined.Count - 1 ? first : new TwoDPoint(piece.P3.X, piece.P3.Y);

            if (piece.Straight)
                result.LineTo(end);
            else
                result.CubicTo(new TwoDPoint(piece.P1.X, piece.P1.Y), new TwoDPoint(piece.P2.X, piece.P2.Y), end);
        }

        result.ClosePath();
    }

    /// <summary>
    /// This method refuses an outline drawn in that crosses itself, which is what one does where the outline
    /// was narrower than twice the distance it was drawn in by -- a slot, a waist, a horn too thin for the
    /// rounding asked of it -- however gently each of its curves bends.
    /// </summary>
    /// <param name="drawnIn">The outline drawn in.</param>
    /// <param name="distance">How far it was drawn in, for the complaint.</param>
    public static void EnsureUncrossed(GeneralPath drawnIn, double distance)
    {
        List<(V2 From, V2 To, int Run, int Index, int Count)> edges = [];
        int number = 0;

        foreach (SubPath run in drawnIn.FindAllSubPaths())
        {
            List<V2> points = [];

            foreach (Piece piece in run.Path.Segments.Select(PieceOf))
            {
                int steps = piece.Straight ? 1 : 24;

                for (int step = points.Count == 0 ? 0 : 1; step <= steps; step++)
                    points.Add(piece.At((double) step / steps));
            }

            for (int index = 0; index + 1 < points.Count; index++)
                edges.Add((points[index], points[index + 1], number, index, points.Count - 1));

            number++;
        }

        for (int i = 0; i < edges.Count; i++)
        {
            for (int j = i + 1; j < edges.Count; j++)
            {
                (V2 a0, V2 a1, int runA, int indexA, int countA) = edges[i];
                (V2 b0, V2 b1, int runB, int indexB, _) = edges[j];

                // Neighbors along a run share an end, the run's last edge and its first among them.
                if (runA == runB && (indexB - indexA == 1 || (indexA == 0 && indexB == countA - 1)))
                    continue;

                if (SegmentsCross(a0, a1, b0, b1, out double along, out double across) &&
                    along is > 1e-9 and < 1 - 1e-9 && across is > 1e-9 and < 1 - 1e-9)
                {
                    V2 at = a0 + (a1 - a0) * along;

                    throw new Exception(
                        $"An outline is narrower near ({at.X:0.###}, {at.Y:0.###}) than twice the {Math.Abs(distance):0.###} " +
                        "it is rounded by; round it by less, or widen it there.");
                }
            }
        }
    }

    // A path segment as a piece: a line, a quadratic raised to the cubic it is, or a cubic.
    private static Piece PieceOf(IPathSegment segment)
    {
        TwoDPoint[] points = segment.Points;
        V2 point(int index) => new (points[index].X, points[index].Y);

        return points.Length switch
        {
            2 => Line(point(0), point(1)),
            3 => new Piece(point(0), point(0) + (point(1) - point(0)) * (2.0 / 3), point(2) + (point(1) - point(2)) * (2.0 / 3), point(2), false),
            _ => new Piece(point(0), point(1), point(2), point(3), false)
        };
    }

    // A piece moved the distance to its left: exactly, for a line; fitted and halved till close enough, for a curve.
    private static List<Piece> Moved(Piece piece, double distance, double tolerance, int depth = 0)
    {
        if (piece.Straight)
        {
            V2 left = (piece.P3 - piece.P0).Unit.Left * distance;

            return [Line(piece.P0 + left, piece.P3 + left)];
        }

        // Where it bends tighter than the distance toward the side moved to, it cannot be moved so far.
        for (int index = 0; index <= 32; index++)
        {
            double t = index / 32.0;
            V2 velocity = piece.Velocity(t);
            double speed = velocity.Length;

            if (speed > 0 && velocity.Cross(piece.Acceleration(t)) / (speed * speed * speed) * distance >= 1)
            {
                V2 at = piece.At(t);

                throw new Exception(
                    $"An outline bends more tightly near ({at.X:0.###}, {at.Y:0.###}) than it can be rounded by " +
                    $"{Math.Abs(distance):0.###}; round it by less, or ease the curve there.");
            }
        }

        V2 startDirection = piece.Direction(0), endDirection = piece.Direction(1);
        V2 start = piece.P0 + startDirection.Left * distance, end = piece.P3 + endDirection.Left * distance;
        const int samples = 16;
        V2[] wanted = new V2[samples - 1];
        double[] place = new double[samples - 1];

        for (int index = 1; index < samples; index++)
        {
            double t = (double) index / samples;

            wanted[index - 1] = piece.At(t) + piece.Direction(t).Left * distance;
            place[index - 1] = t;
        }

        double alpha = -1, beta = -1;

        // The control points stand out along the ends' directions as far as fits the moved places best, each
        // place matched to where along the fitted curve it is nearest, and fitted again (Schneider's way).
        for (int round = 0; round < 6; round++)
        {
            double aa = 0, ab = 0, bb = 0, ra = 0, rb = 0;

            for (int index = 0; index < wanted.Length; index++)
            {
                double t = place[index], u = 1 - t;
                V2 fixedPart = start * (u * u * u + 3 * u * u * t) + end * (3 * u * t * t + t * t * t);
                V2 residual = wanted[index] - fixedPart;
                V2 a = startDirection * (3 * u * u * t);
                V2 b = endDirection * (-3 * u * t * t);

                aa += a.Dot(a); ab += a.Dot(b); bb += b.Dot(b);
                ra += residual.Dot(a); rb += residual.Dot(b);
            }

            double determinant = aa * bb - ab * ab;

            if (Math.Abs(determinant) < 1e-300)
                break;

            alpha = (ra * bb - rb * ab) / determinant;
            beta = (aa * rb - ab * ra) / determinant;

            Piece trial = new (start, start + startDirection * alpha, end - endDirection * beta, end, false);

            for (int index = 0; index < wanted.Length; index++)
                place[index] = Nearest(trial, wanted[index], place[index]);
        }

        Piece fitted = new (start, start + startDirection * alpha, end - endDirection * beta, end, false);
        double worst = 0;

        // How far the fitted curve strays: each place on it, against the distance it should stand from the
        // curve moved -- measured to the curve's nearest place, not to the place with the same parameter, which
        // a fit can be well off of while lying on the right line.
        for (int index = 0; index <= 48; index++)
        {
            double t = index / 48.0;
            V2 point = fitted.At(t);
            double nearest = Nearest(piece, point, t);

            worst = Math.Max(worst, Math.Abs((point - piece.At(nearest)).Length - Math.Abs(distance)));
        }

        if (worst <= tolerance && alpha >= 0 && beta >= 0)
            return [fitted];

        // Halved this often, a curve still not followed is one the fitting cannot follow; better to say so than
        // to go on halving into tens of thousands of pieces.
        if (depth >= MostHalvings)
        {
            V2 at = piece.At(0.5);

            throw new Exception(
                $"An outline near ({at.X:0.###}, {at.Y:0.###}) cannot be followed closely enough to round it by " +
                $"{Math.Abs(distance):0.###}; round it by less, or ease the curve there.");
        }

        (Piece before, Piece after) = piece.SplitAt(0.5);
        List<Piece> pieces = Moved(before, distance, tolerance, depth + 1);

        pieces.AddRange(Moved(after, distance, tolerance, depth + 1));

        return pieces;
    }

    // The parameter of the place on a piece nearest a point, by Newton's method from a guess.
    private static double Nearest(Piece piece, V2 point, double guess)
    {
        double t = guess;

        for (int step = 0; step < 8; step++)
        {
            V2 gap = piece.At(t) - point;
            V2 velocity = piece.Velocity(t);
            double slope = velocity.Dot(velocity) + gap.Dot(piece.Acceleration(t));

            if (Math.Abs(slope) < 1e-300)
                break;

            t = Math.Clamp(t - gap.Dot(velocity) / slope, 0, 1);
        }

        return t;
    }

    // Where two moved pieces cross near the corner they meet at: the end of the first, the start of the second,
    // each cut there.
    private static (Piece, Piece) Crossing(Piece first, Piece second, double distance)
    {
        const int steps = 256;
        V2 previousA = first.At(1);

        for (int i = 1; i <= steps; i++)
        {
            double s = 1 - (double) i / steps;
            V2 pointA = first.At(s);
            V2 previousB = second.At(0);

            for (int j = 1; j <= steps; j++)
            {
                double t = (double) j / steps;
                V2 pointB = second.At(t);

                if (SegmentsCross(previousA, pointA, previousB, pointB, out double along, out double across))
                {
                    double sa = s + (1.0 / steps) * (1 - along);
                    double tb = t - (1.0 / steps) * (1 - across);

                    (sa, tb) = Refined(first, second, sa, tb);

                    return (first.SplitAt(sa).Before, second.SplitAt(tb).After);
                }

                previousB = pointB;
            }

            previousA = pointA;
        }

        V2 corner = first.At(1);

        throw new Exception(
            $"An outline near ({corner.X:0.###}, {corner.Y:0.###}) is too short or too narrow there to be rounded by " +
            $"{Math.Abs(distance):0.###}; round it by less.");
    }

    // Whether the straight runs a0-a1 and b0-b1 cross, and how far along each.
    private static bool SegmentsCross(V2 a0, V2 a1, V2 b0, V2 b1, out double along, out double across)
    {
        V2 r = a1 - a0, q = b1 - b0;
        double denominator = r.Cross(q);

        along = across = 0;

        if (Math.Abs(denominator) < 1e-300)
            return false;

        along = (b0 - a0).Cross(q) / denominator;
        across = (b0 - a0).Cross(r) / denominator;

        return along is >= 0 and <= 1 && across is >= 0 and <= 1;
    }

    // Newton's method on first(s) = second(t), from a close start.
    private static (double, double) Refined(Piece first, Piece second, double s, double t)
    {
        for (int step = 0; step < 30; step++)
        {
            V2 gap = first.At(s) - second.At(t);

            if (gap.Length < 1e-14)
                break;

            V2 ds = first.Velocity(s), dt = second.Velocity(t) * -1;
            double determinant = ds.Cross(dt);

            if (Math.Abs(determinant) < 1e-300)
                break;

            s = Math.Clamp(s - gap.Cross(dt) / determinant, 0, 1);
            t = Math.Clamp(t - ds.Cross(gap) / determinant, 0, 1);
        }

        return (s, t);
    }

    // An arc round a corner from one moved run's end to the next's start, turning as the outline turned there,
    // as cubics of at most a quarter turn each.
    private static IEnumerable<Piece> Arc(V2 center, V2 from, V2 to, double turn, double radius)
    {
        double start = Math.Atan2(from.Y - center.Y, from.X - center.X);
        int count = Math.Max(1, (int) Math.Ceiling(Math.Abs(turn) / (Math.PI / 2) - 1e-9));
        double sweep = turn / count;
        double reach = 4.0 / 3 * Math.Tan(sweep / 4) * radius;
        V2 previous = from;

        for (int index = 0; index < count; index++)
        {
            double a0 = start + sweep * index, a1 = a0 + sweep;
            V2 p3 = index == count - 1 ? to : center + new V2(Math.Cos(a1), Math.Sin(a1)) * radius;
            V2 t0 = new (-Math.Sin(a0), Math.Cos(a0)), t1 = new (-Math.Sin(a1), Math.Cos(a1));

            yield return new Piece(previous, previous + t0 * reach, p3 - t1 * reach, p3, false);

            previous = p3;
        }
    }
}
