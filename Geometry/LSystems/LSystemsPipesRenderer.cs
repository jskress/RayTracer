using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class implements rendering a 3D, L-system generated path as a series of "pipes".
/// </summary>
public class LSystemsPipesRenderer : LSystemShapeRenderer
{
    public LSystemsPipesRenderer(string production) : base(production) {}

    private double _radius;

    /// <summary>
    /// This method is used to tell us that the rendering to a surface is starting.
    /// </summary>
    /// <param name="turtle">The initial turtle.</param>
    protected override void Begin(Turtle turtle)
    {
        _radius = Diameter / 2;

        Surfaces.Add(new Sphere
        {
            Transform = Transforms.Scale(_radius),
            Material = null // <-- This is important.
        });
        BoundingBox = new BoundingBox()
            .Add(Point.Zero);
    }

    /// <summary>
    /// This method should be overridden to handle the given command.
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    /// <param name="command">The turtle command to handle.</param>
    protected override void Execute(Turtle turtle, TurtleCommand command)
    {
        if (command == TurtleCommand.DrawLine)
        {
            Point point = new Point(turtle.Location.X, turtle.Location.Y, turtle.Location.Z);

            CreateCylinder(turtle);
            CreateSphere(turtle);

            BoundingBox.Add(turtle.Location);
        }
    }

    /// <summary>
    /// This method is used to create a cylinder that will represent the latest draw line
    /// command.
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    private void CreateCylinder(Turtle turtle)
    {
        Matrix matrix = Transforms.Translate(turtle.PreviousLocation) *
            GetRotation(turtle.Direction) *
            Transforms.Scale(_radius, 1, _radius);

        Surfaces.Add(new Cylinder
        {
            MinimumY = 0,
            MaximumY = Distance,
            Transform = matrix,
            Material = null // <-- This is important.
        });
    }

    /// <summary>
    /// This method is used to create a sphere to cap the end of the cylinder that
    /// represents a drawn line.
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    private void CreateSphere(Turtle turtle)
    {
        Matrix matrix = Transforms.Translate(turtle.Location) *
            Transforms.Scale(_radius);

        Surfaces.Add(new Sphere
        {
            Transform = matrix,
            Material = null // <-- This is important.
        });
    }

    /// <summary>
    /// This method is used to tell us that the rendering to a surface is ending.
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    protected override void Complete(Turtle turtle)
    {
        BoundingBox.Expand(_radius);
    }

    /// <summary>
    /// This method is used to determine a rotation matrix that will align the "up" vector
    /// with the one given.
    /// </summary>
    /// <param name="direction">The vector to determine the rotation matrix for.</param>
    /// <returns>The appropriate matrix.</returns>
    private static Matrix GetRotation(Vector direction)
    {
        if (direction.Matches(Directions.Down))
            return Transforms.Scale(1, -1, 1);

        Vector vector = Directions.Up.Cross(direction);
        // double sine = vector.Magnitude;
        double cosine = Directions.Up.Dot(direction);
        double constant = 1.0 / (1.0 + cosine);
        Matrix matrix = new Matrix([
            0, -vector.Z, vector.Y, 0,
            vector.Z, 0, -vector.X, 0,
            -vector.Y, vector.X, 0, 0,
            0, 0, 0, 0
        ]);
        Matrix m2 = matrix * matrix;
        
        return Matrix.Identity + matrix + m2 * constant;
    }
}
