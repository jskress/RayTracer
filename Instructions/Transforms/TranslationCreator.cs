using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This class provides the means for creating a single translation matrix.
/// </summary>
public class TranslationCreator : TransformCreator
{
    /// <summary>
    /// This method is used to create the appropriate translation matrix, based on the
    /// given values.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="doubles">The array of doubles, if any, our terms resolved to.</param>
    /// <param name="tuples">The array of tuples, if any, our terms resolved to.</param>
    /// <returns>The created matrix.</returns>
    protected override Matrix CreateTransform(RenderContext context, double[] doubles, NumberTuple[] tuples)
    {
        return Axis switch
        {
            TransformAxis.X => RayTracer.Basics.Transforms.Translate(doubles[0], 0, 0),
            TransformAxis.Y => RayTracer.Basics.Transforms.Translate(0, doubles[0], 0),
            TransformAxis.Z => RayTracer.Basics.Transforms.Translate(0, 0, doubles[0]),
            TransformAxis.All when doubles.Length > 0 =>
                RayTracer.Basics.Transforms.Translate(doubles[0]),
            TransformAxis.All when tuples.Length > 0 =>
                RayTracer.Basics.Transforms.Translate(tuples[0].X, tuples[0].Y, tuples[0].Z),
            TransformAxis.None => throw new Exception("Invalid translate"),
            _ => throw new Exception("Invalid translate")
        };
    }
}
