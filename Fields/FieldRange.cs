namespace RayTracer.Fields;

/// <summary>
/// This structure holds a range of numbers a field function might take, from the lowest it could
/// reach to the highest.
/// <para>
/// It is what lets a marcher skip: given a box of space, the range of the field over that whole box
/// can be worked out without visiting a single point inside it, and if that range does not reach the
/// value that makes the surface then the surface is provably not in there at all.  Everything a
/// marcher would otherwise have to be told -- how finely to step, how steep the function is allowed
/// to get -- follows from that instead of from a number the author of the scene had to guess.  POV-Ray
/// asks for the guess by name, as <c>max_gradient</c>, and its documentation's advice is to raise it
/// until the speckles go away; a bound computed from the function itself has nothing to tune.
/// </para>
/// <para>
/// A range answering "anywhere at all" is a perfectly good answer and always a safe one -- it says
/// only that nothing can be skipped on its account -- so a rule that would have to guess says that
/// instead.  Being a value type matters: bounds are worked out for every span of every ray, so one
/// that allocated would allocate millions of times a frame.
/// </para>
/// </summary>
public readonly struct FieldRange
{
    /// <summary>
    /// This range covers every number there is, and is what a rule that cannot say anything useful
    /// gives back.
    /// </summary>
    public static readonly FieldRange Anywhere =
        new (double.NegativeInfinity, double.PositiveInfinity);

    /// <summary>
    /// This property holds the lowest number in the range.
    /// </summary>
    public double Low { get; }

    /// <summary>
    /// This property holds the highest number in the range.
    /// </summary>
    public double High { get; }

    /// <summary>
    /// This property reports whether the range says nothing useful -- either because it reaches to
    /// infinity or because the arithmetic that made it produced something that is not a number at
    /// all.  A range like that cannot rule anything out, and code that skips must ask before it skips.
    /// </summary>
    public bool IsAnywhere => double.IsNaN(Low) || double.IsNaN(High) ||
                              double.IsInfinity(Low) || double.IsInfinity(High);

    /// <summary>
    /// This property holds how wide the range is.
    /// </summary>
    public double Width => High - Low;

    /// <summary>
    /// This property holds the number in the middle of the range.
    /// </summary>
    public double Middle => (Low + High) / 2;

    public FieldRange(double low, double high)
    {
        Low = low;
        High = high;
    }

    /// <summary>
    /// This method returns a range holding the one number given.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The range holding just it.</returns>
    public static FieldRange Just(double value)
    {
        return new FieldRange(value, value);
    }

    /// <summary>
    /// This method returns a range from two numbers in either order.
    /// </summary>
    /// <param name="first">One end.</param>
    /// <param name="second">The other.</param>
    /// <returns>The range between them.</returns>
    public static FieldRange Between(double first, double second)
    {
        return new FieldRange(Math.Min(first, second), Math.Max(first, second));
    }

    /// <summary>
    /// This method returns the smallest range holding both of the given numbers, or "anywhere" if
    /// either is not a number.
    /// <para>
    /// This and its four-number twin are written out rather than taking however many are handed to
    /// them, because a range is worked out for every span of every ray and one that gathered its
    /// arguments into an array would put millions of those on the heap in a frame.
    /// </para>
    /// </summary>
    /// <param name="first">One number to cover.</param>
    /// <param name="second">Another.</param>
    /// <returns>The range covering them.</returns>
    public static FieldRange Covering(double first, double second)
    {
        if (double.IsNaN(first) || double.IsNaN(second))
            return Anywhere;

        return first < second
            ? new FieldRange(first, second)
            : new FieldRange(second, first);
    }

    /// <summary>
    /// This method returns the smallest range holding all four of the given numbers, or "anywhere" if
    /// any of them is not a number.
    /// </summary>
    /// <returns>The range covering them.</returns>
    public static FieldRange Covering(double first, double second, double third, double fourth)
    {
        if (double.IsNaN(first) || double.IsNaN(second) ||
            double.IsNaN(third) || double.IsNaN(fourth))
            return Anywhere;

        double low = Math.Min(Math.Min(first, second), Math.Min(third, fourth));
        double high = Math.Max(Math.Max(first, second), Math.Max(third, fourth));

        return new FieldRange(low, high);
    }

    /// <summary>
    /// This property reports whether this range holds just one number, which is how a rule tells that
    /// what it was given is really a constant -- a power's exponent, most usefully.
    /// </summary>
    public bool IsExact => Low.Equals(High);

    /// <summary>
    /// This method reports whether the given number falls within this range.
    /// </summary>
    /// <param name="value">The number to test.</param>
    /// <returns><c>true</c>, if the number is in the range.</returns>
    public bool Contains(double value)
    {
        return value >= Low && value <= High;
    }

    /// <summary>
    /// This method reports whether the given number falls within this range, allowing it to miss by
    /// the given amount.  Bounds are worked out in floating point like everything else, so a value
    /// sitting exactly on an end may land a bit outside it.
    /// </summary>
    /// <param name="value">The number to test.</param>
    /// <param name="tolerance">How far outside the range still counts as in it.</param>
    /// <returns><c>true</c>, if the number is in the range.</returns>
    public bool Contains(double value, double tolerance)
    {
        return value >= Low - tolerance && value <= High + tolerance;
    }

    /// <summary>
    /// This method returns the lower half and the upper half of this range, which is how a marcher
    /// narrows in on a crossing it has not ruled out.
    /// </summary>
    /// <returns>The two halves of this range.</returns>
    public (FieldRange Lower, FieldRange Upper) Split()
    {
        double middle = Middle;

        return (new FieldRange(Low, middle), new FieldRange(middle, High));
    }

    public static FieldRange operator -(FieldRange range)
    {
        return new FieldRange(-range.High, -range.Low);
    }

    public static FieldRange operator +(FieldRange left, FieldRange right)
    {
        return new FieldRange(left.Low + right.Low, left.High + right.High);
    }

    public static FieldRange operator -(FieldRange left, FieldRange right)
    {
        return new FieldRange(left.Low - right.High, left.High - right.Low);
    }

    /// <summary>
    /// Multiplying two ranges: with either able to straddle nought, which corner of the two gives the
    /// lowest product and which the highest depends on their signs, so all four are tried.
    /// </summary>
    public static FieldRange operator *(FieldRange left, FieldRange right)
    {
        return Covering(
            left.Low * right.Low, left.Low * right.High,
            left.High * right.Low, left.High * right.High);
    }

    /// <summary>
    /// Dividing two ranges: a divisor that could be nought could make the answer anything, and saying
    /// so is the only honest thing to do with it.
    /// </summary>
    public static FieldRange operator /(FieldRange left, FieldRange right)
    {
        if (right.Contains(0))
            return Anywhere;

        return Covering(
            left.Low / right.Low, left.Low / right.High,
            left.High / right.Low, left.High / right.High);
    }

    public override string ToString()
    {
        return $"[{Low:G6}, {High:G6}]";
    }
}
