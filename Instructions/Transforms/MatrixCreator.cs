using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This class provides the means for creating a single, general matrix.
/// </summary>
public class MatrixCreator : TransformCreator
{
    /// <summary>
    /// A matrix does nothing at all when it is the identity: ones down the diagonal, zeros
    /// everywhere else.  Measuring every one of the sixteen from zero instead would leave a matrix
    /// given no part of the way as sixteen zeros, which is not a transform that does nothing but
    /// one that cannot be undone at all -- and since a moving surface's matrices are inverted to
    /// carry rays into its space, that is a scene that will not render rather than one that looks
    /// wrong.  The numbers of a four by four laid out a row at a time put the diagonal at every
    /// fifth of them.
    /// </summary>
    /// <param name="index">Which of the sixteen numbers is being measured.</param>
    /// <returns>One on the diagonal, zero off it.</returns>
    protected override double IdentityValueAt(int index) => index % 5 == 0 ? 1 : 0;

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
