using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the marble pattern.  There is less to it than the name suggests: it is
/// the X coordinate, pushed sideways by turbulence, and nothing more.  All the veining comes
/// from the colour map it is handed to, which wraps as X marches past each whole number -- so a
/// map that runs dark at both ends draws a vein wherever X crosses one, and the turbulence is
/// what stops those veins being straight.  Without turbulence it is simply stripes down X.
/// </summary>
public class MarblePattern : Pattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This pattern folds turbulence into its own arithmetic, so its points are left alone.
    /// </summary>
    protected override bool StirsItsOwnPoints => true;


    /// <summary>
    /// This method is used to push any random number generator seeds throughout the pigment
    /// tree.
    /// </summary>
    /// <param name="seed">The seed value to set.</param>
    public override void SetSeed(int seed)
    {
        Turbulence?.Seed ??= seed;
    }

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        // Note this deliberately isn't held to the [0, 1] interval: the value is a coordinate,
        // and the colour map wraps whatever it is given.  That wrapping is the whole point --
        // it is what turns a marching X into repeated veins.
        return Turbulence is null
            ? point.X
            : point.X + Turbulence.Generate(point);
    }
}
