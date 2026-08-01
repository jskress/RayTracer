using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Instructions.Transforms;
using RayTracer.Terms;
using BasicsTransforms = RayTracer.Basics.Transforms;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This class collects the 2D transforms written into a path's definition and combines them
/// into a single matrix -- the 2D counterpart of the transforms a surface carries.  A path's
/// points lie in the X/Y plane, so translate and scale take a 2D point (or an X/Y axis and an
/// amount) and rotate takes a single angle, turning the outline within its own plane.  The
/// transforms compose the way a surface's do: the first one written is the first one applied.
/// </summary>
public class TwoDTransformResolver
{
    private readonly record struct TwoDTransform(TransformType Type, TransformAxis Axis, Term Term);

    private readonly List<TwoDTransform> _transforms = [];

    /// <summary>
    /// This property reports whether no transforms have been added, so a path with none can
    /// skip the work entirely.
    /// </summary>
    public bool IsEmpty => _transforms.Count == 0;

    /// <summary>
    /// This method adds a single transform to the list.
    /// </summary>
    /// <param name="type">The sort of transform this is.</param>
    /// <param name="axis">The axis it acts on, where that applies.</param>
    /// <param name="term">The term carrying its value.</param>
    internal void Add(TransformType type, TransformAxis axis, Term term)
    {
        _transforms.Add(new TwoDTransform(type, axis, term));
    }

    /// <summary>
    /// This method combines the transforms into a single matrix.  The first transform written
    /// acts on the raw outline, the next on its result, and so on, which is the reverse of the
    /// order in which the matrices multiply -- so each new matrix goes on the left.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The combined transform.</returns>
    public Matrix Resolve(RenderContext context, Variables variables)
    {
        Matrix result = Matrix.Identity;

        foreach (TwoDTransform transform in _transforms)
            result = ToMatrix(transform, context, variables) * result;

        return result;
    }

    /// <summary>
    /// This method builds the matrix for a single 2D transform.
    /// </summary>
    /// <param name="transform">The transform to build.</param>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The transform's matrix.</returns>
    private static Matrix ToMatrix(TwoDTransform transform, RenderContext context, Variables variables)
    {
        switch (transform.Type)
        {
            case TransformType.Translate:
                return transform.Axis switch
                {
                    TransformAxis.X => BasicsTransforms.Translate(transform.Term.GetValue<double>(variables), 0, 0),
                    TransformAxis.Y => BasicsTransforms.Translate(0, transform.Term.GetValue<double>(variables), 0),
                    _ => TranslateBy(transform.Term.GetValue<TwoDPoint>(variables))
                };
            case TransformType.Scale:
                return transform.Axis switch
                {
                    TransformAxis.X => BasicsTransforms.Scale(transform.Term.GetValue<double>(variables), 1, 1),
                    TransformAxis.Y => BasicsTransforms.Scale(1, transform.Term.GetValue<double>(variables), 1),
                    _ => ScaleBy(transform.Term, variables)
                };
            case TransformType.Rotate:
                return BasicsTransforms.RotateAroundZ(
                    transform.Term.GetValue<double>(variables), context.AnglesAreRadians);
            default:
                throw new Exception($"A path does not support the \"{transform.Type}\" transform.");
        }
    }

    /// <summary>
    /// This method builds a translation from a 2D point.
    /// </summary>
    /// <param name="point">How far to move in X and Y.</param>
    /// <returns>The translation matrix.</returns>
    private static Matrix TranslateBy(TwoDPoint point) => BasicsTransforms.Translate(point.X, point.Y, 0);

    /// <summary>
    /// This method builds a scale from either a 2D point (X and Y factors) or a single number
    /// (the same factor both ways).
    /// </summary>
    /// <param name="term">The term carrying the scale.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The scale matrix.</returns>
    private static Matrix ScaleBy(Term term, Variables variables)
    {
        object value = term.GetValue(variables, typeof(TwoDPoint), typeof(double));

        return value switch
        {
            TwoDPoint point => BasicsTransforms.Scale(point.X, point.Y, 1),
            double factor => BasicsTransforms.Scale(factor, factor, 1),
            _ => throw new Exception("A scale needs a number or a 2D point.")
        };
    }
}
