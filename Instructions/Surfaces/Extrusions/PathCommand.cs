using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This class represents a command in creating a general path.
/// </summary>
public class PathCommand
{
    private readonly PathCommandType _commandType;
    private readonly Term[] _terms;

    public PathCommand(PathCommandType commandType, params Term[] terms)
    {
        _commandType = commandType;
        _terms = terms;
    }

    /// <summary>
    /// This method is used to apply this command to the given path.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="path">The path to apply the command to.</param>
    internal void Apply(Variables variables, GeneralPath path)
    {
        if (_commandType == PathCommandType.Svg)
            ApplySvgSpec(variables, path);
        else
            ApplyCommand(variables, path);
    }

    /// <summary>
    /// This method is used to apply an SVG path specification to the given path.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="path">The path to apply the command to.</param>
    private void ApplyCommand(Variables variables, GeneralPath path)
    {
        double[] numbers = _terms
            .Select(term => term.GetValue<double>(variables))
            .ToArray();

        switch (_commandType)
        {
            case PathCommandType.MoveTo:
                path.MoveTo(numbers[0], numbers[1]);
                break;
            case PathCommandType.LineTo:
                path.LineTo(numbers[0], numbers[1]);
                break;
            case PathCommandType.Close:
                path.ClosePath();
                break;
            case PathCommandType.Svg: // won't happen, but for completeness...
            default:
                throw new ArgumentOutOfRangeException($"Unknown path command type: {_commandType}.");
        }
    }

    /// <summary>
    /// This method is used to apply a raw drawing command to the given path.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="path">The path to apply the command to.</param>
    private void ApplySvgSpec(Variables variables, GeneralPath path)
    {
        string spec = _terms[0].GetValue<string>(variables);

        new SvgPathFactory(spec).ParseInto(path);
    }
}
