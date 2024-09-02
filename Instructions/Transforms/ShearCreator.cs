using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This class provides the means for creating a single shear matrix.
/// </summary>
public class ShearCreator : TransformCreator
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
        return RayTracer.Basics.Transforms.Shear(
            doubles[0], doubles[1], doubles[2],
            doubles[3], doubles[4], doubles[5]);
    }
}
