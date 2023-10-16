using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This is the base class for all things that can go in a scene.
/// </summary>
public class Entity
{
    /// <summary>
    /// This property holds the location of the entity.
    /// </summary>
    public Point Location { get; set; }

    protected Entity(Point location) => Location = location;
}
