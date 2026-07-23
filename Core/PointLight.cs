using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a point light source in a rendered scene: a lamp at a place, shining
/// equally in every direction.
/// </summary>
public class PointLight : Light
{
    /// <summary>
    /// This property notes the location of the light source.
    /// </summary>
    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
    public Point Location { get; set; } = Point.Zero;

    /// <summary>
    /// This method works out which way the light lies from the given point and how far off it is,
    /// which for a point light is simply the direction and distance to where it stands.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>The unit direction from the point toward the light, and the distance to it.</returns>
    public override (Vector Direction, double Distance) TowardFrom(Point point)
    {
        Vector vector = Location - point;

        return (vector.Unit, vector.Magnitude);
    }
}
