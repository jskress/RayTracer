using RayTracer.Graphics;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class implements rendering a closed, 2D, L-system generated curve as an extrusion.
/// </summary>
public class LSystemExtrusionRenderer : LSystemShapeRenderer
{
    private GeneralPath _path;

    public LSystemExtrusionRenderer(string production) : base(production) {}

    /// <summary>
    /// This method is used to tell us that the rendering to a surface is starting.
    /// </summary>
    /// <param name="turtle">The initial turtle.</param>
    protected override void Begin(Turtle turtle)
    {
        _path = new GeneralPath();

        _path.MoveTo(0, 0);
    }

    /// <summary>
    /// This method should be overridden to handle the given command.
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    /// <param name="command">The turtle command to handle.</param>
    protected override void Execute(Turtle turtle, TurtleCommand command)
    {
        if (command == TurtleCommand.DrawLine)
            _path.LineTo(turtle.Location.X, turtle.Location.Z);
    }

    /// <summary>
    /// This method is used to tell us that the rendering to a surface is ending.
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    protected override void Complete(Turtle turtle)
    {
        _path.ClosePath();

        Surfaces.Add(new Extrusion
        {
            Path = _path.Reverse(),
            MinimumY = 0,
            MaximumY = 1,
            Material = null // <-- This is important.
        });

        // Note that since we hand the path to the extrusion, it will be disposed of at a
        // more appropriate time.
        _path = null;
    }
}
