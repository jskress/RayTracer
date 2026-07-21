using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This is the base class for all patterns.
/// </summary>
public abstract class Pattern
{
    private const double TwoMPi = 6.283185307179586476925286766560;

    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public abstract int DiscretePigmentsNeeded { get; }

    /// <summary>
    /// This property holds the turbulence to stir points with before this pattern is asked about
    /// them, or <c>null</c> to leave them where they are.
    /// </summary>
    public Turbulence Turbulence { get; set; }

    /// <summary>
    /// This property reports whether this pattern folds turbulence into its own arithmetic rather
    /// than having its points stirred for it.  Marble and wood do, because what they want stirred
    /// is the single coordinate they are built from, not the point in space -- and stirring both
    /// would apply it twice.  POV-Ray carves out exactly the same two exceptions.
    /// </summary>
    protected virtual bool StirsItsOwnPoints => false;

    /// <summary>
    /// This property holds what the pattern's value is multiplied by before it is shaped, which
    /// says how many times over the pattern repeats across the range it once filled just once.
    /// </summary>
    public double Frequency { get; set; } = 1;

    /// <summary>
    /// This property holds what is added to the pattern's value before it is shaped, which slides
    /// the whole pattern along without changing it -- useful for nudging a band off a seam.
    /// </summary>
    public double Phase { get; set; }

    /// <summary>
    /// This property holds how the pattern's value is shaped once it has been produced.
    /// </summary>
    public WaveType Wave { get; set; } = WaveType.Ramp;

    /// <summary>
    /// This property holds the exponent used when the wave is <see cref="WaveType.Poly"/>, and is
    /// ignored otherwise.
    /// </summary>
    public double Exponent { get; set; } = 1;

    /// <summary>
    /// This method is used to push any random number generator seeds throughout the pigment
    /// tree.
    /// By default, if this pattern implements the <see cref="INoiseConsumer"/> interface,
    /// the seed will be set.
    /// </summary>
    /// <param name="seed">The seed value to set.</param>
    public virtual void SetSeed(int seed)
    {
        if (this is INoiseConsumer consumer)
            consumer.Seed ??= seed;
    }

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public abstract double Evaluate(Point point);

    /// <summary>
    /// This method is what callers should ask, rather than <see cref="Evaluate"/> directly: it
    /// stirs the point first, where the pattern wants that done for it, and then evaluates.
    /// <para>
    /// Keeping the stirring here rather than in each pattern is the whole reason every pattern gets
    /// turbulence for free -- a pattern goes on computing exactly what it always did, and simply
    /// finds itself asked about a point that has been pushed around.
    /// </para>
    /// </summary>
    /// <param name="point">The point the pattern is being asked about.</param>
    /// <returns>The derived pattern value.</returns>
    public double ValueFor(Point point)
    {
        double value = Evaluate(Turbulence is null || StirsItsOwnPoints
            ? point
            : Turbulence.Warp(point));

        return Shape(value);
    }

    /// <summary>
    /// This method takes the number a pattern produced and shapes it on its way to the colour map:
    /// scaled and slid by the frequency and phase, wrapped back into range, and then bent by
    /// whichever wave was asked for.
    /// <para>
    /// The order matters and is POV-Ray's: the frequency multiplies before the wrap, so raising it
    /// makes the pattern repeat rather than stretch, and the wave is applied last, to a value that
    /// is already in range.
    /// </para>
    /// </summary>
    /// <param name="value">The value the pattern produced.</param>
    /// <returns>The shaped value.</returns>
    private double Shape(double value)
    {
        // Left strictly alone when nothing has been asked for, which matters more than it looks:
        // POV-Ray wraps whenever its frequency is non-zero, and wrapping unbidden would break every
        // pattern that deliberately hands back a value outside [0, 1] -- marble most of all, whose
        // value is a coordinate that the colour map is meant to wrap for itself.
        if (Frequency != 1 || Phase != 0)
        {
            // The wrap is what turns a raised frequency into repetition rather than a stretch.  A
            // negative frequency runs the pattern backwards and can leave the value below zero, so
            // it is lifted back into range afterward.
            value = (value * Frequency + Phase) % 1;

            if (value < 0)
                value -= Math.Floor(value);
        }

        return Wave switch
        {
            WaveType.Ramp => value,
            WaveType.Sine => (1 + Cycloidal(value)) * 0.5,
            WaveType.Triangle => TriangleWave(value),
            WaveType.Scallop => Math.Abs(Cycloidal(value * 0.5)),
            WaveType.Cubic => value * value * (3 - 2 * value),
            WaveType.Poly => Math.Pow(value, Exponent),
            _ => value
        };
    }

    /// <summary>
    /// This method folds a value back and forth across the unit interval, so that it climbs to the
    /// middle and falls away again rather than snapping back at the end.
    /// </summary>
    /// <param name="value">The value to fold.</param>
    /// <returns>The folded value.</returns>
    private static double TriangleWave(double value)
    {
        double offset = value >= 0
            ? value - Math.Floor(value)
            : value + 1 + Math.Floor(Math.Abs(value));

        return offset >= 0.5 ? 2 * (1 - offset) : 2 * offset;
    }

    /// <summary>
    /// This method returns whether this pattern matches the one given.
    /// </summary>
    /// <param name="pattern">The pattern to match against.</param>
    /// <returns><c>true</c>, if the given pattern matches this one, or <c>false</c>, if
    /// not.</returns>
    public bool Matches(Pattern pattern)
    {
        return GetType() == pattern.GetType() &&
               DetailsMatch(pattern);
    }

    /// <summary>
    /// Subclasses that carry more data than the pattern itself does should override this
    /// and return whether their details match.  Even though the argument isn't specifically
    /// typed, subclasses can safely cast it to their own type, since a type check will
    /// have already been done.
    ///
    /// Subclasses must call this class's <c>DetailsMatch</c> method.
    /// </summary>
    /// <param name="pattern">The pattern to match against.</param>
    /// <returns><c>true</c>, if the given pattern's details match this one, or <c>false</c>,
    /// if not.</returns>
    protected virtual bool DetailsMatch(Pattern pattern)
    {
        return DiscretePigmentsNeeded == pattern.DiscretePigmentsNeeded;
    }

    /// <summary>
    /// This is a method for applying a sine wave to a value.  Taken from POV to use ina
    /// variety of things, mostly patterns.
    /// </summary>
    /// <param name="value">The value to update.</param>
    /// <returns>The updated value.</returns>
    protected static double Cycloidal(double value)
    {
        return value >= 0.0
            ? Math.Sin((value - Math.Floor(value)) * 50_000.0 / 50_000.0 * TwoMPi)
            : 0.0 - Math.Sin((0.0 - (value + Math.Floor(0.0 - value))) * 50_000.0 / 50_000.0 * TwoMPi);
    }

    /// <summary>
    /// This method is used to perform an "inverted clip" on the given value.  The value
    /// is clipped to between 0 and 1.  If it is already in this range, it is inverted from
    /// the number 1.
    /// </summary>
    /// <param name="value">The value to clip.</param>
    /// <returns>The clipped value.</returns>
    protected static double InvertedClip(double value)
    {
        return value switch
        {
            < 0.0 => 1.0,
            > 1.0 => 0.0,
            _ => 1.0 - value
        };
    }
}
