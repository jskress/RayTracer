using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This class provides the means for creating a single rotation matrix.
/// </summary>
public class RotationCreator : TransformCreator
{
    /// <summary>
    /// This method is used to create the appropriate rotation matrix, based on the given
    /// values.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="doubles">The array of doubles, if any, our terms resolved to.</param>
    /// <param name="tuples">The array of tuples, if any, our terms resolved to.</param>
    /// <returns>The created matrix.</returns>
    protected override Matrix CreateTransform(
        RenderContext context, double[] doubles, NumberTuple[] tuples)
    {
        return Axis switch
        {
            TransformAxis.X => RayTracer.Basics.Transforms.RotateAroundX(doubles[0], context.AnglesAreRadians),
            TransformAxis.Y => RayTracer.Basics.Transforms.RotateAroundY(doubles[0], context.AnglesAreRadians),
            TransformAxis.Z => RayTracer.Basics.Transforms.RotateAroundZ(doubles[0], context.AnglesAreRadians),
            TransformAxis.None => throw new Exception("Invalid rotation"),
            TransformAxis.All => throw new Exception("Invalid rotation"),
            _ => throw new Exception("Invalid rotation")
        };
    }
}
