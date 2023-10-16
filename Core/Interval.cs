namespace RayTracer.Core;

/// <summary>
/// This class represents an interval of numbers.
/// </summary>
public sealed class Interval
{
    /// <summary>
    /// This represents an interval that contains and surrounds no value.
    /// </summary>
    public static readonly Interval Empty = new (double.PositiveInfinity, double.NegativeInfinity);

    /// <summary>
    /// This represents an interval that contains and surrounds all values.
    /// </summary>
    public static readonly Interval Universe = new (double.NegativeInfinity, double.PositiveInfinity);

    /// <summary>
    /// This property notes the minimum value in the interval.
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    /// This property notes the maximum value in the interval.
    /// </summary>
    public double Maximum { get; set; }

    public Interval(double minimum, double maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    public Interval(Interval source)
    {
        Minimum = source.Minimum;
        Maximum = source.Maximum;
    }

    /// <summary>
    /// This method returns whether the given number is contained in this interval.  If
    /// the number matches one of the boundaries, it is counted as contained.
    /// </summary>
    /// <param name="number">The number to test.</param>
    /// <returns><c>true</c>, if the number is contained in the interval, or <c>false</c>,
    /// if not.</returns>
    public bool Contains(double number)
    {
        return Minimum <= number && number <= Maximum;
    }

    /// <summary>
    /// This method returns whether the given number is surrounded by this interval.  If
    /// the number matches one of the boundaries, it is counted as not surrounded.
    /// </summary>
    /// <param name="number">The number to test.</param>
    /// <returns><c>true</c>, if the number is contained in the interval, or <c>false</c>,
    /// if not.</returns>
    public bool Surrounds(double number)
    {
        return Minimum < number && number < Maximum;
    }

    /// <summary>
    /// This method is used to clamp a number to fit within the interval.
    /// </summary>
    /// <param name="number">The number to clamp.</param>
    /// <returns>The clamped number.</returns>
    public double Clamp(double number)
    {
        return Math.Clamp(number, Minimum, Maximum);
    }
}
