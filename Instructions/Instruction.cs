using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This is the base class for all instructions.
/// </summary>
public abstract class Instruction
{
    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public abstract void Execute(RenderContext context, Variables variables);
}
