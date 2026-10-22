using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This class creates the transform that puts a thing on a spline: its origin at a place along it, its +X running
/// the way the spline runs there -- the way the vehicles, the trains and the aircraft are all built to face -- and
/// its +Y up.  Up is the world's, as far as the spline lets it be, unless the thing is <c>banked</c>: then it is the
/// way a rider would feel to be up, leaning into the bend as a turning aircraft does or a banked track is laid, by
/// how fast it is going round how tight a bend.
/// </summary>
public class AlongCreator : TransformCreator
{
    /// <summary>
    /// This is how fast a thing falls, in the meters and seconds the libraries are built in, which is what says how
    /// far a thing going round a bend at a speed leans into it.
    /// </summary>
    public const double Gravity = 9.81;

    // How long before and after a moment a banked thing's speed is measured over.
    private const double Moment = 1e-3;

    /// <summary>
    /// This property holds the term that names the spline.
    /// </summary>
    public Term SplineTerm { get; init; }

    /// <summary>
    /// This property holds the term for how far along: a share of the spline's length, or, <see cref="ByDistance"/>,
    /// a distance.
    /// </summary>
    public Term AmountTerm { get; init; }

    /// <summary>
    /// This property notes whether the amount is a distance rather than a share of the length.
    /// </summary>
    public bool ByDistance { get; init; }

    /// <summary>
    /// This property notes whether the thing leans into the bends it goes round.
    /// </summary>
    public bool Banked { get; init; }

    /// <summary>
    /// This property holds the term for how far apart the thing's two ends ride on the spline, or null for a thing
    /// that stands on it at one place.  A rigid vehicle rides on its end axles: round a bend both are on the line,
    /// its middle a little inside it and its heading from one to the other, rather than its middle on the line and
    /// its ends swung off it.
    /// </summary>
    public Term WheelbaseTerm { get; init; }

    /// <summary>
    /// This method creates the transform, which has no part to be applied: a motion is a share of a transform, and
    /// a place on a spline has no share.  A thing moved along a spline is moved in terms of the time.
    /// </summary>
    public override Matrix ResolveAt(RenderContext context, Variables variables, double fraction)
    {
        if (fraction != 1)
        {
            throw new TokenException(
                "A motion is a share of a transform, and a place along a spline has no share: move a thing along " +
                "a spline in terms of the time instead.")
            {
                Token = SplineTerm.ErrorToken
            };
        }

        if (SplineTerm.GetValue(variables) is not Spline spline)
            throw new TokenException("What follows \"along\" must be a spline.") { Token = SplineTerm.ErrorToken };

        SplineMeasure measure = spline.Measure;
        double distance = DistanceAt(variables, measure);
        Point place = measure.PointAt(distance);
        Vector forward = measure.HeadingAt(distance);
        Vector up = new (0, 1, 0);

        if (WheelbaseTerm?.GetValue<double>(variables) is > 0 and var wheelbase)
        {
            Point front = measure.PointAt(distance + wheelbase / 2);
            Point rear = measure.PointAt(distance - wheelbase / 2);
            Vector chord = front - rear;

            place = rear + chord / 2;

            if (chord.Magnitude > wheelbase * 1e-6)
                forward = chord.Unit;
        }

        if (Banked)
        {
            // The rider is pressed toward the inside of the bend, by speed squared times the curvature, as well as
            // down by gravity: what feels up is the way opposite the two together.
            double speed = SpeedAt(variables, measure, distance);

            up = measure.BendAt(distance) * (speed * speed) + new Vector(0, Gravity, 0);
        }

        up -= forward * up.Dot(forward);

        // Straight up the spline, or straight down it, there is no up to be had across it; the world's Z stands in.
        if (up.Magnitude < 1e-9)
            up = new Vector(0, 0, 1) - forward * forward.Z;

        up = up.Unit;

        Vector side = forward.Cross(up);

        return new Matrix([
            forward.X, up.X, side.X, place.X,
            forward.Y, up.Y, side.Y, place.Y,
            forward.Z, up.Z, side.Z, place.Z,
            0, 0, 0, 1
        ]);
    }

    private double DistanceAt(Variables variables, SplineMeasure measure)
    {
        double amount = AmountTerm.GetValue<double>(variables);

        return ByDistance ? amount : amount * measure.Length;
    }

    // How fast the place moves along the spline at this moment: the distance a moment either side, the time shifted
    // in a scope of its own.  Nothing that is not moved by the time has any speed.
    private double SpeedAt(Variables variables, SplineMeasure measure, double distance)
    {
        if (variables.GetValue("time", typeof(double)) is not double time)
            return 0;

        Variables later = new (variables);

        later.SetValue("time", time + Moment);

        return (DistanceAt(later, measure) - distance) / Moment;
    }

    protected override Matrix CreateTransform(RenderContext context, double[] doubles, NumberTuple[] tuples) =>
        throw new InvalidOperationException("A place along a spline is worked out whole, from its spline.");
}
