using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create a top-level object.  It wraps an object creation
/// resolver and, when executed, executes the instruction to produce the object that
/// then gets added to the instruction context we were provided upon construction.
/// </summary>
public class TopLevelObjectCreator : Instruction
{
    /// <summary>
    /// This property holds the context to add tne created object to.
    /// </summary>
    public InstructionContext Context { get; init; }

    /// <summary>
    /// This property holds the resolver that will produce the object.
    /// </summary>
    public IObjectResolver Resolver { get; init; }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        Context.AddTopLevelObject(Resolver.ResolveToObject(context, variables));
    }
}
