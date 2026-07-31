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
    private readonly TextPathResolver _textResolver;

    public PathCommand(PathCommandType commandType, params Term[] terms)
    {
        _commandType = commandType;
        _terms = terms;
    }

    public PathCommand(TextPathResolver textResolver)
    {
        _commandType = PathCommandType.Text;
        _textResolver = textResolver;
    }

    /// <summary>
    /// This method is used to apply this command to the given path.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="path">The path to apply the command to.</param>
    internal void Apply(RenderContext context, Variables variables, GeneralPath path)
    {
        switch (_commandType)
        {
            case PathCommandType.Svg:
                ApplySvgSpec(variables, path);
                break;
            case PathCommandType.Icon:
                ApplyIconSpec(variables, path);
                break;
            case PathCommandType.Text:
                ApplyTextSpec(context, variables, path);
                break;
            default:
                ApplyCommand(variables, path);
                break;
        }
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

    /// <summary>
    /// This method is used to apply a FontAwesome icon to the given path.  The icon's outline is
    /// read from the installed FontAwesome zip and handed to the same factory the raw SVG command
    /// uses.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="path">The path to apply the command to.</param>
    private void ApplyIconSpec(Variables variables, GeneralPath path)
    {
        string spec = _terms[0].GetValue<string>(variables);

        new SvgPathFactory(FontAwesomeIcons.ReadPathData(spec)).ParseInto(path);
    }

    /// <summary>
    /// This method is used to lay a run of text out and fold its glyph outlines into the given
    /// path.  The text resolver does the laying out; here we simply add the glyphs it produced
    /// to the path we are building, so text sits among any other runs the path already has.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="path">The path to apply the command to.</param>
    private void ApplyTextSpec(RenderContext context, Variables variables, GeneralPath path)
    {
        GeneralPath glyphs = _textResolver.Resolve(context, variables);

        path.Append(glyphs);
    }
}
