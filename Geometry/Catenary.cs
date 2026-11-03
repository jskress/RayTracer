using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class hangs a cable between two places: the curve a rope, a chain or a wire of even weight takes under its
/// own weight, a catenary, y = a cosh((x - x0) / a) + c in the upright plane through the two ends.  It is handed back
/// as a <see cref="Spline"/>, so anything that goes along a spline -- a tube, a rope, a chain, a sweep, a placing
/// `along` it -- may hang.
/// <para>
/// **How it hangs is said by its sag or by its length**: the sag, how far its middle hangs below the straight line
/// between its ends; or the length of the cable, which must be longer than the distance between them.  Either fixes
/// a, the catenary's one shape: a long cable on a short span hangs deep and narrow, a short one nearly straight.
/// </para>
/// <para>
/// **The spline is cubic Hermite pieces of the exact curve**, each set by the curve's own place and slope at its
/// ends, the pieces even in length along the curve and as many as its turning asks -- one for every eight degrees it
/// turns, and never fewer than eight -- so the spline lies within a few millionths of its length of the catenary.
/// </para>
/// </summary>
public static class Catenary
{
    // How far a piece of the spline may turn, in radians, and the fewest pieces it is made of.
    private const double MostTurnEach = 8 * Math.PI / 180;
    private const int FewestPieces = 8;

    /// <summary>
    /// This method hangs a cable between two places, by its sag or by its length; exactly one of the two must be
    /// given.
    /// </summary>
    /// <param name="from">Where the cable hangs from.</param>
    /// <param name="to">Where it hangs to.</param>
    /// <param name="sag">How far its middle hangs below the straight line between its ends, or <c>null</c>.</param>
    /// <param name="length">How long the cable is, or <c>null</c>.</param>
    /// <returns>The curve it hangs in, as a spline.</returns>
    public static Spline Hung(Point from, Point to, double? sag, double? length)
    {
        if ((sag is null) == (length is null))
            throw new Exception("A catenary hangs by its sag or by its length; say one of them.");

        Vector across = new (to.X - from.X, 0, to.Z - from.Z);
        double span = across.Magnitude;
        double rise = to.Y - from.Y;
        double scale = Math.Max(span, Math.Abs(rise));

        if (span <= scale * 1e-9)
        {
            throw new Exception(
                "A catenary's ends stand one straight above the other, and a cable between them hangs straight down; " +
                "draw it as a line.");
        }

        if (sag is <= 0)
            throw new Exception("A catenary's sag must be more than nought.");

        if (length is { } said && said <= Math.Sqrt(span * span + rise * rise) * (1 + 1e-9))
            throw new Exception("A catenary must be longer than the distance between its ends, or it cannot hang.");

        double a = length is { } cable ? ShapeForLength(span, rise, cable) : ShapeForSag(span, rise, sag!.Value);
        double middle = MiddleOf(span, rise, a);
        double low = from.Y - a * Math.Cosh(-middle / a);
        Vector way = across / span;

        Point PlaceAt(double x) => new (from.X + way.X * x, low + a * Math.Cosh((x - middle) / a), from.Z + way.Z * x);
        Vector SlopeAt(double x) => new (way.X, Math.Sinh((x - middle) / a), way.Z);

        // Even in length along the curve: the length from its lowest place to x is a sinh((x - middle) / a).
        double startLength = a * Math.Sinh(-middle / a);
        double endLength = a * Math.Sinh((span - middle) / a);
        double turning = Math.Abs(Math.Atan(Math.Sinh((span - middle) / a)) - Math.Atan(Math.Sinh(-middle / a)));
        int pieces = Math.Max(FewestPieces, (int) Math.Ceiling(turning / MostTurnEach));
        Spline spline = new () { Start = PlaceAt(0) };
        double previous = 0;

        for (int piece = 1; piece <= pieces; piece++)
        {
            double along = startLength + (endLength - startLength) * piece / pieces;
            double x = piece == pieces ? span : middle + a * Math.Asinh(along / a);
            double third = (x - previous) / 3;

            spline.Segments.Add(new SplineSegmentSpec
            {
                Control1 = PlaceAt(previous) + SlopeAt(previous) * third,
                Control2 = PlaceAt(x) - SlopeAt(x) * third,
                End = piece == pieces ? to : PlaceAt(x)
            });
            previous = x;
        }

        return spline;
    }

    /// <summary>
    /// This method returns where, along the span from its first end, the catenary of a given shape through both ends
    /// is lowest -- its x0, which may lie beyond either end when one end is much the higher.
    /// </summary>
    private static double MiddleOf(double span, double rise, double a)
    {
        double half = span / (2 * a);

        return span / 2 - a * Math.Asinh(rise / (2 * a * Math.Sinh(half)));
    }

    /// <summary>
    /// This method finds the shape of the catenary of a given length between two ends: a such that the cable's
    /// length over its span, squared less its rise squared, is 2a sinh(span / 2a) -- by bisection on sinh(t) / t,
    /// which climbs from one as t, half the span over a, grows.
    /// </summary>
    private static double ShapeForLength(double span, double rise, double length)
    {
        double wanted = Math.Sqrt(length * length - rise * rise) / span;
        double low = 0, high = 1;

        while (Math.Sinh(high) / high < wanted)
            high *= 2;

        for (int round = 0; round < 200; round++)
        {
            double middle = (low + high) / 2;

            if (middle <= 0 || Math.Sinh(middle) / middle < wanted)
                low = middle;
            else
                high = middle;
        }

        return span / (low + high);
    }

    /// <summary>
    /// This method finds the shape of the catenary between two ends whose middle hangs a given depth below the
    /// straight line between them: the sag shrinks as a grows, so a is found by bisection on its logarithm.
    /// </summary>
    private static double ShapeForSag(double span, double rise, double sag)
    {
        double SagFor(double a)
        {
            double middle = MiddleOf(span, rise, a);
            double low = -a * Math.Cosh(-middle / a);

            return rise / 2 - (low + a * Math.Cosh((span / 2 - middle) / a));
        }

        double small = Math.Log(span * 1e-6), large = Math.Log(span * 1e6);

        for (int round = 0; round < 200; round++)
        {
            double middle = (small + large) / 2;

            // A shape so tight that cosh runs past what a number holds hangs deeper than any sag: grow it.
            if (!(SagFor(Math.Exp(middle)) <= sag))
                small = middle;
            else
                large = middle;
        }

        return Math.Exp((small + large) / 2);
    }
}
