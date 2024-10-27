using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class represents the current state of rendering as represented by our turtle.
/// </summary>
public class Turtle
{
    /// <summary>
    /// This property holds the current location of the turtle.
    /// </summary>
    public Point Location { get; set; }

    /// <summary>
    /// This property holds the direction in which the turtle is facing.
    /// </summary>
    public Vector Direction { get; set; }

    internal Turtle()
    {
        Location = Point.Zero;
        Direction = Directions.Right;
    }

    /// <summary>
    /// This method creates a copy of the current turtle.
    /// </summary>
    /// <returns></returns>
    public Turtle Copy()
    {
        return new Turtle
        {
            Location = Location,
            Direction = Direction
        };
    }
}
