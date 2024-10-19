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
