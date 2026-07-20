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

    /// <summary>
    /// This property holds the material the turtle is currently drawing with, or <c>null</c> if
    /// nothing has named one, in which case whatever it draws inherits from the L-system as a
    /// whole, as it always did.
    /// <para>
    /// This is state rather than a one-off, which is what sets it apart from the surface a
    /// <c>~</c> stamps: naming a material changes everything drawn from there on, until something
    /// names another.  It sits alongside <see cref="Diameter"/> for that reason, and gets the same
    /// treatment at a branch -- a branch inherits the material in force where it forked, and gives
    /// it back on the way out, so colouring a limb cannot leak into its neighbour.
    /// </para>
    /// </summary>
    public Material Material { get; set; }

    /// <summary>
    /// This property holds how deeply branched the turtle currently is: zero along the trunk, one
    /// inside a branch off it, and so on.  It is what lets a plant be coloured by how far out it
    /// has grown without every level having to be spelled out as its own production rule.
    /// </summary>
    public int Depth { get; private set; }

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
    /// This method builds the matrix that places a surface into the turtle's current frame:
    /// a leaf authored in local space (growing along +Z, its width along X and its face
    /// looking toward +Y) is carried to grow along the turtle's heading, with its face
    /// following the turtle's roll and its base at the turtle's location.  The matrix columns
    /// are the turtle's own orthonormal axes -- left, up and heading -- with the location as
    /// the translation.  "Left" is <c>Up x Direction</c>, which makes the three a right-handed
    /// basis (its determinant is +1), so the placed surface is oriented, never mirrored.
    /// </summary>
    /// <returns>The placement matrix for the turtle's current position and orientation.</returns>
    internal Matrix GetPlacementMatrix()
    {
        Vector left = Up.Cross(Direction);

        return new Matrix([
            left.X, Up.X, Direction.X, Location.X,
            left.Y, Up.Y, Direction.Y, Location.Y,
            left.Z, Up.Z, Direction.Z, Location.Z,
            0, 0, 0, 1
        ]);
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
    /// This method creates the turtle that carries on inside a branch: a copy of this one, standing
    /// where it stands and carrying what it carries, but one level further out.  Everything the
    /// branch then does to itself is discarded when it closes, since the turtle that resumes is
    /// this one, untouched.
    /// </summary>
    /// <returns>The turtle to draw the branch with.</returns>
    public Turtle Branch()
    {
        return new Turtle(_controls)
        {
            Location = Location,
            PreviousLocation = PreviousLocation,
            Up = Up,
            Direction = Direction,
            Diameter = Diameter,
            Material = Material,
            Depth = Depth + 1
        };
    }
}
