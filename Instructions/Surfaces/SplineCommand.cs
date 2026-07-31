using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class represents a command in creating a spline.
/// </summary>
public class SplineCommand
{
    private readonly SplineCommandType _commandType;
    private readonly Term[] _terms;
    private readonly Term _scaleTerm;

    public SplineCommand(SplineCommandType commandType, params Term[] terms)
    {
        _commandType = commandType;

        // Each spline point is three terms (its X, Y and Z), so a command's point terms
        // always come in threes.  A single extra term beyond that is the optional per-point
        // scale, which we pull off here so the point extraction below stays clean.
        if (terms.Length % 3 == 1)
        {
            _scaleTerm = terms[^1];
            _terms = terms[..^1];
        }
        else
            _terms = terms;
    }

    /// <summary>
    /// This method is used to apply this command to the given spline.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="spline">The spline to apply the command to.</param>
    internal void Apply(Variables variables, Spline spline)
    {
        Point[] points = GetPoints(variables);
        double scale = _scaleTerm?.GetValue<double>(variables) ?? 1;

        switch (_commandType)
        {
            case SplineCommandType.MoveTo:
                spline.Start = points[0];
                spline.StartScale = scale;
                break;
            case SplineCommandType.LineTo:
                spline.Segments.Add(new SplineSegmentSpec { End = points[0], Scale = scale });
                break;
            case SplineCommandType.QuadTo:
                spline.Segments.Add(new SplineSegmentSpec
                {
                    Control1 = points[0], End = points[1], Scale = scale
                });
                break;
            case SplineCommandType.CurveTo:
                spline.Segments.Add(new SplineSegmentSpec
                {
                    Control1 = points[0], Control2 = points[1], End = points[2], Scale = scale
                });
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unknown spline command type: {_commandType}.");
        }
    }

    /// <summary>
    /// This method is used to evaluate our list of terms into a list of points.  Each
    /// three of our terms evaluate to the X, Y and Z of one point.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The list of points our terms evaluate to.</returns>
    private Point[] GetPoints(Variables variables)
    {
        Point[] points = new Point[_terms.Length / 3];

        for (int index = 0; index < _terms.Length; index += 3)
        {
            double x = _terms[index].GetValue<double>(variables);
            double y = _terms[index + 1].GetValue<double>(variables);
            double z = _terms[index + 2].GetValue<double>(variables);

            points[index / 3] = new Point(x, y, z);
        }

        return points;
    }
}
