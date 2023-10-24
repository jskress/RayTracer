using RayTracer.Basics;

namespace RayTracer.Parser;

/// <summary>
/// This class supports parsing transforms for surfaces or materials.
/// </summary>
internal class TransformParser
{
    private readonly FileContent _fileContent;
    private readonly TupleParser _tupleParser;
    private readonly List<Matrix> _transforms;

    internal TransformParser(FileContent fileContent)
    {
        _fileContent = fileContent;
        _tupleParser = new TupleParser(fileContent);
        _transforms = new List<Matrix>();
    }

    /// <summary>
    /// This method adds a transform of the indicated type to our working list.
    /// </summary>
    /// <param name="type"></param>
    /// <returns><c>true</c>, if the type was a transform, or <c>false</c>, if not.</returns>
    internal bool TryAddTransform(string type)
    {
        switch (type)
        {
            case "translate":
                ParseTranslate();
                break;
            case "scale":
                ParseScale();
                break;
            case "rotateX":
                ParseXRotation();
                break;
            case "rotateY":
                ParseYRotation();
                break;
            case "rotateZ":
                ParseZRotation();
                break;
            case "shear":
                ParseShear();
                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>
    /// This method returns the aggregate transform will all individual ones properly
    /// combined.
    /// </summary>
    /// <returns>The final transform.</returns>
    internal Matrix GetFinalTransform()
    {
        if (_transforms.Count == 0)
            return Matrix.Identity;

        _transforms.Reverse();

        List<Matrix> rest = _transforms.GetRange(1, _transforms.Count - 1);

        return rest
            .Aggregate(_transforms[0], (accumulator, next) => accumulator * next);
    }

    /// <summary>
    /// This method handles parsing a translation matrix.
    /// </summary>
    private void ParseTranslate()
    {
        double[] tuple = _tupleParser.ParseTuple();

        _transforms.Add(Transforms.Translate(tuple[0], tuple[1], tuple[2]));
    }

    /// <summary>
    /// This method handles parsing a scaling matrix.
    /// </summary>
    private void ParseScale()
    {
        if (_fileContent.Peek() == '<')
        {
            double[] tuple = _tupleParser.ParseTuple();

            _transforms.Add(Transforms.Scale(tuple[0], tuple[1], tuple[2]));
        }
        else
        {
            double scale = _fileContent.GetNextDouble();

            _transforms.Add(Transforms.Scale(scale));
        }
    }

    /// <summary>
    /// This method parses a rotation matrix around the X axis.
    /// </summary>
    private void ParseXRotation()
    {
        (double angle, bool isRadians) = ParseAngle();

        _transforms.Add(Transforms.RotateAroundX(angle, isRadians));
    }

    /// <summary>
    /// This method parses a rotation matrix around the Y axis.
    /// </summary>
    private void ParseYRotation()
    {
        (double angle, bool isRadians) = ParseAngle();

        _transforms.Add(Transforms.RotateAroundY(angle, isRadians));
    }

    /// <summarz>
    /// This method parses a rotation matrix around the Z axis.
    /// </summarz>
    private void ParseZRotation()
    {
        (double angle, bool isRadians) = ParseAngle();

        _transforms.Add(Transforms.RotateAroundZ(angle, isRadians));
    }

    /// <summary>
    /// This method parses an angle from the content.
    /// </summary>
    /// <returns>A tuple containing the angle and an "is radians" flag.</returns>
    private (double, bool) ParseAngle()
    {
        double angle = _fileContent.GetNextDouble();
        bool isRadians = _fileContent.IsNext('*');

        return (angle, isRadians);
    }

    /// <summary>
    /// This method handles parsing a shearing matrix.
    /// </summary>
    private void ParseShear()
    {
        double[] tuple = _tupleParser.ParseTuple(6);

        _transforms.Add(Transforms.Shear(
            tuple[0], tuple[1], tuple[2],
            tuple[3], tuple[4], tuple[5]));
    }
}
