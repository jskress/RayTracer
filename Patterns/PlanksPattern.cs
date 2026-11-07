using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the planks pattern: boards lying side by side, each running along X, butted end to end, with
/// a seam between every two.
/// <para>
/// A seam reads nought and a board a number of its own, from a half up to one, so a color map shades board from board
/// above a half and colors the seam below it; and the board rises into the seam smoothly across the seam's width, so
/// as a roughening the seam is a rounded groove rather than a cliff no slope can be taken across.  Brick has the
/// layout but neither: every brick is the same brick, and its joints are steps.
/// </para>
/// <para>
/// The boards lie side by side both across Y and across Z, as a brick's courses do, so the pattern lays a floor (whose
/// boards lie across Z) and a wall (across Y) alike, and anything curving between the two.  Where only one way
/// is wanted -- a hull's sides, whose planks lie across Y while the hull bends round in Z -- a depth far larger than
/// the thing leaves the other way with no seam in it.
/// </para>
/// <para>
/// A board's middle stands on nought both across Y and across Z, so that a floor at nought or a wall through it lies
/// along the middle of a row of boards rather than in a seam.  Laid from a seam, the commonest floor there is came out
/// seam from end to end.
/// </para>
/// <para>
/// The butts are staggered by the golden ratio: each row's start is moved along by 0.618 of a board from the row
/// before, so that no row ever lines up with another: no two butts in neighboring rows stand within 0.38 of a board
/// of each other, nor two rows apart within 0.23, nor three within 0.15.
/// </para>
/// </summary>
public class PlanksPattern : Pattern
{
    // The golden ratio's fraction, and the silver ratio's: how far along a row's butts move with each row across Y,
    // and with each across Z.  Two irrationals, so that rows stepped both ways never fall back into line.
    private const double AcrossY = 0.6180339887498949;
    private const double AcrossZ = 0.4142135623730950;

    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports, which is none: it gives a number
    /// in the [0, 1] interval for a color map.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This property specifies the size of a board: how long it runs along X, how wide it is across Y and how wide
    /// across Z.
    /// </summary>
    public Vector PlankSize { get; set; } = new (4, 0.15, 0.15);

    /// <summary>
    /// This property specifies how wide a seam is: the whole width over which a board's edge rounds down into it.
    /// </summary>
    public double SeamSize { get; set; } = 0.008;

    /// <summary>
    /// A seam is the finest thing planks draw, and the first to alias.
    /// </summary>
    protected override double FinestDetail => SeamSize;

    /// <summary>
    /// This method works out the pattern's value at a point: nought on a seam, the board's own number on the board,
    /// and between them across the seam's width a rounded rise from the one to the other.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        double length = PlankSize.X;
        double width = PlankSize.Y;
        double depth = PlankSize.Z;
        double row = Math.Floor(point.Y / width + 0.5);
        double layer = Math.Floor(point.Z / depth + 0.5);
        double shift = Fraction(AcrossY * row + AcrossZ * layer) * length;
        double along = point.X - shift;
        double board = Math.Floor(along / length);
        double edge = Math.Min(
            Math.Min(FromEdge(along / length - board, length), FromEdge(point.Y / width + 0.5 - row, width)),
            FromEdge(point.Z / depth + 0.5 - layer, depth));
        double own = 0.5 + 0.5 * Field.DrawFor((int) (long) layer, (int) (long) row, (int) (long) board, 0x504C);
        double half = SeamSize * 0.5;

        if (edge >= half)
            return own;

        double share = edge / half;

        return own * share * share * (3 - 2 * share);
    }

    // How far a place lies inside its cell, from whichever of the cell's two edges is nearer, given how far across the
    // cell it is as a share.
    private static double FromEdge(double share, double size)
    {
        return Math.Max(0, Math.Min(share, 1 - share)) * size;
    }

    // The fractional part of a number, in [0, 1) whatever its sign.
    private static double Fraction(double value) => value - Math.Floor(value);

    /// <summary>
    /// This method returns whether this pattern's details match another's.
    /// </summary>
    /// <param name="pattern">The pattern to match against.</param>
    /// <returns><c>true</c>, if the details match, or <c>false</c>, if not.</returns>
    protected override bool DetailsMatch(Pattern pattern)
    {
        PlanksPattern other = (PlanksPattern) pattern;

        return PlankSize.Matches(other.PlankSize) && SeamSize.Near(other.SeamSize) && base.DetailsMatch(pattern);
    }
}
