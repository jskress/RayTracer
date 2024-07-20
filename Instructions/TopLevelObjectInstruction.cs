using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create a top-level object.  It wraps an object creation
/// instruction and, when executed, executes the instruction to produce the object that
/// then gets added to the instruction context we were provided upon construction.
/// </summary>
public class TopLevelObjectInstruction<TObject> : Instruction
    where TObject : class, new()
{
    private readonly InstructionContext _context;
    private readonly InstructionSet<TObject> _instructionSet;

    public TopLevelObjectInstruction(InstructionContext context, InstructionSet<TObject> instructionSet)
    {
        _context = context;
        _instructionSet = instructionSet;

        context.AddInstruction(this);
    }

    /// <summary>
    /// This method is used to run all our instructions.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        _instructionSet.Execute(context, variables);
        _context.AddTopLevelObject(_instructionSet.CreatedObject);
    }
}
