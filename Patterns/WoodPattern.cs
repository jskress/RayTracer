using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the wood pattern.
/// </summary>
public class WoodPattern : Pattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This property controls the turbulence we will use.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public Turbulence Turbulence { get; set; }

    /// <summary>
    /// This method is used to push any random number generator seeds throughout the pigment
    /// tree.
    /// </summary>
    /// <param name="seed">The seed value to set.</param>
    public override void SetSeed(int seed)
    {
        if (Turbulence is not null)
            Turbulence.Seed ??= seed;
    }

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        double x = point.X;
        double y = point.Y;

        if (Turbulence is not null)
        {
            double tx = Turbulence.Generate(point);
            double ty = Turbulence.Generate(point);

            x += Cycloidal(x * tx);
            y += Cycloidal(y * ty);
        }
        
        double value = new Vector(x, y, 0).Magnitude;
        bool isOdd = Math.Floor(value) % 2 != 0;

        value = 1 - InvertedClip(value.Fraction());

        if (isOdd)
            value = 1 - value;

        return value;
    }
}
