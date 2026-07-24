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
    /// <summary>
    /// A matrix's sixteen numbers do not come apart into a thing that can be done by degrees --
    /// scaling them toward zero does not give a transform half way to it, it gives nonsense -- so
    /// a bare matrix cannot serve as a motion.
    /// </summary>
    protected override bool CanBeGivenInPart => false;

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
