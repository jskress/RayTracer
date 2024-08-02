using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create matrices from a list of them.
/// </summary>
public class TransformInstructionSet : CopyableListInstructionSet<Matrix>
{
    private bool _reversed;

    /// <summary>
    /// This method is used to run all our instructions.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        if (!_reversed)
        {
            Instructions.Reverse();

            _reversed = true;
        }

        base.Execute(context, variables);
    }

    /// <summary>
    /// This method creates an aggregated matrix that is the product of all our child matrices.
    /// </summary>
    /// <param name="transforms">The list of child objects.</param>
    protected override void CreateTargetFrom(List<Matrix> transforms)
    {
        CreatedObject = transforms.IsEmpty()
            ? Matrix.Identity
            : transforms[1..].Aggregate(
                transforms[0],
                (accumulator, next) => accumulator * next);
    }
    /// <summary>
    /// This method creates a copy of this instruction set,
    /// </summary>
    /// <returns>The copy of this instruction set.</returns>
    public override object Copy()
    {
        return CopyInto(new TransformInstructionSet());
    }
}
