using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class bends a surface along a spline: `deformed { along S  centerline from O toward D }`.  A place in the
/// shape's own space is measured as how far it lies along the centerline, a, and how far off it to either side, x
/// and y; it is stood the same distance along the spline, the same way off it in the spline's rotation-minimizing
/// frame there.  So each slice square to the centerline stays square to the spline and keeps its shape, which is a
/// bend rather than a shear.  Before the spline's start and past its end the shape carries on straight, along the
/// spline's heading at that end.  A spline running straight from O along D gives the shape back unchanged.
/// `twist` turns the slices about the spline as they go, evenly from nothing at its start to the angle at its end;
/// `stretched` lays the centerline from O to as far as the shape reaches along it over the whole spline.
/// <para>
/// **A ray is straight in the bent space and curved in the shape's own.**  It is followed there in chords (Barr,
/// "Ray Tracing Deformed Surfaces", SIGGRAPH 1986), each an ordinary ray the shape is asked about: the spline is
/// cut into pieces turning and twisting no more than eight degrees, a piece's place in the bent space is the slab
/// between the planes square to the spline at its two ends, and where a ray crosses a piece's slab it is cut into
/// chords, halved until none strays from the curve by more than a quarter of the ray's width there -- a
/// ten-thousandth of the shape's size for a ray with none.  Within a piece where a place lies along the spline has
/// one answer, found by Newton's method between the piece's ends, so a shape folded until its two halves lie side by
/// side is found in each half without either taking the other's place.
/// </para>
/// <para>
/// **A crossing found on a chord is found again on the ray**: stepped along the curve onto the plane touching the
/// surface where the chord met it, the surface it was met on asked about one short chord there, and stepped again
/// onto the plane where that chord met it -- which leaves it on the ray and within some trillionths of the shape's
/// size of its surface.  Where that cannot be done (the surface met at a graze, or through an instance, whose way
/// down cannot be followed) it is found again on three ever shorter chords instead.  A crossing no shorter chord finds
/// again was the chord's own, where it strayed across the shape and the ray does not, and is dropped.
/// </para>
/// <para>
/// **A bend tighter than the shape is thick on its inside is refused**: the inside would pass through itself.  So
/// are strands and hair inside a bent shape, which are found by how wide a ray is (a chord has no width), and an
/// opening for the sky, which must be a box.
/// </para>
/// </summary>
public sealed class Deformation
{
    // How far a piece of the spline may turn, or twist, in radians.
    private const double MostTurning = 8 * Math.PI / 180;

    // How far, as a share of the shape's size, a chord may stray from the curve a ray becomes in the shape's space,
    // for a ray with no width: a shadow ray's.
    private const double Straying = 1e-4;

    // How far, as a share of how wide a ray is, a chord may stray: no further than a quarter of the patch the ray
    // covers, so nothing finer than a pixel is lost.  Held between a millionth and a hundredth of the shape's size.
    private const double StrayingInWidths = 0.25;

    // How many times a chord may be halved.
    private const int DeepestHalving = 24;

    // How far into a bend's radius the shape may reach on its inside.
    private const double FarthestIn = 0.98;

    // Four-point Gauss-Legendre, for a length along a piece of a curve.
    private static readonly double[] Nodes =
    [
        -0.8611363115940526, -0.3399810435848563, 0.3399810435848563, 0.8611363115940526
    ];

    private static readonly double[] Weights =
    [
        0.3478548451374538, 0.6521451548625461, 0.6521451548625461, 0.3478548451374538
    ];

    // Two-point Gauss-Legendre, exact for a cubic's velocity.
    private static readonly double TwoPoint = 1 / Math.Sqrt(3);

    /// <summary>
    /// This property holds the spline the shape is bent along.
    /// </summary>
    public Spline Spline { get; set; }

    /// <summary>
    /// This property holds where the centerline starts, in the shape's own space; the origin if not said.
    /// </summary>
    public Point From { get; set; }

    /// <summary>
    /// This property holds the way the centerline runs, in the shape's own space.
    /// </summary>
    public Vector Toward { get; set; }

    /// <summary>
    /// This property holds how far the slices are turned about the spline by its end, in radians, turning evenly
    /// along it from none at its start.
    /// </summary>
    public double Twist { get; set; }

    /// <summary>
    /// This property notes that the centerline, from its start to as far as the shape reaches along it, is laid
    /// over the whole spline, rather than the spline being followed for as long as the shape is.
    /// </summary>
    public bool Stretched { get; set; }

    /// <summary>
    /// This property notes that some deformation has been made ready, so that carrying a normal out to the world
    /// must carry the place it is at alongside it.  Until one is, nothing need.
    /// </summary>
    internal static bool AnyMade { get; private set; }

    /// <summary>
    /// This property holds the box the bent shape occupies, in its surface's own space before its transforms.
    /// </summary>
    internal BoundingBox OuterBox { get; private set; }

    private V3 _origin;
    private V3 _along;
    private V3 _across;
    private V3 _up;
    private Bezier[] _curves = [];
    private Piece[] _pieces = [];
    private Piece[] _active = [];
    private BoundingBox _innerBox;
    private V3 _innerMin;
    private V3 _innerMax;
    private double _aMin;
    private double _aMax;
    private double _xMin;
    private double _xMax;
    private double _yMin;
    private double _yMax;
    private double _scale;
    private double _length;
    private double _stretch = 1;
    private double _twistRate;

    [ThreadStatic] private static Deformation _lastBent;
    [ThreadStatic] private static double _lastX;
    [ThreadStatic] private static double _lastY;
    [ThreadStatic] private static double _lastZ;
    [ThreadStatic] private static V3 _lastPlace;

    /// <summary>
    /// This method makes the deformation ready for the surface it bends, once that surface knows its own box.
    /// </summary>
    /// <param name="surface">The surface bent.</param>
    internal void Prepare(Surface surface)
    {
        AnyMade = true;

        if (surface.BoundingBox is not { } box)
        {
            throw new InvalidOperationException(
                "A shape that runs on for ever cannot be deformed: it has no length to bend along.");
        }

        if (Toward is null || Toward.Magnitude == 0)
            throw new InvalidOperationException("A deformation's centerline must run some way.");

        RefuseWhatCannotBend(surface, []);

        _innerBox = box;
        _origin = From is null ? default : V3.Of(From);
        _along = V3.Of(Toward).Unit;
        _across = Perpendicular(_along);
        _up = _along.Cross(_across);

        BuildPieces();

        if (box.IsEmpty)
        {
            _active = [];
            OuterBox = new BoundingBox();

            return;
        }

        MeasureTheShape(box);

        if (Stretched)
        {
            double reach = Reach(surface.TrueBoundingBox() ?? box);

            if (reach <= 1e-9 * _scale)
            {
                throw new InvalidOperationException(
                    "A stretched deformation lays the centerline from its start to as far as the shape reaches along " +
                    "it over the spline, and this shape reaches nowhere past the centerline's start.");
            }

            _stretch = _length / reach;
        }

        RefuseFolds();
        SettleTheActivePieces();
    }

    // Refuses what a chord cannot find, or a bend cannot carry, anywhere in the shape: strands and hair, found by how
    // wide a ray is, and openings for the sky, which must be boxes.
    private static void RefuseWhatCannotBend(Surface surface, HashSet<Surface> seen)
    {
        if (!seen.Add(surface))
            return;

        if (surface.HairGrowth is not null || surface is StrandSurface)
        {
            throw new InvalidOperationException(
                "A deformed shape cannot hold hair or strands: they are found by how wide a ray is, and a bend is " +
                "followed in chords, which have no width.");
        }

        if (surface.OpensToTheSky)
            throw new InvalidOperationException("A deformed shape cannot hold an opening for the sky: it must be a box.");

        switch (surface)
        {
            case Instance instance:
                RefuseWhatCannotBend(instance.Prototype, seen);
                break;
            case Group group:
                foreach (Surface child in group.Surfaces)
                    RefuseWhatCannotBend(child, seen);
                break;
            case CsgSurface csg:
                RefuseWhatCannotBend(csg.Left, seen);
                RefuseWhatCannotBend(csg.Right, seen);
                break;
            case IAssembled { Root: { } root }:
                RefuseWhatCannotBend(root, seen);
                break;
        }
    }

    // How far a box reaches along the centerline past its start.
    private double Reach(BoundingBox box)
    {
        double most = double.NegativeInfinity;

        foreach (V3 corner in Corners(box))
            most = Math.Max(most, (corner - _origin).Dot(_along));

        return most;
    }

    private static IEnumerable<V3> Corners(BoundingBox box)
    {
        Point low = box.Minimum;
        Point high = box.Maximum;

        for (int corner = 0; corner < 8; corner++)
        {
            yield return new V3(
                (corner & 1) == 0 ? low.X : high.X,
                (corner & 2) == 0 ? low.Y : high.Y,
                (corner & 4) == 0 ? low.Z : high.Z);
        }
    }

    // The spline's curves as cubics, and the pieces they are cut into, with a straight piece before and after.
    private void BuildPieces()
    {
        List<ISplineCurve> curves = Spline?.GetCurves() ?? [];

        if (curves.Count == 0)
            throw new InvalidOperationException("A shape must be deformed along a spline that runs somewhere.");

        _curves = curves.Select(Bezier.Of).ToArray();

        // The spans each curve is cut into by how it turns, and how long each is: the whole length must be known
        // before it can be said how far each span twists.
        List<(int Curve, double U0, double U1, double Length)> spans = [];

        for (int index = 0; index < _curves.Length; index++)
        {
            Bezier curve = _curves[index];

            if (index > 0 && Angle(_curves[index - 1].Tangent(1), curve.Tangent(0)) > 0.5 * Math.PI / 180)
            {
                throw new InvalidOperationException(
                    "A shape can only be deformed along a spline that turns smoothly: this one turns sharply where " +
                    $"its curve {index + 1} begins.");
            }

            List<(double U0, double U1)> turned = [];

            if (curves[index] is SplineLine)
                turned.Add((0, 1));
            else
                Split(curve, 0, 1, 0, turned);

            spans.AddRange(turned.Select(span => (index, span.U0, span.U1, ArcLength(curve, span.U0, span.U1))));
        }

        _length = spans.Sum(span => span.Length);

        if (_length <= 0)
            throw new InvalidOperationException("A shape must be deformed along a spline that runs somewhere.");

        _twistRate = Twist / _length;

        List<Piece> pieces = [new Piece { Straight = true, Curve = -1, S0 = double.NegativeInfinity, S1 = 0 }];
        V3 normal = TurnedLeast(_along, _curves[0].Tangent(0), _across);
        double sofar = 0;

        foreach ((int index, double u0, double u1, double length) in spans)
        {
            Bezier curve = _curves[index];
            bool line = curves[index] is SplineLine;
            int parts = Math.Max(1, (int) Math.Ceiling(Math.Abs(_twistRate) * length / MostTurning));

            for (int part = 0; part < parts; part++)
            {
                double from = u0 + (u1 - u0) * part / parts;
                double to = part == parts - 1 ? u1 : u0 + (u1 - u0) * (part + 1) / parts;
                V3 c0 = curve.At(from);
                V3 t0 = curve.Tangent(from);
                V3 c1 = curve.At(to);
                V3 t1 = curve.Tangent(to);
                V3 n0 = (normal - t0 * t0.Dot(normal)).Unit;
                V3 n1 = Reflected(Displacement(curve, from, to), t0, n0, t1);
                double piece = parts == 1 ? length : ArcLength(curve, from, to);
                double middle = (from + to) / 2;

                pieces.Add(new Piece
                {
                    Straight = line && Twist == 0,
                    Curve = index,
                    U0 = from,
                    U1 = to,
                    S0 = sofar,
                    S1 = sofar + piece,
                    Reference = sofar,
                    C0 = c0,
                    T0 = t0,
                    N0 = n0,
                    C1 = c1,
                    T1 = t1,
                    N1 = n1,
                    Turning = Angle(t0, curve.Tangent(middle)) + Angle(curve.Tangent(middle), t1) +
                              Math.Abs(_twistRate) * piece
                });

                sofar += piece;
                normal = n1;
            }
        }

        // Where two curves join, the pieces either side share one plane, square to the heading between theirs: the
        // spline may turn by as much as a degree there, and each piece's own plane would leave a wedge between them
        // that is neither's -- crossings lost -- or both's -- crossings found twice.
        for (int index = 2; index < pieces.Count; index++)
        {
            if (pieces[index].Curve != pieces[index - 1].Curve)
                pieces[index].T0 = pieces[index - 1].T1 = (pieces[index - 1].T1 + pieces[index].T0).Unit;
        }

        Piece first = pieces[1];
        Piece last = pieces[^1];

        pieces[0].C0 = pieces[0].C1 = first.C0;
        pieces[0].T0 = pieces[0].T1 = first.T0;
        pieces[0].N0 = pieces[0].N1 = first.N0;

        pieces.Add(new Piece
        {
            Straight = true, Curve = -2, S0 = sofar, S1 = double.PositiveInfinity, Reference = sofar,
            C0 = last.C1, C1 = last.C1, T0 = last.T1, T1 = last.T1, N0 = last.N1, N1 = last.N1
        });

        _pieces = pieces.ToArray();
    }

    // Cuts a curve into spans each turning no more than the most a piece may.
    private static void Split(Bezier curve, double u0, double u1, int depth, List<(double, double)> into)
    {
        double middle = (u0 + u1) / 2;
        V3 middleTangent = curve.Tangent(middle);
        double turning = Angle(curve.Tangent(u0), middleTangent) + Angle(middleTangent, curve.Tangent(u1));

        if (turning <= MostTurning || depth >= 16)
        {
            into.Add((u0, u1));

            return;
        }

        Split(curve, u0, middle, depth + 1, into);
        Split(curve, middle, u1, depth + 1, into);
    }

    // How far along the centerline, and to either side of it, the shape's box reaches.
    private void MeasureTheShape(BoundingBox box)
    {
        _innerMin = V3.Of(box.Minimum);
        _innerMax = V3.Of(box.Maximum);
        _aMin = _xMin = _yMin = double.PositiveInfinity;
        _aMax = _xMax = _yMax = double.NegativeInfinity;

        foreach (V3 corner in Corners(box))
        {
            V3 offset = corner - _origin;
            double a = offset.Dot(_along);
            double x = offset.Dot(_across);
            double y = offset.Dot(_up);

            _aMin = Math.Min(_aMin, a);
            _aMax = Math.Max(_aMax, a);
            _xMin = Math.Min(_xMin, x);
            _xMax = Math.Max(_xMax, x);
            _yMin = Math.Min(_yMin, y);
            _yMax = Math.Max(_yMax, y);
        }

        _scale = Math.Max(Math.Max(Reach(), _aMax - _aMin), 1e-12);
    }

    // How far from the centerline the shape's box reaches.
    private double Reach() =>
        Math.Sqrt(Math.Max(_xMin * _xMin, _xMax * _xMax) + Math.Max(_yMin * _yMin, _yMax * _yMax));

    // Refuses a bend tighter than the shape reaches on its inside.
    private void RefuseFolds()
    {
        foreach (Piece piece in _pieces.Where(piece => !piece.Straight && Overlaps(piece)))
        {
            Bezier curve = _curves[piece.Curve];

            foreach (double u in new[] { piece.U0, (piece.U0 + piece.U1) / 2, piece.U1 })
            {
                Place place = PlaceAt(piece, curve, u);
                double inward = Math.Max(_xMin * place.K.Dot(place.N), _xMax * place.K.Dot(place.N)) +
                                Math.Max(_yMin * place.K.Dot(place.B), _yMax * place.K.Dot(place.B));

                if (inward >= FarthestIn)
                {
                    double bend = place.K.Length;

                    throw new InvalidOperationException(
                        $"A shape is bent too tightly to be deformed: the spline bends with a radius of {1 / bend:G4} " +
                        $"some {piece.S0:G4} along it, and the shape reaches {inward / bend:G4} toward the inside of " +
                        "that bend, so its inside would pass through itself.");
                }
            }
        }
    }

    // Whether a piece holds any of the shape.
    private bool Overlaps(Piece piece) => piece.S1 > _aMin * _stretch && piece.S0 < _aMax * _stretch;

    // The pieces the shape reaches into, each boxed round what of the shape it holds.
    private void SettleTheActivePieces()
    {
        List<Piece> active = [];
        BoundingBox outer = new ();

        foreach (Piece piece in _pieces.Where(Overlaps))
        {
            BoundingBox box = new ();

            if (piece.Straight)
            {
                foreach (double s in new[] { Math.Max(piece.S0, _aMin * _stretch), Math.Min(piece.S1, _aMax * _stretch) })
                {
                    Place place = StraightPlace(piece, s);

                    AddCorners(box, place.C, place.N, place.B);
                }

                box.Expand(1e-9 * _scale);
            }
            else
            {
                Bezier curve = _curves[piece.Curve];

                for (int step = 0; step <= 4; step++)
                {
                    Place place = PlaceAt(piece, curve, piece.U0 + (piece.U1 - piece.U0) * step / 4);

                    AddCorners(box, place.C, place.N, place.B);
                }

                double length = piece.S1 - piece.S0;
                double reach = Math.Max(Math.Max(Math.Abs(_xMin), Math.Abs(_xMax)),
                    Math.Max(Math.Abs(_yMin), Math.Abs(_yMax))) * Math.Sqrt(2);

                box.Expand((length + reach * piece.Turning) * piece.Turning / 4 + 1e-9 * _scale);
            }

            piece.Box = box;
            outer.Add(box);
            active.Add(piece);
        }

        _active = active.ToArray();
        OuterBox = outer;
    }

    private void AddCorners(BoundingBox box, V3 middle, V3 normal, V3 binormal)
    {
        foreach (double x in new[] { _xMin, _xMax })
        {
            foreach (double y in new[] { _yMin, _yMax })
                box.Add((middle + normal * x + binormal * y).ToPoint());
        }
    }

    /// <summary>
    /// This method adds where a ray, in the bent space, crosses the bent shape.  The crossings are the shape's own,
    /// found by asking it about chords, each with its distance set to where along this ray it lies.
    /// </summary>
    /// <param name="surface">The surface bent.</param>
    /// <param name="ray">The ray, in the bent space.</param>
    /// <param name="intersections">The list to add the crossings to.</param>
    /// <param name="from">How far along the ray to begin looking.</param>
    /// <param name="to">How far along the ray to stop looking.</param>
    internal void Intersect(
        Surface surface, Ray ray, List<Intersection> intersections, double from = double.NegativeInfinity,
        double to = double.PositiveInfinity)
    {
        if (_active.Length == 0)
            return;

        (double near, double far) = OuterBox.GetIntersections(ray);

        if (near > far || far < from || near > to)
            return;

        Marcher marcher = new (this, surface, ray, intersections);

        foreach (Piece piece in _active)
        {
            (double low, double high) = piece.Box.GetIntersections(ray);

            low = Math.Max(low, from);
            high = Math.Min(high, to);

            if (low >= high || !marcher.WithinTheSlab(piece, ref low, ref high, out bool fromAPlane, out bool toAPlane))
                continue;

            marcher.March(piece, low, high, fromAPlane, toAPlane);
        }
    }

    /// <summary>
    /// This method carries a place in the bent space into the shape's own.  Where a fold puts two of the shape's
    /// places at one in the bent space, the one within the shape's box -- or nearest it -- is taken.
    /// </summary>
    /// <param name="point">The place in the bent space.</param>
    /// <returns>The place in the shape's own space.</returns>
    internal Point Undeform(Point point)
    {
        if (ReferenceEquals(_lastBent, this) && _lastX == point.X && _lastY == point.Y && _lastZ == point.Z)
            return _lastPlace.ToPoint();

        V3 place = V3.Of(point);
        V3 best = place;
        double bestOff = double.PositiveInfinity;
        Piece[] pieces = _active.Length > 0 ? _active : _pieces;

        foreach (Piece piece in pieces)
        {
            if (!InSlab(piece, place))
                continue;

            V3 found = Inverse(piece, place);
            double off = OutsideTheShape(found);

            if (off < bestOff)
            {
                bestOff = off;
                best = found;
            }

            if (off == 0)
                break;
        }

        if (double.IsPositiveInfinity(bestOff))
        {
            // In no piece's slab -- beyond the spline's end, where two pieces' planes do not meet, say: the nearest
            // piece by its middle answers.
            Piece nearest = pieces.MinBy(piece => (Middle(piece) - place).Length);

            best = Inverse(nearest, place);
        }

        _lastBent = this;
        _lastX = point.X;
        _lastY = point.Y;
        _lastZ = point.Z;
        _lastPlace = best;

        return best.ToPoint();
    }

    /// <summary>
    /// This method carries a place in the shape's own space out into the bent space.
    /// </summary>
    /// <param name="point">The place in the shape's own space.</param>
    /// <returns>The place in the bent space.</returns>
    internal Point Deform(Point point)
    {
        V3 offset = V3.Of(point) - _origin;
        Place place = PlaceAlong(offset.Dot(_along) * _stretch);

        return (place.C + place.N * offset.Dot(_across) + place.B * offset.Dot(_up)).ToPoint();
    }

    /// <summary>
    /// This method carries a direction at a place in the shape's own space out into the bent space: stretched along
    /// the spline as the bend stretches the shape there, and turned with it.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="at">The place, in the shape's own space; with none, the direction is left as it is.</param>
    /// <returns>The direction in the bent space.</returns>
    internal Vector DirectionOut(Vector direction, Point at)
    {
        if (at is null)
            return direction;

        Bend bend = BendAt(at);
        V3 way = V3.Of(direction);
        double a = way.Dot(_along);

        return (bend.Place.T * (bend.Stretch * a) +
                bend.Place.N * (way.Dot(_across) - bend.Twisting * bend.Y * a) +
                bend.Place.B * (way.Dot(_up) + bend.Twisting * bend.X * a)).ToVector();
    }

    /// <summary>
    /// This method carries a direction at a place in the bent space into the shape's own: the inverse of
    /// <see cref="DirectionOut"/>.
    /// </summary>
    /// <param name="direction">The direction, in the bent space.</param>
    /// <param name="at">The place, in the shape's own space.</param>
    /// <returns>The direction in the shape's own space.</returns>
    internal Vector DirectionIn(Vector direction, Point at)
    {
        Bend bend = BendAt(at);
        V3 way = V3.Of(direction);
        double a = way.Dot(bend.Place.T) / bend.Stretch;

        return (_along * a +
                _across * (way.Dot(bend.Place.N) + bend.Twisting * bend.Y * a) +
                _up * (way.Dot(bend.Place.B) - bend.Twisting * bend.X * a)).ToVector();
    }

    /// <summary>
    /// This method carries a normal at a place in the shape's own space out into the bent space, by the inverse
    /// transpose of the bend there.  It is not made a unit.
    /// </summary>
    /// <param name="normal">The normal.</param>
    /// <param name="at">The place, in the shape's own space; with none, the normal is left as it is.</param>
    /// <returns>The normal in the bent space.</returns>
    internal Vector NormalOut(Vector normal, Point at)
    {
        if (at is null)
            return normal;

        Bend bend = BendAt(at);
        V3 way = V3.Of(normal);
        double x = way.Dot(_across);
        double y = way.Dot(_up);
        double along = (way.Dot(_along) + bend.Twisting * (bend.Y * x - bend.X * y)) / bend.Stretch;

        return (bend.Place.T * along + bend.Place.N * x + bend.Place.B * y).ToVector();
    }

    /// <summary>
    /// This method carries a footprint at a place in the bent space into the shape's own, each of its edges as a
    /// direction there.
    /// </summary>
    /// <param name="footprint">The footprint, in the bent space.</param>
    /// <param name="at">The place, in the shape's own space.</param>
    /// <returns>The footprint in the shape's own space.</returns>
    internal Footprint FootprintIn(Footprint footprint, Point at) =>
        new (DirectionIn(footprint.Across, at), DirectionIn(footprint.Along, at));

    // How the bend carries the shape at a place in its own space: the place along the spline, how much it stretches
    // the shape along the spline there, how fast it twists it (per unit along the centerline), and how far off the
    // centerline the place lies.  With these, a step (da, dx, dy) in the shape's own frame goes to
    // T (stretch da) + N (dx - twisting y da) + B (dy + twisting x da).
    private Bend BendAt(Point at)
    {
        V3 offset = V3.Of(at) - _origin;
        double x = offset.Dot(_across);
        double y = offset.Dot(_up);
        Place place = PlaceAlong(offset.Dot(_along) * _stretch);
        double stretch = _stretch * (1 - x * place.K.Dot(place.N) - y * place.K.Dot(place.B));

        return new Bend(place, Math.Max(stretch, 1e-6), _stretch * place.Rate, x, y);
    }

    private readonly record struct Bend(Place Place, double Stretch, double Twisting, double X, double Y);

    // How far outside the shape's box a place in its own space lies; nought inside it.
    private double OutsideTheShape(V3 place)
    {
        double x = Math.Max(Math.Max(_innerMin.X - place.X, place.X - _innerMax.X), 0);
        double y = Math.Max(Math.Max(_innerMin.Y - place.Y, place.Y - _innerMax.Y), 0);
        double z = Math.Max(Math.Max(_innerMin.Z - place.Z, place.Z - _innerMax.Z), 0);

        return x + y + z;
    }

    private static V3 Middle(Piece piece) => (piece.C0 + piece.C1) * 0.5;

    // Whether a place in the bent space lies between the planes square to the spline at a piece's two ends.
    private static bool InSlab(Piece piece, V3 place) =>
        (piece.Curve == -1 || (place - piece.C0).Dot(piece.T0) >= 0) &&
        (piece.Curve == -2 || (place - piece.C1).Dot(piece.T1) < 0);

    // The place a distance along the spline: its point, frame and bend.
    private Place PlaceAlong(double s)
    {
        int low = 0, high = _pieces.Length - 1;

        while (low < high)
        {
            int middle = (low + high) / 2;

            if (_pieces[middle].S1 <= s)
                low = middle + 1;
            else
                high = middle;
        }

        Piece piece = _pieces[low];

        if (piece.Straight)
            return StraightPlace(piece, s);

        Bezier curve = _curves[piece.Curve];

        return PlaceAt(piece, curve, ParameterFor(piece, curve, s), s);
    }

    // The place a distance along a straight piece.
    private Place StraightPlace(Piece piece, double s) =>
        Twisted(piece.C0 + piece.T0 * (s - piece.Reference), piece.T0, piece.N0, default, s);

    // The curve's own parameter a distance along the spline, within a piece.
    private static double ParameterFor(Piece piece, Bezier curve, double s)
    {
        double wanted = Math.Clamp(s, piece.S0, piece.S1) - piece.S0;
        double span = piece.S1 - piece.S0;
        double u = span > 0 ? piece.U0 + (piece.U1 - piece.U0) * wanted / span : piece.U0;

        for (int step = 0; step < 12; step++)
        {
            double error = ArcLength(curve, piece.U0, u) - wanted;
            double speed = curve.Velocity(u).Length;

            if (speed <= 0)
                break;

            double next = Math.Clamp(u - error / speed, piece.U0, piece.U1);

            if (Math.Abs(next - u) <= 1e-15)
                return next;

            u = next;
        }

        return u;
    }

    // The point, frame and bend at a curve's parameter within a piece, the frame turned on from the piece's start and
    // twisted as far as the spline twists there.
    private Place PlaceAt(Piece piece, Bezier curve, double u, double? at = null)
    {
        V3 c = curve.At(u);
        V3 velocity = curve.Velocity(u);
        V3 t = curve.Tangent(u);
        V3 n = Reflected(Displacement(curve, piece.U0, u), piece.T0, piece.N0, t);
        double speed = velocity.Length;
        V3 k = default;

        if (speed > 0)
        {
            V3 acceleration = curve.Acceleration(u);

            k = (acceleration - t * t.Dot(acceleration)) / (speed * speed);
        }

        double s = _twistRate == 0 ? 0 : at ?? piece.S0 + ArcLength(curve, piece.U0, u);

        return Twisted(c, t, n, k, s);
    }

    // A place with its frame turned about the spline by as far as the spline twists a distance along it.
    private Place Twisted(V3 c, V3 t, V3 n, V3 k, double s)
    {
        if (_twistRate == 0)
            return new Place { C = c, T = t, N = n, B = t.Cross(n), K = k };

        double angle = _twistRate * Math.Clamp(s, 0, _length);
        V3 binormal = t.Cross(n);
        V3 turned = n * Math.Cos(angle) + binormal * Math.Sin(angle);

        return new Place
        {
            C = c, T = t, N = turned, B = t.Cross(turned), K = k,
            Rate = s > 0 && s < _length ? _twistRate : 0
        };
    }

    // A place in the bent space carried into the shape's own, by a piece it lies in.
    private V3 Inverse(Piece piece, V3 point)
    {
        Place place;
        double s;

        if (piece.Straight)
        {
            s = piece.Reference + (point - piece.C0).Dot(piece.T0);
            place = StraightPlace(piece, s);
        }
        else
        {
            Bezier curve = _curves[piece.Curve];
            double u = Root(piece, curve, point);

            s = piece.S0 + ArcLength(curve, piece.U0, u);
            place = PlaceAt(piece, curve, u, s);
        }

        V3 offset = point - place.C;

        return _origin + _along * (s / _stretch) + _across * offset.Dot(place.N) + _up * offset.Dot(place.B);
    }

    // The curve's parameter where the plane square to it holds the point, within a piece: Newton's method, bisecting
    // wherever it would step outside what is known to hold the answer.
    private static double Root(Piece piece, Bezier curve, V3 point)
    {
        double low = piece.U0, high = piece.U1;
        double lowSide = (point - piece.C0).Dot(piece.T0);
        double highSide = (point - piece.C1).Dot(piece.T1);

        if (lowSide <= 0)
            return low;

        if (highSide >= 0)
            return high;

        double u = low + (high - low) * lowSide / (lowSide - highSide);

        for (int step = 0; step < 60; step++)
        {
            V3 c = curve.At(u);
            V3 velocity = curve.Velocity(u);
            double speed = velocity.Length;

            if (speed <= 0)
            {
                u = (low + high) / 2;

                continue;
            }

            V3 t = velocity / speed;
            V3 acceleration = curve.Acceleration(u);
            V3 offset = point - c;
            double side = offset.Dot(t);
            V3 turning = (acceleration - t * t.Dot(acceleration)) / speed;
            double slope = -speed + offset.Dot(turning);

            if (side > 0)
                low = u;
            else
                high = u;

            double next = slope < 0 ? u - side / slope : double.NaN;

            if (!(next > low && next < high))
                next = (low + high) / 2;

            if (Math.Abs(next - u) <= 1e-15 || high - low <= 1e-15)
                return next;

            u = next;
        }

        return u;
    }

    // How long a curve is between two of its parameters: four-point Gauss-Legendre, ample for a piece turning so
    // little.
    private static double ArcLength(Bezier curve, double u0, double u1)
    {
        double half = (u1 - u0) / 2;

        if (half == 0)
            return 0;

        double middle = (u0 + u1) / 2;
        double sum = 0;

        for (int index = 0; index < Nodes.Length; index++)
            sum += Weights[index] * curve.Velocity(middle + half * Nodes[index]).Length;

        return sum * half;
    }

    // How far a curve goes between two of its parameters, as the sum of its velocity rather than the difference of
    // two places: a little way on from a piece's start, the difference of two places cancels away nearly all its
    // digits, and a frame turned on by it came out as noise.  Two Gauss-Legendre points are exact for a cubic.
    private static V3 Displacement(Bezier curve, double u0, double u1)
    {
        double half = (u1 - u0) / 2;
        double middle = (u0 + u1) / 2;

        return (curve.Velocity(middle - half * TwoPoint) + curve.Velocity(middle + half * TwoPoint)) * half;
    }

    // A frame's normal carried by Wang's double reflection from one heading to another, the given way apart.
    private static V3 Reflected(V3 v1, V3 t0, V3 n0, V3 t1)
    {
        double c1Square = v1.Dot(v1);

        if (c1Square == 0)
            return (n0 - t1 * t1.Dot(n0)).Unit;

        V3 reflectedNormal = n0 - v1 * (2 / c1Square * v1.Dot(n0));
        V3 reflectedTangent = t0 - v1 * (2 / c1Square * v1.Dot(t0));
        V3 v2 = t1 - reflectedTangent;
        double c2Square = v2.Dot(v2);
        V3 normal = c2Square < 1e-30 ? reflectedNormal : reflectedNormal - v2 * (2 / c2Square * v2.Dot(reflectedNormal));

        return (normal - t1 * t1.Dot(normal)).Unit;
    }

    // A direction carried by the least turn that takes one way onto another.
    private static V3 TurnedLeast(V3 from, V3 to, V3 direction)
    {
        V3 axis = from.Cross(to);
        double sine = axis.Length;
        double cosine = from.Dot(to);

        if (sine < 1e-12)
            return direction;

        axis /= sine;

        return direction * cosine + axis.Cross(direction) * sine + axis * (axis.Dot(direction) * (1 - cosine));
    }

    private static V3 Perpendicular(V3 way)
    {
        V3 other = Math.Abs(way.Y) < 0.9 ? new V3(0, 1, 0) : new V3(1, 0, 0);

        return (other - way * way.Dot(other)).Unit;
    }

    private static double Angle(V3 one, V3 two) => Math.Atan2(one.Cross(two).Length, one.Dot(two));

    // Whether the stretch of the curve about a chord may come near the shape's box at all: the five places known
    // along it, boxed and grown by twice how far the chord strays.
    private bool MayHoldTheShape(Chord chord, V3 quarter, V3 threeQuarters, double straying, double tolerance)
    {
        V3 low = V3.Min(V3.Min(V3.Min(chord.Q0, chord.Q1), V3.Min(chord.QM, quarter)), threeQuarters);
        V3 high = V3.Max(V3.Max(V3.Max(chord.Q0, chord.Q1), V3.Max(chord.QM, quarter)), threeQuarters);
        double grow = 2 * straying + tolerance;

        return low.X - grow <= _innerMax.X && high.X + grow >= _innerMin.X &&
               low.Y - grow <= _innerMax.Y && high.Y + grow >= _innerMin.Y &&
               low.Z - grow <= _innerMax.Z && high.Z + grow >= _innerMin.Z;
    }

    /// <summary>
    /// This class follows one ray through the pieces it crosses.
    /// </summary>
    private sealed class Marcher(Deformation bend, Surface surface, Ray ray, List<Intersection> intersections)
    {
        private readonly V3 _origin = V3.Of(ray.Origin);
        private readonly V3 _direction = V3.Of(ray.Direction);
        private readonly List<Intersection> _found = [];
        private readonly List<Intersection> _again = [];
        private readonly Stack<Chord> _chords = new ();
        private double _tolerance;
        private double _claimFrom;
        private double _claimTo;

        private V3 At(double t) => _origin + _direction * t;

        // Narrows a stretch of the ray to the part between a piece's two end planes, and says whether it now begins
        // or ends at one of them, rather than at the piece's box.
        internal bool WithinTheSlab(
            Piece piece, ref double low, ref double high, out bool fromAPlane, out bool toAPlane)
        {
            double boxLow = low, boxHigh = high;

            fromAPlane = toAPlane = false;

            if (piece.Curve != -1 &&
                !Keep((_origin - piece.C0).Dot(piece.T0), _direction.Dot(piece.T0), false, ref low, ref high))
            {
                return false;
            }

            if (piece.Curve != -2 &&
                !Keep(-(_origin - piece.C1).Dot(piece.T1), -_direction.Dot(piece.T1), true, ref low, ref high))
            {
                return false;
            }

            fromAPlane = low > boxLow;
            toAPlane = high < boxHigh;

            return low < high;
        }

        // Keeps the part of a stretch where a + b t is not below nought, or, strictly, above it.  A slab holds its
        // lower plane and not its upper, so a ray lying in the plane between two pieces is the upper piece's alone:
        // taken as both, every crossing along it was found twice.
        private static bool Keep(double a, double b, bool strictly, ref double low, ref double high)
        {
            if (b == 0)
                return strictly ? a > 0 : a >= 0;

            double t = -a / b;

            if (b > 0)
                low = Math.Max(low, t);
            else
                high = Math.Min(high, t);

            return low < high;
        }

        // Follows the ray through one piece, in chords.  A crossing within a hair of the plane between two pieces --
        // where a shape ends at the spline's start, say, as a shape bent from its end does -- is the piece's whose
        // stretch of the ray begins there: its first chord reaches back by the hair, and the other's last chord stops
        // short by it.  Each taking only what lay within its own chords, rounding lost such a crossing as often as not.
        internal void March(Piece piece, double low, double high, bool fromAPlane, bool toAPlane)
        {
            double hair = 1e-10 * bend._scale / Math.Max(_direction.Length, 1e-300);

            _claimFrom = fromAPlane ? low - hair : low;
            _claimTo = toAPlane ? high - hair : high;

            double width = ray.RadiusAt(Math.Max(low, 0));

            _tolerance = width > 0
                ? Math.Clamp(StrayingInWidths * width, 1e-6 * bend._scale, 1e-2 * bend._scale)
                : Straying * bend._scale;

            V3 first = bend.Inverse(piece, At(low));
            V3 last = bend.Inverse(piece, At(high));

            if (piece.Straight)
            {
                Cross(piece, low, first, high, last, low, high);

                return;
            }

            _chords.Clear();
            _chords.Push(new Chord(low, first, high, last, bend.Inverse(piece, At((low + high) / 2)), 0));

            while (_chords.Count > 0)
            {
                Chord chord = _chords.Pop();
                double middle = (chord.T0 + chord.T1) / 2;
                V3 quarter = bend.Inverse(piece, At((chord.T0 + middle) / 2));
                V3 threeQuarters = bend.Inverse(piece, At((middle + chord.T1) / 2));
                double straying = Math.Max((chord.QM - (chord.Q0 + chord.Q1) * 0.5).Length,
                    Math.Max((quarter - (chord.Q0 * 0.75 + chord.Q1 * 0.25)).Length,
                        (threeQuarters - (chord.Q0 * 0.25 + chord.Q1 * 0.75)).Length));

                if (!bend.MayHoldTheShape(chord, quarter, threeQuarters, straying, _tolerance))
                    continue;

                if (straying > _tolerance && chord.Depth < DeepestHalving)
                {
                    _chords.Push(new Chord(middle, chord.QM, chord.T1, chord.Q1, threeQuarters, chord.Depth + 1));
                    _chords.Push(new Chord(chord.T0, chord.Q0, middle, chord.QM, quarter, chord.Depth + 1));
                }
                else
                    Cross(piece, chord.T0, chord.Q0, chord.T1, chord.Q1, low, high);
            }
        }

        // Asks the shape where a chord crosses it, and keeps each crossing on the chord, found again on the ray.
        private void Cross(Piece piece, double t0, V3 q0, double t1, V3 q1, double low, double high)
        {
            V3 way = q1 - q0;

            if (way.Dot(way) == 0)
                return;

            Ray chord = new (q0.ToPoint(), way.ToVector(), ray.TimeIndex);
            (double near, double far) = bend._innerBox.GetIntersections(chord);

            if (near > far || far < 0 || near > 1)
                return;

            _found.Clear();
            surface.AddIntersections(chord, _found);

            foreach (Intersection crossing in _found)
            {
                double u = crossing.Distance;
                double along = t0 + u * (t1 - t0);

                // Within the chord, or what the piece claims beyond its first or short of its last; the chords inside
                // a piece share their ends exactly, and each keeps where it begins.
                if (!(u >= 0 || (t0 == low && along >= _claimFrom)) || !(u < 1 || t1 == high) ||
                    along < _claimFrom || along >= _claimTo)
                {
                    continue;
                }

                // On a straight piece a chord is the curve, and what it found is exact.
                (Intersection found, double t) = piece.Straight
                    ? (crossing, along)
                    : Refined(piece, crossing, q0 + way * u, along, t1 - t0, low, high);

                // A chord passing a hair inside the shape where the ray passes a hair outside it crosses it twice,
                // and neither crossing is found again nearer the ray.
                if (found is null)
                    continue;

                found.Distance = t;
                intersections.Add(found);
            }
        }

        // A crossing found again on the ray: by the planes touching the surface it was found on, or failing that on
        // three ever shorter chords; none if it is not found again at all.
        private (Intersection, double) Refined(
            Piece piece, Intersection crossing, V3 place, double t, double span, double low, double high)
        {
            Surface struck = crossing.Surface;
            List<Surface> between = ReferenceEquals(struck, surface) ? null : Between(struck, crossing.Portals);

            return ByThePlanes(piece, crossing, struck, between, place, t, span, low, high) is ({ } found, double at)
                ? (found, at)
                : ByShorterChords(piece, crossing, struck, between, place, t, span, low, high);
        }

        // The surfaces between the bent one and one inside it, outermost first -- the instances it was found through
        // among them, taken where the walk up runs out at the top of a shared shape, as a crossing walks out to the
        // world -- or null if the walk does not come to the bent one with every instance passed, or something on the
        // way is bent itself.
        private List<Surface> Between(Surface struck, Portals portals)
        {
            List<Surface> chain = [];
            Surface holder = struck.Parent;

            while (true)
            {
                if (holder is null)
                {
                    if (portals.IsEmpty)
                        return null;

                    holder = portals.Through;
                    portals = portals.Outward;
                }

                if (ReferenceEquals(holder, surface))
                    return portals.IsEmpty ? chain.AsEnumerable().Reverse().ToList() : null;

                if (holder.Deformation is not null)
                    return null;

                chain.Add(holder);
                holder = holder.Parent;
            }
        }

        // Steps along the curve onto the plane touching the surface where the chord met it, asks the surface about one
        // short chord there, and steps onto the plane where that one met it, until the crossing the plane is drawn
        // through lies within a millionth of the shape's size of the ray -- once or twice.  Nothing if a plane is met
        // at a graze, the surface is not found again, or the surface's way down cannot be followed.
        private (Intersection, double)? ByThePlanes(
            Piece piece, Intersection crossing, Surface struck, List<Surface> between, V3 place, double t, double span,
            double low, double high)
        {
            if (!ReferenceEquals(struck, surface) && (between is null || struck.Deformation is not null))
                return null;

            if (NormalAt(struck, between, crossing, place) is not { } normal ||
                OntoThePlane(piece, place, normal, t, span, low, high) is not var (onto, slope, curve))
            {
                return null;
            }

            for (int cycle = 0; cycle < 3; cycle++)
            {
                // The plane strays from the surface by about the square of how far along it the curve was met, over
                // twice the surface's radius there -- taken to be no less than a fiftieth of the shape's size.  The
                // chord asked about is four times that either side, widened sixteenfold while it finds nothing.
                double off = (curve - place).Length;
                double reach = Math.Max(4 * off * off / (0.02 * bend._scale) / Math.Abs(slope), 1e-9 * span);
                (Intersection, double)? met = null;
                double from = 0, to = 0;
                V3 q0 = default, way = default;

                for (int widening = 0; widening < 4 && met is null && reach < span; widening++, reach *= 16)
                {
                    from = Math.Max(low, onto - reach);
                    to = Math.Min(high, onto + reach);

                    if (to <= from)
                        return null;

                    q0 = bend.Inverse(piece, At(from));
                    way = bend.Inverse(piece, At(to)) - q0;
                    met = Nearest(struck, between, crossing, q0, way, curve);
                }

                if (met is not ({ } again, double share))
                    return null;

                place = q0 + way * share;

                if (NormalAt(struck, between, again, place) is not { } facing ||
                    OntoThePlane(piece, place, facing, from + share * (to - from), to - from, low, high) is not
                        var (final, finalSlope, finalCurve))
                {
                    return null;
                }

                if ((finalCurve - place).Length <= 1e-6 * bend._scale)
                    return (again, final);

                crossing = again;
                onto = final;
                slope = finalSlope;
                curve = finalCurve;
            }

            return null;
        }

        // The ray's place nearest a crossing at which the curve meets the plane through the crossing square to the
        // given normal, how fast the curve crosses the plane there, and where it is in the shape's space: Newton's
        // method along the ray, its slope found by a step a thousandth of the span and kept by the secant after.
        // Nothing if the curve runs along the plane, leaves the piece, or does not settle.
        private (double, double, V3)? OntoThePlane(
            Piece piece, V3 place, V3 normal, double t, double span, double low, double high)
        {
            V3 q = bend.Inverse(piece, At(t));
            double side = (q - place).Dot(normal);
            double step = span * 1e-3 * (t + span * 1e-3 <= high ? 1 : -1);
            V3 stepped = bend.Inverse(piece, At(t + step));
            double speed = (stepped - q).Length / Math.Abs(step);
            double slope = ((stepped - place).Dot(normal) - side) / step;

            if (!(Math.Abs(slope) > 0.01 * speed))
                return null;

            for (int pass = 0; pass < 8 && Math.Abs(side) > 1e-13 * bend._scale; pass++)
            {
                double move = -side / slope;
                double next = t + move;

                if (next < low - span * 1e-9 || next > high + span * 1e-9)
                    return null;

                V3 there = bend.Inverse(piece, At(next));
                double nextSide = (there - place).Dot(normal);

                if (move != 0)
                    slope = (nextSide - side) / move;

                if (!(Math.Abs(slope) > 0.01 * speed))
                    return null;

                t = next;
                q = there;
                side = nextSide;
            }

            return Math.Abs(side) <= 1e-12 * bend._scale ? (t, slope, q) : null;
        }

        // The surface's normal where a crossing met it, carried up into the bent surface's own space through the
        // surfaces between, or nothing if there is none to be had.
        private V3? NormalAt(Surface struck, List<Surface> between, Intersection crossing, V3 place)
        {
            int time = ray.TimeIndex;
            Vector normal;

            if (ReferenceEquals(struck, surface))
                normal = surface.SurfaceNormalAt(place.ToPoint(), crossing);
            else
            {
                Point point = place.ToPoint();

                foreach (Surface holder in between)
                    point = holder.InverseTransformAt(time) * point;

                point = struck.InverseTransformAt(time) * point;
                normal = struck.TransposedInverseTransformAt(time) * struck.SurfaceNormalAt(point, crossing);

                for (int index = between.Count - 1; index >= 0; index--)
                    normal = between[index].TransposedInverseTransformAt(time) * normal;
            }

            V3 unit = V3.Of(normal);
            double length = unit.Length;

            return length > 0 && double.IsFinite(length) ? unit / length : null;
        }

        // The crossing of the struck surface on a chord nearest a place, and how far along the chord it is.
        private (Intersection, double)? Nearest(
            Surface struck, List<Surface> between, Intersection crossing, V3 q0, V3 way, V3 place)
        {
            if (way.Dot(way) == 0)
                return null;

            _again.Clear();
            Ask(struck, between, new Ray(q0.ToPoint(), way.ToVector(), ray.TimeIndex));

            Intersection nearest = null;
            double nearestOff = double.PositiveInfinity;

            foreach (Intersection again in _again)
            {
                if (!ReferenceEquals(again.Surface, crossing.Surface) || again.Distance < -0.5 || again.Distance > 1.5)
                    continue;

                double off = (q0 + way * again.Distance - place).Length;

                if (off < nearestOff)
                {
                    nearestOff = off;
                    nearest = again;
                }
            }

            if (nearest is null)
                return null;

            // Found on the surface itself, it has not been stamped by the instance it was first found through.
            if (!ReferenceEquals(struck, surface) && between is not null)
                nearest.Portals = crossing.Portals;

            return (nearest, nearest.Distance);
        }

        // A crossing found again on as many as three ever shorter chords about it, or none if the first finds none:
        // the chord's own, where it strayed across the shape and the ray does not.
        private (Intersection, double) ByShorterChords(
            Piece piece, Intersection crossing, Surface struck, List<Surface> between, V3 place, double t, double span,
            double low, double high)
        {
            double reach = span * 0.1;

            for (int pass = 0; pass < 3; pass++)
            {
                double from = Math.Max(low, t - reach);
                double to = Math.Min(high, t + reach);

                if (to <= from)
                    break;

                V3 q0 = bend.Inverse(piece, At(from));
                V3 way = bend.Inverse(piece, At(to)) - q0;

                if (Nearest(struck, between, crossing, q0, way, place) is not ({ } nearest, double share))
                {
                    if (pass == 0)
                        return (null, t);

                    break;
                }

                place = q0 + way * share;
                t = from + share * (to - from);
                crossing = nearest;
                reach *= 0.02;
            }

            return (crossing, t);
        }

        // Asks only the surface a crossing was found on about a chord, carried down to it through the surfaces
        // between: a crossing is found again on what it was found on, not the whole shape.  Asked of the bent surface
        // itself when it is what was struck, or when the way down to it cannot be followed.
        private void Ask(Surface struck, List<Surface> between, Ray chord)
        {
            if (ReferenceEquals(struck, surface) || between is null)
            {
                surface.AddIntersections(chord, _again);

                return;
            }

            foreach (Surface holder in between)
                chord = holder.InverseTransformAt(chord.TimeIndex).Transform(chord);

            struck.Intersect(chord, _again);
        }
    }

    private readonly record struct Chord(double T0, V3 Q0, double T1, V3 Q1, V3 QM, int Depth);

    /// <summary>
    /// This class holds one piece of the spline: a curve's span turning and twisting no more than eight degrees, or
    /// the straight run before its start or past its end.  Its S0 and S1 are how far along the spline it runs.
    /// </summary>
    private sealed class Piece
    {
        public bool Straight;
        public int Curve;
        public double U0;
        public double U1;
        public double S0;
        public double S1;
        public double Reference;
        public V3 C0;
        public V3 T0;
        public V3 N0;
        public V3 C1;
        public V3 T1;
        public V3 N1;
        public double Turning;
        public BoundingBox Box;
    }

    private struct Place
    {
        public V3 C;
        public V3 T;
        public V3 N;
        public V3 B;
        public V3 K;
        public double Rate;
    }

    /// <summary>
    /// This struct is a spline's curve as a cubic, with its first and second derivatives.
    /// </summary>
    private readonly struct Bezier(V3 p0, V3 p1, V3 p2, V3 p3)
    {
        public static Bezier Of(ISplineCurve curve) => curve switch
        {
            SplineLine line => Line(V3.Of(line.Start), V3.Of(line.End)),
            SplineQuadCurve quad => Raised(V3.Of(quad.Start), V3.Of(quad.Control), V3.Of(quad.End)),
            SplineCubicCurve cubic => new Bezier(
                V3.Of(cubic.Start), V3.Of(cubic.Control1), V3.Of(cubic.Control2), V3.Of(cubic.End)),
            _ => throw new InvalidOperationException("A shape can only be deformed along lines and curves.")
        };

        private static Bezier Line(V3 start, V3 end) =>
            new (start, start + (end - start) / 3, start + (end - start) * (2.0 / 3), end);

        private static Bezier Raised(V3 start, V3 control, V3 end) =>
            new (start, start + (control - start) * (2.0 / 3), end + (control - end) * (2.0 / 3), end);

        public V3 At(double u)
        {
            double v = 1 - u;

            return p0 * (v * v * v) + p1 * (3 * v * v * u) + p2 * (3 * v * u * u) + p3 * (u * u * u);
        }

        public V3 Velocity(double u)
        {
            double v = 1 - u;

            return ((p1 - p0) * (v * v) + (p2 - p1) * (2 * v * u) + (p3 - p2) * (u * u)) * 3;
        }

        public V3 Acceleration(double u) => ((p2 - p1 * 2 + p0) * (1 - u) + (p3 - p2 * 2 + p1) * u) * 6;

        // The unit heading, taken from a little way on where the curve stops for an instant at a control point.
        public V3 Tangent(double u)
        {
            V3 velocity = Velocity(u);
            double speed = velocity.Length;
            double size = (p3 - p0).Length + (p1 - p0).Length + (p2 - p1).Length;

            if (speed > size * 1e-9)
                return velocity / speed;

            double step = u < 0.5 ? 1e-6 : -1e-6;
            V3 chord = (At(u + step) - At(u)) * Math.Sign(step);

            return chord.Unit;
        }
    }
}

/// <summary>
/// This struct is a point or a direction in three dimensions, allocating nothing, for the arithmetic a deformation
/// does many times a ray.
/// </summary>
internal readonly struct V3(double x, double y, double z)
{
    public double X { get; } = x;
    public double Y { get; } = y;
    public double Z { get; } = z;

    public static V3 Of(NumberTuple tuple) => new (tuple.X, tuple.Y, tuple.Z);

    public static V3 operator +(V3 a, V3 b) => new (a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V3 operator -(V3 a, V3 b) => new (a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V3 operator -(V3 a) => new (-a.X, -a.Y, -a.Z);
    public static V3 operator *(V3 a, double s) => new (a.X * s, a.Y * s, a.Z * s);
    public static V3 operator /(V3 a, double s) => new (a.X / s, a.Y / s, a.Z / s);

    public double Dot(V3 b) => X * b.X + Y * b.Y + Z * b.Z;

    public V3 Cross(V3 b) => new (Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);

    public double Length => Math.Sqrt(Dot(this));

    public V3 Unit => this / Length;

    public static V3 Min(V3 a, V3 b) => new (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));

    public static V3 Max(V3 a, V3 b) => new (Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));

    public Point ToPoint() => new (X, Y, Z);

    public Vector ToVector() => new (X, Y, Z);
}
