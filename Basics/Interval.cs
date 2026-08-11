using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class represents a mathematical interval.  It notes whether each end of the
/// interval is open or closed. 
/// </summary>
public record Interval
{
    /// <summary>
    /// This property produces an interval that will execute exactly once.
    /// </summary>
    public static Interval Once => new Interval { Start = 1, End = 1 }.Reset(1);

    /// <summary>
    /// This property holds the start value for the interval.
    /// </summary>
    public double Start { get; init; }

    /// <summary>
    /// This property notes whether the start of the interval is open or closed.
    /// </summary>
    public bool IsStartOpen { get; init; }

    /// <summary>
    /// This property holds the end value for the interval.
    /// </summary>
    public double End { get; init; }

    /// <summary>
    /// This property notes whether the end of the interval is open or closed.
    /// </summary>
    public bool IsEndOpen { get; init; }

    /// <summary>
    /// This property notes whether the interval has been exhausted.
    /// <para>
    /// It asks whether the <i>next</i> value would lie beyond the end rather than whether the current
    /// one has landed on it, and the difference is the difference between stopping and not stopping.
    /// A range only reaches its end exactly when the end is a whole number of steps from the start,
    /// and nothing makes anyone write one that is: <c>[0, 3.4]</c> counted by ones goes 0, 1, 2, 3, 4,
    /// and never once equals 3.4.  Asking whether it had arrived meant such a range ran forever.
    /// </para>
    /// <para>
    /// The end itself is still taken when it is landed on, which is what the check against
    /// <see cref="DoubleExtensions.Near"/> is for: a step of a quarter reaches one as 0.99999999, and
    /// a range written to include its end must include it.
    /// </para>
    /// </summary>
    public bool IsAtEnd
    {
        get
        {
            double next = _value + _step;

            return !next.Near(_stopAt) && (_step > 0 ? next > _stopAt : next < _stopAt);
        }
    }

    private double _value;
    private double _step;
    private double _stopAt;

    /// <summary>
    /// This method is used to set up the interval to produce values.  It must be called
    /// before the <see cref="Next"/> method.
    /// <remarks>It is up to the caller to make sure that the start, end and step make
    /// sense.  Infinite loops may otherwise result!</remarks>
    /// </summary>
    /// <param name="step">The step size to use.</param>
    /// <returns>This object, for fluency.</returns>
    public Interval Reset(double step)
    {
        _value =  Start - (IsStartOpen ? 0 : step);
        _step = step;
        _stopAt = End - (IsEndOpen ? step : 0);

        return this;
    }

    /// <summary>
    /// This method will produce the next value from the range based on the step value
    /// provided to the <see cref="Reset"/> method.  An exception is thrown if the interval
    /// has already been exhausted.
    /// </summary>
    /// <returns>The next value from the interval.</returns>
    public double Next()
    {
        if (IsAtEnd)
            throw new Exception("Illegal state: the range is already complete.");

        _value += _step;

        return _value;
    }
}
