using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This class provides the means for creating a single, general matrix.
/// </summary>
public class MatrixCreator : TransformCreator
{
    /// <summary>
    /// This method is used to create the appropriate shear matrix, based on the given
    /// values.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="doubles">The array of doubles, if any, our terms resolved to.</param>
    /// <param name="tuples">The array of tuples, if any, our terms resolved to.</param>
    /// <returns>The created matrix.</returns>
    protected override Matrix CreateTransform(
        RenderContext context, double[] doubles, NumberTuple[] tuples)
    {
        return new Matrix(doubles);
    }
}
