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
public abstract class InstructionSet<TObject> : Instruction, IInstructionSet
    where TObject : class
{
    /// <summary>
    /// This property returns the object created by executing this instruction set.
    /// </summary>
    public TObject CreatedObject { get; protected set; }
}

/// <summary>
/// This class represents a set of instructions to run against a particular type of object.
/// </summary>
public class ObjectInstructionSet<TObject> : InstructionSet<TObject>
    where TObject : class, new()
{
    /// <summary>
    /// This property holds the list of instructions for the set.
    /// </summary>
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

/// <summary>
/// This class represents a set of instructions to run to create a particular type of
/// object from a child list of objects of the same type.
/// </summary>
public abstract class ListInstructionSet<TObject> : InstructionSet<TObject>
    where TObject : class
{
    /// <summary>
    /// This property holds the list of instruction sets for the set.
    /// </summary>
    protected readonly List<InstructionSet<TObject>> Instructions = [];

    /// <summary>
    /// This method is used to add a new instruction to the set.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    public void AddInstruction(InstructionSet<TObject> instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        Instructions.Add(instruction);
    }

    /// <summary>
    /// This method is used to run all our instructions.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        List<TObject> objects = [];

        foreach (InstructionSet<TObject> instruction in Instructions)
        {
            instruction.Execute(context, variables);

            objects.Add(instruction.CreatedObject);
        }

        CreateTargetFrom(objects);
    }

    /// <summary>
    /// This method should be supplied by subclasses to create the target object based on
    /// the given list of child objects.
    /// </summary>
    /// <param name="objects">The list of child objects.</param>
    protected abstract void CreateTargetFrom(List<TObject> objects);
}
