using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to give a scene a function of its own.
/// <para>
/// It runs where it was written, and that is the point of it being an instruction rather than
/// something settled while parsing: the function is bound into the scope it was declared in, and
/// carries that same scope away with it as the one its body will be worked out against.  A function
/// written in an included file therefore sees what that file set up, wherever it is later called
/// from.
/// </para>
/// </summary>
public class DeclareFunctionInstruction : Instruction
{
    /// <summary>
    /// This property holds the function, as yet belonging to no scope.
    /// </summary>
    public UserFunction Function { get; init; }

    /// <summary>
    /// This method binds the function into the scope it was written in.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        variables.SetValue(Function.Name, Function.BoundTo(variables));
    }
}
