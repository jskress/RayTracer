using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This class provides the means for creating a single scale matrix.
/// </summary>
public class ScaleCreator : TransformCreator
{
    /// <summary>
    /// This method is used to create the appropriate scale matrix, based on the given
    /// values.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="doubles">The array of doubles, if any, our terms resolved to.</param>
    /// <param name="tuples">The array of tuples, if any, our terms resolved to.</param>
    /// <returns>The created matrix.</returns>
    protected override Matrix CreateTransform(RenderContext context, double[] doubles, NumberTuple[] tuples)
    {
        return Axis switch
        {
            TransformAxis.X => RayTracer.Basics.Transforms.Scale(doubles[0], 1, 1),
            TransformAxis.Y => RayTracer.Basics.Transforms.Scale(1, doubles[0], 1),
            TransformAxis.Z => RayTracer.Basics.Transforms.Scale(1, 1, doubles[0]),
            TransformAxis.All when doubles.Length > 0 =>
                RayTracer.Basics.Transforms.Scale(doubles[0]),
            TransformAxis.All when tuples.Length > 0 =>
                RayTracer.Basics.Transforms.Scale(tuples[0].X, tuples[0].Y, tuples[0].Z),
            TransformAxis.None => throw new Exception("Invalid scale"),
            _ => throw new Exception("Invalid scale")
        };
    }
}
