using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the base class for banded patterns.  See the <see cref="BandType"/>
/// enum for the list of supported band types.
/// </summary>
public abstract class BandPattern : Pattern
{
    /// <summary>
    /// This property notes the type of band this instance implements.
    /// </summary>
    public BandType BandType { get; set; }

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        double value = BandType switch
        {
            BandType.LinearX => point.X,
            BandType.LinearY => point.Y,
            BandType.LinearZ => point.Z,
            BandType.Cylindrical => CylindricalValue(point),
            BandType.Spherical => SphericalValue(point),
            _ => throw new ArgumentOutOfRangeException($"Unexpected gradient type: {BandType}")
        };

        return Adjust(value);
    }

    /// <summary>
    /// This method provides the implementation of the cylindrical form of a gradient.
    /// </summary>
    /// <param name="point">The point to get the gradient value for.</param>
    /// <returns>The gradient value for the point.</returns>
    private static double CylindricalValue(Point point)
    {
        return Math.Sqrt(point.X * point.X + point.Z * point.Z);
    }

    /// <summary>
    /// This method provides the implementation of the spherical form of a gradient.
    /// </summary>
    /// <param name="point">The point to get the gradient value for.</param>
    /// <returns>The gradient value for the point.</returns>
    private static double SphericalValue(Point point)
    {
        return Math.Sqrt(point.X * point.X + point.Y * point.Y + point.Z * point.Z);
    }

    /// <summary>
    /// This method is used to adjust the value determined by the band type.
    /// </summary>
    /// <param name="value">The value to adjust.</param>
    /// <returns>The adjusted value.</returns>
    protected abstract double Adjust(double value);

    /// <summary>
    /// This method is used to decide whether our details match those of the given pattern.
    ///
    /// Subclasses must call this class's <c>DetailsMatch</c> method.
    /// </summary>
    /// <param name="pattern">The pattern to match against.</param>
    /// <returns><c>true</c>, if the given pattern's details match this one, or <c>false</c>,
    /// if not.</returns>
    protected override bool DetailsMatch(Pattern pattern)
    {
        BandPattern other = (BandPattern) pattern;

        return base.DetailsMatch(pattern) && BandType == other.BandType;
    }
}
