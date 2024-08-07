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
/// This interface is used to mark instruction sets that can be copied.
/// </summary>
public interface ICopyableInstructionSet : IInstructionSet
{
    /// <summary>
    /// This method must be implemented to perform the copy operation.
    /// </summary>
    /// <returns>The copy of this instruction set.</returns>
    public object Copy();
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
    protected List<ObjectInstruction<TObject>> Instructions = [];

    /// <summary>
    /// This method is used to add a new instruction to the set.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    public void AddInstruction(ObjectInstruction<TObject> instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        Instructions.Add(instruction);
    }

    /// <summary>
    /// This method returns whether then named property is affected by any of the instructions
    /// we carry.
    /// </summary>
    /// <param name="name">The name of the property to check for.</param>
    /// <returns><c>true</c>, if we have an instruction that affects the named property, or
    /// <c>false</c>, if not.</returns>
    public bool TouchesPropertyNamed(string name)
    {
        return Instructions.Any(
            instruction => instruction is ITouchesProperty touchesProperty &&
                           touchesProperty.PropertyName == name);
    }

    /// <summary>
    /// This method is used to run all our instructions.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        CreateObject(variables);
        InitObject(context);
        ApplyInstructions(context, variables);
    }

    /// <summary>
    /// This method may be used by subclasses to perform any initialization on our created
    /// object that is needed.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    protected virtual void CreateObject(Variables variables)
    {
        CreatedObject = new TObject();
    }

    /// <summary>
    /// This method may be used by subclasses to perform any initialization on our created
    /// object that is needed.
    /// </summary>
    /// <param name="context">The current render context.</param>
    protected virtual void InitObject(RenderContext context) {}

    /// <summary>
    /// This method is used to run actually apply our instructions.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    protected void ApplyInstructions(RenderContext context, Variables variables)
    {
        foreach (ObjectInstruction<TObject> instruction in Instructions)
        {
            instruction.Target = CreatedObject;

            instruction.Execute(context, variables);
        }
    }

    /// <summary>
    /// This method is used to copy relevant information from this instruction set to the
    /// one provided.
    /// </summary>
    /// <param name="target">The instruction set to copy into.</param>
    /// <returns>The given target; it makes things easier.</returns>
    protected virtual TSet CopyInto<TSet>(TSet target)
        where TSet : ObjectInstructionSet<TObject>
    {
        target.Instructions = [..Instructions];

        return target;
    }
}

/// <summary>
/// This class represents a set of instructions to run against a particular type of object
/// that can be copied.
/// </summary>
public abstract class CopyableObjectInstructionSet<TObject>
    : ObjectInstructionSet<TObject>, ICopyableInstructionSet
    where TObject : class, new()
{
    /// <summary>
    /// This method must be implemented to perform the copy operation.
    /// </summary>
    /// <returns>The copy of this instruction set.</returns>
    public abstract object Copy();
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
    protected List<InstructionSet<TObject>> Instructions = [];

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

    /// <summary>
    /// This method is used to copy relevant information from this instruction set to the
    /// one provided.
    /// </summary>
    /// <param name="target">The instruction set to copy into.</param>
    /// <returns>The given target; it makes things easier.</returns>
    protected TSet CopyInto<TSet>(TSet target)
        where TSet : ListInstructionSet<TObject>
    {
        target.Instructions = [..Instructions];

        return target;
    }
}

/// <summary>
/// This class represents a set of instructions to run against a list of created objects
/// that can be copied.
/// </summary>
public abstract class CopyableListInstructionSet<TObject>
    : ListInstructionSet<TObject>, ICopyableInstructionSet
    where TObject : class, new()
{
    /// <summary>
    /// This method must be implemented to perform the copy operation.
    /// </summary>
    /// <returns>The copy of this instruction set.</returns>
    public abstract object Copy();
}
