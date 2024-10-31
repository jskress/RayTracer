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
    public Point Location { get; private set; }

    /// <summary>
    /// This property holds the previous location of the turtle.
    /// </summary>
    public Point PreviousLocation { get; private set; }

    /// <summary>
    /// This property holds the direction which is currently "up" for the turtle.
    /// </summary>
    private Vector Up { get; set; }

    /// <summary>
    /// This property holds the direction in which the turtle is facing.
    /// </summary>
    public Vector Direction { get; private set; }

    internal Turtle()
    {
        Location = Point.Zero;
        PreviousLocation = Point.Zero;
        Up = Directions.Up;
        Direction = Directions.Right;
    }

    /// <summary>
    /// This method is used to move the turtle by the given distance in the current
    /// direction.
    /// </summary>
    /// <param name="distance">The distance to move the turtle.</param>
    public void Move(double distance)
    {
        PreviousLocation = Location;
        Location += Direction * distance;
    }

    /// <summary>
    /// This method is used to turn the turtle left or right.
    /// </summary>
    /// <param name="angle">The amount to turn the turtle.</param>
    public void Yaw(double angle)
    {
        Direction = RotateAround(Direction, Up, angle);
    }

    /// <summary>
    /// This method is used to change the pitch of the turtle.
    /// </summary>
    /// <param name="angle">The amount to pitch the turtle.</param>
    public void Pitch(double angle)
    {
        Vector axis = Direction.Cross(Up);
        
        Direction = RotateAround(Direction, axis, angle);
        Up = RotateAround(Up, axis, angle);
    }

    /// <summary>
    /// This method is used to change the roll of the turtle.
    /// </summary>
    /// <param name="angle">The amount to roll the turtle.</param>
    public void Roll(double angle)
    {
        Up = RotateAround(Up, Direction, angle);
    }

    /// <summary>
    /// This method tells the turtle to turn around.
    /// </summary>
    public void TurnAround()
    {
        Direction = -Direction;
    }

    /// <summary>
    /// This method tells the turtle to point up.
    /// </summary>
    public void PointUp()
    {
        Up = Directions.Left;
        Direction = Directions.Up;
    }

    /// <summary>
    /// This method is used to rotate a vector around the given axis vector.
    /// </summary>
    /// <param name="vector">The vector to rotate.</param>
    /// <param name="axis">The axis around which to rotate the vector.</param>
    /// <param name="angle">The rotation angle to apply.</param>
    /// <returns>The rotated vector.</returns>
    private static Vector RotateAround(Vector vector, Vector axis, double angle)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        
        return vector * cos + axis.Cross(vector) * sin +
               axis * axis.Dot(vector) * (1 - cos);
    }

    /// <summary>
    /// This method creates a copy of the current turtle.
    /// </summary>
    /// <returns>A copy of this turtle.</returns>
    public Turtle Copy()
    {
        return new Turtle
        {
            Location = Location,
            PreviousLocation = PreviousLocation,
            Up = Up,
            Direction = Direction
        };
    }
}
