using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a distant light: one so far off that its rays arrive parallel and its
/// direction is the same everywhere, which is how the sun lights a scene.
/// <para>
/// It has no place, only a heading, and that is the whole of the difference.  A point light a mile
/// up would do nearly the same job, but "nearly" is the trouble: its rays would still fan out by a
/// hair and its shadows still splay, where a distant light's rays are truly parallel and its
/// shadows truly straight, which is what the eye reads as sunlight.
/// </para>
/// </summary>
public class DistantLight : Light
{
    /// <summary>
    /// This property notes the direction the light travels -- where the rays are headed, not where
    /// they come from -- so that a light pointed <c>[0, -1, 0]</c> shines straight down.
    /// </summary>
    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
    public Vector Direction { get; set; } = new (0, -1, 0);

    /// <summary>
    /// This method works out which way the light lies from the given point, which for a distant
    /// light is the same everywhere -- back along the way its rays travel -- and infinitely far,
    /// so that a shadow ray keeps testing for occluders however far it must go.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>The unit direction toward the light, and infinity for the distance.</returns>
    public override (Vector Direction, double Distance) TowardFrom(Point point)
    {
        return ((-Direction).Unit, double.PositiveInfinity);
    }
}
