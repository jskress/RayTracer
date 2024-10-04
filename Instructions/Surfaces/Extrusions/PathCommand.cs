using RayTracer.Basics;
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
        TwoDPoint[] points = GetPoints(variables);

        switch (_commandType)
        {
            case PathCommandType.MoveTo:
                path.MoveTo(points[0]);
                break;
            case PathCommandType.LineTo:
                path.LineTo(points[0]);
                break;
            case PathCommandType.QuadTo:
                path.QuadTo(points[0], points[1]);
                break;
            case PathCommandType.CurveTo:
                path.CubicTo(points[0], points[1], points[2]);
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
    /// This method is used to evaluate our list of terms into a list of points.  Each
    /// term evaluates to a double, each two of which are paired up to a point.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The list of points our terms evaluate to.</returns>
    private TwoDPoint[] GetPoints(Variables variables)
    {
        TwoDPoint[] points = new TwoDPoint[_terms.Length / 2];

        for (int index = 0; index < _terms.Length; index += 2)
        {
            double x = _terms[index].GetValue<double>(variables);
            double y = _terms[index + 1].GetValue<double>(variables);
            
            points[index / 2] = new TwoDPoint(x, y);
        }

        return points;
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
