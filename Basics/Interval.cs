using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class represents a mathematical interval.  It notes whether each end of the
/// interval is open or closed. 
/// </summary>
public record Interval
{
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
    /// </summary>
    public bool IsAtEnd => _value.Near(_stopAt);

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
