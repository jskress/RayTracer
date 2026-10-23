using RayTracer.Basics;

namespace RayTracer.Core.Physics;

/// <summary>
/// This class holds what a scene says of a surface it has made a body: a thing moved not by its transforms but by
/// what pushes it -- gravity, the air, what it strikes and what it is tied to -- worked out from where it stands
/// at time nought.  Everything it does not say is worked out for it: its mass from its size, as though it were as
/// dense as water; its drag from its size and the air; its shape a ball unless it is a cube.
/// </summary>
public sealed class Body
{
    /// <summary>
    /// This is how dense a body is taken to be when nothing says its mass: water's, in kilograms a cubic meter.
    /// </summary>
    public const double Density = 1000;

    /// <summary>
    /// This property holds the body's mass in kilograms, or null for one worked out from its size.
    /// </summary>
    public double? Mass { get; set; }

    /// <summary>
    /// This property holds how fast and which way it is moving at time nought.
    /// </summary>
    public Vector Velocity { get; set; } = new (0, 0, 0);

    /// <summary>
    /// This property holds how fast it is turning at time nought, in radians a second about each axis.
    /// </summary>
    public Vector Spin { get; set; } = new (0, 0, 0);

    /// <summary>
    /// This property holds how much of its speed into a thing it strikes it keeps coming away: nought for none,
    /// one for all.
    /// </summary>
    public double Bounce { get; set; } = 0.5;

    /// <summary>
    /// This property holds how hard it grips what it slides on.
    /// </summary>
    public double Friction { get; set; } = 0.5;

    /// <summary>
    /// This property holds how much of the air's drag on a thing of its size and shape it feels: one for all of it,
    /// nought for none.
    /// </summary>
    public double Drag { get; set; } = 1;

    /// <summary>
    /// This property notes whether the body collides as a box -- its own box -- rather than as a ball; null for a
    /// box if it is a cube and a ball otherwise.
    /// </summary>
    public bool? IsABox { get; set; }

    /// <summary>
    /// This property holds the radius of the ball it collides as, or null for one from its size.
    /// </summary>
    public double? Radius { get; set; }

    /// <summary>
    /// This property holds what it is tied to.
    /// </summary>
    public List<Tether> Tethers { get; set; } = [];
}

/// <summary>
/// This record holds one tie a body has: to a pin standing still, or to another body by its name, by a string
/// that holds it no further away than its length -- as far as it stands at time nought, unless said.  A string
/// pulls and never pushes: a body may come nearer, as a ball on a string swung over its pin does.
/// </summary>
/// <param name="Pin">The place it is tied to, or null.</param>
/// <param name="Name">The name of the body it is tied to, or null.</param>
/// <param name="Length">How long the string is, or null for the distance at time nought.</param>
public sealed record Tether(Point Pin, string Name, double? Length);
