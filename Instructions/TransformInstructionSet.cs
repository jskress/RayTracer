using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create matrices from a list of them.
/// </summary>
public class TransformInstructionSet : ListInstructionSet<Matrix>
{
    private bool _reversed;

    /// <summary>
    /// This method creates an aggregated matrix that is the product of all our child matrices.
    /// </summary>
    /// <param name="transforms">The list of child objects.</param>
    protected override void CreateTargetFrom(List<Matrix> transforms)
    {
        if (!_reversed)
        {
            Instructions.Reverse();

            _reversed = true;
        }

        CreatedObject = transforms.IsEmpty()
            ? Matrix.Identity
            : transforms[1..].Aggregate(
                transforms[0],
                (accumulator, next) => accumulator * next);
    }
}
