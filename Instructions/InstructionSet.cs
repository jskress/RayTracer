using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This is a tagging interface that allows us to use heterogeneous collections of
/// instruction sets without caring about their target object type.
/// </summary>
public interface IInstructionSet
{
    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    void Execute(RenderContext context, Variables variables);
}

/// <summary>
/// This class represents a set of instructions to run against a particular type of object.
/// </summary>
public class InstructionSet<TObject> : Instruction, IInstructionSet
    where TObject : class, new()
{
    /// <summary>
    /// This property returns the object created by executing this instruction set.
    /// </summary>
    public TObject CreatedObject { get; private set; }

    private readonly List<ObjectInstruction<TObject>> _instructions = [];

    /// <summary>
    /// This method is used to add a new instruction to the set.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    public void AddInstruction(ObjectInstruction<TObject> instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        _instructions.Add(instruction);
    }

    /// <summary>
    /// This method is used to run all our instructions.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        CreatedObject = new TObject();

        foreach (ObjectInstruction<TObject> instruction in _instructions)
        {
            instruction.Target = CreatedObject;

            instruction.Execute(context, variables);
        }
    }
}
