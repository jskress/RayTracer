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

    /// <summary>
    /// This property holds the current segment diameter for the turtle.
    /// </summary>
    public double Diameter { get; private set; }

    private readonly LSystemRenderingControls _controls;

    internal Turtle(LSystemRenderingControls controls)
    {
        _controls = controls;

        Location = Point.Zero;
        PreviousLocation = Point.Zero;
        Up = Directions.Up;
        Direction = Directions.Right;
        Diameter = controls.Diameter;
    }

    /// <summary>
    /// This method is used to move the turtle by the current distance in the current
    /// direction.
    /// </summary>
    public void Move()
    {
        PreviousLocation = Location;
        Location += Direction * _controls.Length;
    }

    /// <summary>
    /// This method is used to turn the turtle left or right.
    /// </summary>
    /// <param name="sign">The direction of the turn.</param>
    public void Yaw(int sign)
    {
        Direction = RotateAround(Direction, Up, _controls.Angle * sign);
    }

    /// <summary>
    /// This method is used to change the pitch of the turtle.
    /// </summary>
    /// <param name="sign">The direction of the pitch.</param>
    public void Pitch(int sign)
    {
        Vector axis = Direction.Cross(Up);
        double angle = _controls.Angle * sign;

        Direction = RotateAround(Direction, axis, angle);
        Up = RotateAround(Up, axis, angle);
    }

    /// <summary>
    /// This method is used to change the roll of the turtle.
    /// </summary>
    /// <param name="sign">The direction of the roll.</param>
    public void Roll(int sign)
    {
        Up = RotateAround(Up, Direction, _controls.Angle * sign);
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
    /// This method is used to decrease the segment diameter by the factor provided during
    /// construction.
    /// </summary>
    public void DecreaseDiameter()
    {
        Diameter *= _controls.Factor;
    }

    /// <summary>
    /// This method creates a copy of the current turtle.
    /// </summary>
    /// <returns>A copy of this turtle.</returns>
    public Turtle Copy()
    {
        return new Turtle(_controls)
        {
            Location = Location,
            PreviousLocation = PreviousLocation,
            Up = Up,
            Direction = Direction,
            Diameter = Diameter
        };
    }
}
