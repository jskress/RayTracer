using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions;

public class TransformInstruction : InstructionSet<Matrix>
{
    /// <summary>
    /// This method is used to create a translation instruction.
    /// </summary>
    /// <param name="term">The term that supplies the instruction's value.</param>
    /// <param name="axis">The axis along which the translation should occur.</param>
    /// <returns>The translation instruction.</returns>
    public static TransformInstruction TranslationInstruction(Term term, TransformAxis axis)
    {
        return new TransformInstruction(TransformType.Translate, axis, [term]);
    }

    /// <summary>
    /// This method is used to create a scale instruction.
    /// </summary>
    /// <param name="term">The term that supplies the instruction's value.</param>
    /// <param name="axis">The axis along which the scale should occur.</param>
    /// <returns>The scale instruction.</returns>
    public static TransformInstruction ScaleInstruction(Term term, TransformAxis axis)
    {
        return new TransformInstruction(TransformType.Scale, axis, [term]);
    }

    /// <summary>
    /// This method is used to create a rotate instruction.
    /// </summary>
    /// <param name="term">The term that supplies the instruction's value.</param>
    /// <param name="axis">The axis around which the rotation should occur.</param>
    /// <returns>The rotate instruction.</returns>
    public static TransformInstruction RotationInstruction(Term term, TransformAxis axis)
    {
        return new TransformInstruction(TransformType.Rotate, axis, [term]);
    }

    /// <summary>
    /// This method is used to create a shear instruction.
    /// </summary>
    /// <param name="terms">The terms that supply the instruction's values.</param>
    /// <returns>The shear instruction.</returns>
    public static TransformInstruction ShearInstruction(Term[] terms)
    {
        return new TransformInstruction(TransformType.Shear, TransformAxis.None, terms);
    }

    /// <summary>
    /// This method is used to create a matrix instruction.
    /// </summary>
    /// <param name="terms">The terms that supply the instruction's values.</param>
    /// <returns>The matrix instruction.</returns>
    public static TransformInstruction MatrixInstruction(Term[] terms)
    {
        return new TransformInstruction(TransformType.Matrix, TransformAxis.None, terms);
    }

    private readonly TransformType _type;
    private readonly TransformAxis _axis;
    private readonly Term[] _terms;
    private readonly Type[] _expectedTypes;

    private TransformInstruction(
        TransformType type, TransformAxis axis, Term[] terms)
    {
        _type = type;
        _terms = terms;
        _axis = axis;
        _expectedTypes = axis == TransformAxis.All
            ? [typeof(NumberTuple), typeof(double)]
            : [typeof(double)];
    }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        object[] values = _terms
            .Select(term => term.GetValue(variables, _expectedTypes))
            .ToArray();
        double[] doubles = values
            .Where(value => value is double)
            .Cast<double>()
            .ToArray();
        NumberTuple[] tuples = values
            .Where(value => value is NumberTuple)
            .Cast<NumberTuple>()
            .ToArray();

        CreatedObject = _type switch
        {
            // Translations
            TransformType.Translate when _axis == TransformAxis.X =>
                Transforms.Translate(doubles[0], 0, 0),
            TransformType.Translate when _axis == TransformAxis.Y =>
                Transforms.Translate(0, doubles[0], 0),
            TransformType.Translate when _axis == TransformAxis.Z =>
                Transforms.Translate(0, 0, doubles[0]),
            TransformType.Translate when doubles.Length > 0 =>
                Transforms.Translate(doubles[0], doubles[0], doubles[0]),
            TransformType.Translate when tuples.Length > 0 =>
                Transforms.Translate(tuples[0].X, tuples[0].Y, tuples[0].Z),
            // Scales
            TransformType.Scale when _axis == TransformAxis.X =>
                Transforms.Scale(doubles[0], 1, 1),
            TransformType.Scale when _axis == TransformAxis.Y =>
                Transforms.Scale(1, doubles[0], 1),
            TransformType.Scale when _axis == TransformAxis.Z =>
                Transforms.Scale(1, 1, doubles[0]),
            TransformType.Scale when doubles.Length > 0 =>
                Transforms.Scale(doubles[0]),
            TransformType.Scale when tuples.Length > 0 =>
                Transforms.Scale(tuples[0].X, tuples[0].Y, tuples[0].Z),
            // Rotations
            TransformType.Rotate when _axis == TransformAxis.X =>
                Transforms.RotateAroundX(doubles[0], context.AnglesAreRadians),
            TransformType.Rotate when _axis == TransformAxis.Y =>
                Transforms.RotateAroundY(doubles[0], context.AnglesAreRadians),
            TransformType.Rotate when _axis == TransformAxis.Z =>
                Transforms.RotateAroundZ(doubles[0], context.AnglesAreRadians),
            TransformType.Shear => Transforms.Shear(
                doubles[0], doubles[1], doubles[2],
                doubles[3], doubles[4], doubles[5]),
            TransformType.Matrix => new Matrix(doubles),
            _ => null
        };
    }
}
