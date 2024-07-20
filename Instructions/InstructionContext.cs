using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class is the root of our instruction set structure.  It is used to manage the
/// execution of all the instructions found in our input source in preparation for rendering
/// a single image.
/// </summary>
public class InstructionContext
{
    private readonly List<Instruction> _instructions = [];
    private readonly List<object> _objects = [];

    /// <summary>
    /// This method is used to add a new instruction to the set.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    public void AddInstruction(Instruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        _instructions.Add(instruction);
    }

    /// <summary>
    /// This method is used to add a top-level object.
    /// </summary>
    /// <param name="value">The top-level object to add.</param>
    internal void AddTopLevelObject(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _objects.Add(value);
    }

    /// <summary>
    /// This method is used to run all our instructions.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public void Execute(RenderContext context, Variables variables)
    {
        foreach (Instruction instruction in _instructions)
            instruction.Execute(context, variables);
    }
}
