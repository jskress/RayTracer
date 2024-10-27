using System.Text;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This is the base class for each of the supported renderers that will convert the
/// production of an L-system into some form of geometry.
/// </summary>
public abstract class LSystemShapeRenderer
{
    /// <summary>
    /// This field holds the standard rune (character) to turtle command mapping.
    /// </summary>
    private static readonly Dictionary<Rune, TurtleCommand> StandardCommandMapping = new ()
    {
        { new Rune('f'), TurtleCommand.Move },
        { new Rune('F'), TurtleCommand.DrawLine },
        { new Rune('+'), TurtleCommand.TurnLeft },
        { new Rune('-'), TurtleCommand.TurnRight },
        { new Rune('^'), TurtleCommand.PitchUp },
        { new Rune('&'), TurtleCommand.PitchDown },
        { new Rune('\\'), TurtleCommand.RollLeft },
        { new Rune('/'), TurtleCommand.RollRight },
        { new Rune('|'), TurtleCommand.TurnAround },
        { new Rune('$'), TurtleCommand.ToVertical },
        { new Rune('['), TurtleCommand.StartBranch },
        { new Rune(']'), TurtleCommand.CompleteBranch }
    };

    /// <summary>
    /// This property carries the global angle to use in rendering the surface. 
    /// </summary>
    public double Angle { get; set; }

    /// <summary>
    /// This property carries the global distance the turtle is to travel for each move
    /// to use in rendering the surface. 
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// This property holds the map that will be used to convert runes to turtle commands.
    /// </summary>
    public Dictionary<Rune, TurtleCommand> CommandMapping { get; set; } = StandardCommandMapping;

    /// <summary>
    /// This property exposes the surface generated by a subclass.
    /// </summary>
    protected Surface Surface { get; set; }

    private readonly string _production;
    private readonly Stack<Turtle> _stack;

    protected LSystemShapeRenderer(string production)
    {
        _production = production;
        _stack = new Stack<Turtle>();
    }

    /// <summary>
    /// This method is used to render the L-system production we were constructed with into
    /// a surface for ray tracing.  Note that the material ie explicitly set to <c>null</c>.
    /// </summary>
    /// <returns>The surface that results from</returns>
    internal Surface Render()
    {
        if (CommandMapping.IsNullOrEmpty())
            throw new Exception("A non-empty turtle command map must be provided.");

        Turtle turtle = new Turtle();
        
        _stack.Push(turtle);

        Begin(turtle);

        _production.AsRunes()
            .Select(ToTurtleCommand)
            .Where(command => command != TurtleCommand.Unknown)
            .ToList()
            .ForEach(command =>
            {
                PreExecute(_stack.Peek(), command);
                Execute(_stack.Peek(), command);
            });

        Complete(turtle);

        return Surface;
    }

    /// <summary>
    /// This method is used to tell the subclass that the rendering to a surface is
    /// starting.
    /// </summary>
    /// <param name="turtle">The initial turtle.  Provided in case the renderer needs to
    /// make any initial adjustments.</param>
    protected virtual void Begin(Turtle turtle) {}

    /// <summary>
    /// This method is used to convert the given rune into a turtle command.
    /// </summary>
    /// <param name="rune">The rune to convert.</param>
    /// <returns>The turtle command the rune translates to.</returns>
    private TurtleCommand ToTurtleCommand(Rune rune)
    {
        return CommandMapping.GetValueOrDefault(rune, TurtleCommand.Unknown);
    }

    /// <summary>
    /// This method is used to provide default handling for some of the turtle commands.
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    /// <param name="command">The turtle command to handle.</param>
    private void PreExecute(Turtle turtle, TurtleCommand command)
    {
        switch (command)
        {
            case TurtleCommand.Move:
            case TurtleCommand.DrawLine:
                turtle.Location += turtle.Direction * Distance;
                break;
            case TurtleCommand.TurnLeft:
                turtle.Direction *= Transforms.RotateAroundZ(-Angle, true);
                break;
            case TurtleCommand.TurnRight:
                turtle.Direction *= Transforms.RotateAroundZ(Angle, true);
                break;
            case TurtleCommand.PitchUp:
                turtle.Direction *= Transforms.RotateAroundX(-Angle, true);
                break;
            case TurtleCommand.PitchDown:
                turtle.Direction *= Transforms.RotateAroundX(Angle, true);
                break;
            case TurtleCommand.RollLeft:
                turtle.Direction *= Transforms.RotateAroundY(-Angle, true);
                break;
            case TurtleCommand.RollRight:
                turtle.Direction *= Transforms.RotateAroundY(Angle, true);
                break;
            case TurtleCommand.TurnAround:
                turtle.Direction = -turtle.Direction;
                break;
            case TurtleCommand.ToVertical:
                turtle.Direction = Directions.Up;
                break;
            case TurtleCommand.StartBranch:
                _stack.Push(turtle.Copy());
                break;
            case TurtleCommand.CompleteBranch:
                _stack.Pop();
                break;
            case TurtleCommand.Unknown:
            default:
                break;
        }
    }

    /// <summary>
    /// This method should be overridden to handle the given command.
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    /// <param name="command">The turtle command to handle.</param>
    protected abstract void Execute(Turtle turtle, TurtleCommand command);

    /// <summary>
    /// This method is used to tell the subclass that the rendering to a surface is
    /// ending.
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    protected virtual void Complete(Turtle turtle) {}
}
