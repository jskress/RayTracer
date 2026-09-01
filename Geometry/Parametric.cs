using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Core;
using RayTracer.Fields;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a parametric surface: a sheet whose every point is written down as three
/// pieces of arithmetic in two parameters, <c>u</c> and <c>v</c>.
/// <para>
/// Where an <see cref="Isosurface"/> says where a shape *is* -- everywhere one function comes out at
/// nought -- this says what a shape *is made of*, by naming the point at every place on it.  The two
/// suit different things: an isosurface is at home with shapes defined by a rule about space, and this
/// with shapes that are swept, wound or grown -- shells, horns, twisted columns, and the sculptural
/// forms that are easy to walk around and hard to write as a rule.
/// </para>
/// <para>
/// **It is found by narrowing the parameters rather than by stepping along the ray.**  Any rectangle
/// of <c>u</c> and <c>v</c> can be bounded -- the three expressions are asked what they may come to
/// over it, which gives a box the whole of that piece of the sheet must lie inside.  A ray missing
/// that box misses every point of the sheet the rectangle stands for, so the rectangle is put down
/// unopened.  That is what makes this affordable, and it is the same discipline the isosurface uses,
/// turned from the ray onto the parameters.  **Nothing here is ever turned into triangles**; the
/// bound is a proof rather than an approximation.
/// </para>
/// <para>
/// **A parametric surface is a sheet and not a solid.**  It has no inside, so it cannot be filled,
/// refracted through, or usefully combined -- the same as a <see cref="Parallelogram"/>, a
/// <see cref="Disc"/> or a patch.  A shell looks solid and is not.
/// </para>
/// </summary>
public class Parametric : Surface
{
    /// <summary>How deep the parameters are narrowed before a piece is given up on.</summary>
    private const int MaximumDepth = 26;

    /// <summary>A whisker of slack on a piece's box, so a ray grazing it is not lost to arithmetic.</summary>
    private const double BoxPadding = 1e-6;

    /// <summary>How small a piece must get, as a fraction of the whole sheet's size, before the
    /// crossing in it is solved for rather than narrowed further.
    /// <para>
    /// **This is a fraction and not a length.**  A length would be a different demand on every sheet:
    /// the same 0.08 that suits a ball a couple of units across asks a shell four units across for
    /// twice the halvings in each parameter, for no better an answer.
    /// </para>
    /// <para>
    /// A coarser 0.08 of the diagonal renders half again as fast and is still wrong: it puts back the
    /// self-shadowing the tight solving was there to remove, on rim pixels where the ball is brightly
    /// lit and the sheet comes out at the ambient.  0.04 is honest and slightly quicker; 0.02 is what
    /// is set, for the margin.
    /// </para></summary>
    private const double SolveWithinFraction = 0.02;

    /// <summary>How many steps the solving is given before it is called a miss.</summary>
    private const int SolveSteps = 30;

    /// <summary>
    /// How close the solver drives the gap before it is content.  **This is far tighter than the
    /// accuracy the crossing is wanted to**, and deliberately so: stopping at the accuracy leaves the
    /// point up to that far *inside* the sheet, and the shadow ray then leaving it meets the sheet it
    /// is standing on.  Nine pixels of a ball came out lit by nothing but the ambient for exactly that
    /// reason, and they read as missed crossings rather than as what they were.  Newton doubles its
    /// correct figures each step, so the extra depth is two steps or so.
    /// </summary>
    private const double SolveTo = 1e-12;

    /// <summary>How small the solving's determinant may get before there is nothing to solve.
    /// <para>
    /// **This has to be far smaller than the usual epsilon.**  The determinant is the ray's direction
    /// against the two the sheet runs in, so it is genuinely tiny wherever the ray grazes the sheet or
    /// the sheet folds to a point -- at the poles of a ball, every ray has one.  Refusing at a
    /// millionth threw away solvable crossings all over the shape and left it speckled.
    /// </para></summary>
    private const double SmallestDeterminant = 1e-14;

    /// <summary>
    /// This property reports that a parametric surface is a sheet, which decides which side of it a
    /// ray leaving it starts on.  Which way its normal points is a consequence of how its arithmetic
    /// was written -- swapping a sign in one of the three expressions reverses it -- and no author
    /// should have to know that to get a lit surface.
    /// </summary>
    public override bool IsASheet => true;

    /// <summary>
    /// This property holds the arithmetic giving a point's X, in terms of <c>u</c> and <c>v</c>.
    /// </summary>
    public FieldExpression X { get; set; }

    /// <summary>
    /// This property holds the arithmetic giving a point's Y.
    /// </summary>
    public FieldExpression Y { get; set; }

    /// <summary>
    /// This property holds the arithmetic giving a point's Z.
    /// </summary>
    public FieldExpression Z { get; set; }

    /// <summary>
    /// This property holds the span of <c>u</c> the sheet is drawn over.
    /// </summary>
    public Interval UDomain { get; set; } = new () { Start = 0, End = 1 };

    /// <summary>
    /// This property holds the span of <c>v</c>.
    /// </summary>
    public Interval VDomain { get; set; } = new () { Start = 0, End = 1 };

    /// <summary>
    /// This property holds how small a piece of the sheet must get before its middle is taken for a
    /// crossing, in the surface's own units.
    /// </summary>
    public double Accuracy { get; set; } = 0.0001;

    private FieldFunction _x;
    private FieldFunction _y;
    private FieldFunction _z;
    private FieldGradient _slopesOfX;
    private FieldGradient _slopesOfY;
    private FieldGradient _slopesOfZ;
    private double _solveWithin;

    /// <summary>
    /// This method compiles the three expressions and their slopes, so that neither the narrowing nor
    /// the shading walks a tree.  The slopes are what a normal is made of: the sheet's normal at a
    /// place is the two directions it runs in there, crossed.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (X is null || Y is null || Z is null)
            throw new Exception("A parametric surface needs all three of x, y and z.");

        _x = FieldFunction.Compile(X);
        _y = FieldFunction.Compile(Y);
        _z = FieldFunction.Compile(Z);
        _slopesOfX = FieldGradient.Of(X);
        _slopesOfY = FieldGradient.Of(Y);
        _slopesOfZ = FieldGradient.Of(Z);

        // How small a piece has to get before it is solved rather than split again, worked out from
        // how big the whole sheet is.  A ball and a shell then ask for the same number of halvings
        // instead of the shell asking for many times more.
        (FieldRange x, FieldRange y, FieldRange z) = BoxOver(RangeOf(UDomain), RangeOf(VDomain));
        double across = Math.Sqrt(
            Squared(x.High - x.Low) + Squared(y.High - y.Low) + Squared(z.High - z.Low));

        _solveWithin = across * SolveWithinFraction;
    }

    /// <summary>
    /// This method returns a number times itself, guarding against a bound that says nothing.
    /// </summary>
    private static double Squared(double value)
    {
        return double.IsFinite(value) ? value * value : 0;
    }

    /// <summary>
    /// This method returns the box the whole sheet lies in, which is what the three expressions come
    /// to over the whole of both parameters.  It costs one bound rather than any sampling, and it is
    /// exactly the same question asked of the whole that the narrowing asks of the parts.
    /// </summary>
    /// <returns>The box the sheet lies in.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        (FieldRange u, FieldRange v) = (RangeOf(UDomain), RangeOf(VDomain));
        (FieldRange x, FieldRange y, FieldRange z) = BoxOver(u, v);

        if (x.IsAnywhere || y.IsAnywhere || z.IsAnywhere)
            return null;

        BoundingBox box = new ();

        box.Add(new Point(x.Low, y.Low, z.Low));
        box.Add(new Point(x.High, y.High, z.High));

        return box;
    }

    /// <summary>
    /// This method finds where a ray crosses the sheet, by narrowing the parameters until a piece of
    /// it is smaller than the accuracy asked for.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any crossings to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double length = ray.Direction.Magnitude;

        if (length == 0)
            return;

        // The same reasoning as the isosurface's: the narrowing works in the space the expressions are
        // written in, so the ray is followed with a direction of unit length and the answer scaled back
        // at the end.  Otherwise the accuracy would mean something different at every scale.
        Ray unitRay = new (ray.Origin, ray.Direction / length, ray.TimeIndex);
        int before = intersections.Count;

        Narrow(unitRay, RangeOf(UDomain), RangeOf(VDomain), MaximumDepth, length, intersections);
        WeedOutRepeats(intersections, before, Accuracy / length);
    }

    /// <summary>
    /// This method drops crossings that are really one crossing found more than once, which the
    /// overlap between neighbouring pieces allows.  Two at the same distance along one ray are the
    /// same place on the sheet; keeping both would have a ray enter and leave in the same breath.
    /// </summary>
    /// <param name="intersections">The list of crossings.</param>
    /// <param name="from">Where this surface's own crossings begin in it.</param>
    /// <param name="together">How near two must be to be the same one.</param>
    private static void WeedOutRepeats(
        List<Intersection> intersections, int from, double together)
    {
        if (intersections.Count - from < 2)
            return;

        intersections.Sort(from, intersections.Count - from, DistanceOrder.Instance);

        for (int index = intersections.Count - 1; index > from; index--)
        {
            if (Math.Abs(intersections[index].Distance - intersections[index - 1].Distance) <= together)
                intersections.RemoveAt(index);
        }
    }

    /// <summary>
    /// This method narrows one rectangle of parameters, either putting it down unopened or splitting
    /// it and asking the same of both halves.
    /// <para>
    /// **Crossings behind the origin are kept**, which every surface here must do: a ray cast from
    /// inside a shape, or one asked which crossings lie behind it, needs them.  See the note on that
    /// in the path-source work.
    /// </para>
    /// </summary>
    private void Narrow(
        Ray ray, FieldRange u, FieldRange v, int depth, double length,
        List<Intersection> intersections)
    {
        (FieldRange x, FieldRange y, FieldRange z) = BoxOver(u, v);

        // A bound saying nothing rules nothing out, so such a piece is still opened.
        if (!x.IsAnywhere && !y.IsAnywhere && !z.IsAnywhere)
        {
            BoundingBox box = new ();

            box.Add(new Point(x.Low, y.Low, z.Low));
            box.Add(new Point(x.High, y.High, z.High));
            box.Expand(BoxPadding);

            if (!box.IsHitBy(ray))
                return;

            // Small enough to go looking for the crossing itself.  **Narrowing alone cannot get
            // there**: the box has to shrink below the accuracy in all three directions, which for a
            // sheet a couple of units across takes some thirty halvings of each parameter, and the
            // first attempt at this simply ran out of depth and drew nothing at all.  So the
            // narrowing only has to get close enough for one crossing to be alone in the piece, and
            // then the crossing is solved for exactly.
            if (box.Maximum.X - box.Minimum.X <= _solveWithin &&
                box.Maximum.Y - box.Minimum.Y <= _solveWithin &&
                box.Maximum.Z - box.Minimum.Z <= _solveWithin)
            {
                Solve(ray, u, v, length, intersections);

                return;
            }
        }

        if (depth == 0)
            return;

        // The wider parameter is the one worth halving: splitting the narrow one again would shrink
        // the box hardly at all and double the work.
        if (u.Width >= v.Width)
        {
            Narrow(ray, new FieldRange(u.Low, u.Middle), v, depth - 1, length, intersections);
            Narrow(ray, new FieldRange(u.Middle, u.High), v, depth - 1, length, intersections);
        }
        else
        {
            Narrow(ray, u, new FieldRange(v.Low, v.Middle), depth - 1, length, intersections);
            Narrow(ray, u, new FieldRange(v.Middle, v.High), depth - 1, length, intersections);
        }
    }

    /// <summary>
    /// This method looks for the exact crossing inside one small piece of the sheet, and records it if
    /// there is one.
    /// <para>
    /// What is solved is <c>origin + t·direction = S(u, v)</c>, three equations in three unknowns, by
    /// Newton's method from the middle of the piece.  **The slopes it needs are the ones already to
    /// hand**: the same symbolic derivatives the normal is made of, so there is no step size to choose
    /// and nothing to tune.
    /// </para>
    /// <para>
    /// **The stepping is kept inside the piece it started in.**  Every crossing lies in exactly one
    /// piece, so a piece has only ever to find its own, and holding it there stops one crossing being
    /// reported by all its neighbours too.  Letting the step wander out and then throwing the answer
    /// away is not the same thing and does not work: the piece that owned that crossing might not
    /// find it either, and then it is reported by nobody and a pixel goes missing.
    /// </para>
    /// </summary>
    private void Solve(
        Ray ray, FieldRange uRange, FieldRange vRange, double length,
        List<Intersection> intersections)
    {
        (double u, double v) = (uRange.Middle, vRange.Middle);
        Point where = PointAt(u, v);
        double t = (where - ray.Origin).Dot(ray.Direction);

        for (int step = 0; step < SolveSteps; step++)
        {
            where = PointAt(u, v);

            Vector gap = ray.Origin + ray.Direction * t - where;

            if (gap.Magnitude <= SolveTo)
            {
                // A crossing landing on the join between two pieces is found by both, since the
                // clamping includes the ends; the weeding afterwards drops the repeat.  A repeat is
                // much the cheaper mistake -- a hole shows and a repeat does not.
                intersections.Add(new ParametricIntersection(this, t / length, u, v));

                return;
            }

            (double wasT, double wasU, double wasV) = (t, u, v);
            (Vector alongU, Vector alongV) = SlopesAt(u, v);

            // The three columns of the derivative: how the gap moves with t, with u and with v.
            Vector byT = ray.Direction;
            double determinant = byT.Dot(alongU.Cross(alongV));

            if (Math.Abs(determinant) < SmallestDeterminant)
                return;

            // Cramer's rule, solving for the step that closes the gap.
            t -= gap.Dot(alongU.Cross(alongV)) / determinant;
            u += byT.Dot(gap.Cross(alongV)) / determinant;
            v += byT.Dot(alongU.Cross(gap)) / determinant;

            // **The step is kept inside the piece.**  Every crossing lies in exactly one piece, so a
            // piece has only ever to find its own; letting the step wander out and then throwing the
            // answer away is how a crossing came to be reported by nobody at all.
            u = Math.Clamp(u, uRange.Low, uRange.High);
            v = Math.Clamp(v, vRange.Low, vRange.High);

            // A piece with no crossing of its own pins against its edge and stops moving; there is
            // nothing further to learn from it, and it would otherwise burn every step it has.  This
            // is measured *after* the clamping and has to be: a pinned step is a big step that goes
            // nowhere, and measured before, it looks like progress and never stops.
            if (Math.Abs(t - wasT) + Math.Abs(u - wasU) + Math.Abs(v - wasV) < SolveTo)
                break;
        }

        // It did not converge all the way, but it may still have come inside the accuracy the crossing
        // is wanted to; that is worth keeping rather than losing the crossing altogether.
        if ((ray.Origin + ray.Direction * t - PointAt(u, v)).Magnitude <= Accuracy)
            intersections.Add(new ParametricIntersection(this, t / length, u, v));
    }

    /// <summary>
    /// This method returns the point the sheet has at a place on it.
    /// </summary>
    private Point PointAt(double u, double v)
    {
        return new Point(_x.Evaluate(u, v, 0), _y.Evaluate(u, v, 0), _z.Evaluate(u, v, 0));
    }

    /// <summary>
    /// This method returns the two directions the sheet runs in at a place on it.
    /// </summary>
    private (Vector AlongU, Vector AlongV) SlopesAt(double u, double v)
    {
        return (
            new Vector(
                _slopesOfX.Along(FieldAxis.X, u, v, 0),
                _slopesOfY.Along(FieldAxis.X, u, v, 0),
                _slopesOfZ.Along(FieldAxis.X, u, v, 0)),
            new Vector(
                _slopesOfX.Along(FieldAxis.Y, u, v, 0),
                _slopesOfY.Along(FieldAxis.Y, u, v, 0),
                _slopesOfZ.Along(FieldAxis.Y, u, v, 0)));
    }

    /// <summary>
    /// This method returns the box a rectangle of parameters puts the sheet inside.
    /// </summary>
    private (FieldRange X, FieldRange Y, FieldRange Z) BoxOver(FieldRange u, FieldRange v)
    {
        FieldRange none = FieldRange.Just(0);

        return (X.Bound(u, v, none), Y.Bound(u, v, none), Z.Bound(u, v, none));
    }

    /// <summary>
    /// This method returns the normal at a crossing, which is the two directions the sheet runs in
    /// there, crossed.
    /// <para>
    /// **Those two directions are worked out symbolically rather than by sampling**, because the
    /// engine already differentiates a field expression for an isosurface's gradient.  So there is no
    /// step size to choose here and no accuracy to lose to one.
    /// </para>
    /// </summary>
    /// <param name="point">The point the normal is wanted at.</param>
    /// <param name="intersection">The crossing being shaded, which carries where on the sheet it is.</param>
    /// <returns>The normal to the sheet there.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        if (intersection is not ParametricIntersection place)
            return new Vector(0, 1, 0);

        (double u, double v) = (place.U, place.V);
        (Vector alongU, Vector alongV) = SlopesAt(u, v);
        Vector normal = alongU.Cross(alongV);

        // Where the sheet folds to a point -- the tip of a horn, the poles of a ball -- the two
        // directions line up and their cross product vanishes.  There is no normal there to give, so
        // one is borrowed from just beside it rather than handing back nothing.
        return normal.Magnitude.Near(0) ? NormalBeside(u, v) : normal.Unit;
    }

    /// <summary>
    /// This method finds a normal a little way off a place the sheet has none of its own.
    /// </summary>
    private Vector NormalBeside(double u, double v)
    {
        double step = Math.Max(UDomain.End - UDomain.Start, VDomain.End - VDomain.Start) * 0.001;
        (Vector alongU, Vector alongV) = SlopesAt(u + step, v + step);
        Vector normal = alongU.Cross(alongV);

        return normal.Magnitude.Near(0) ? new Vector(0, 1, 0) : normal.Unit;
    }

    /// <summary>
    /// This method returns an interval as the range the field arithmetic works in.
    /// </summary>
    private static FieldRange RangeOf(Interval interval)
    {
        return new FieldRange(interval.Start, interval.End);
    }

    /// <summary>
    /// This class orders crossings by how far along the ray they are, which is all the weeding needs
    /// -- the full ordering a scene uses brings in which surface was built first, and these are all
    /// the same surface.
    /// </summary>
    private class DistanceOrder : IComparer<Intersection>
    {
        internal static readonly DistanceOrder Instance = new ();

        public int Compare(Intersection left, Intersection right)
        {
            return left!.Distance.CompareTo(right!.Distance);
        }
    }
}
