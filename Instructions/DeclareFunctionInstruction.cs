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
    /// This property holds what the function is called.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds the names of the values the function takes.
    /// </summary>
    public IReadOnlyList<string> ParameterNames { get; init; }

    /// <summary>
    /// This property holds what each value falls back to when a call leaves it out.
    /// </summary>
    public IReadOnlyList<Term> Defaults { get; init; }

    /// <summary>
    /// This property holds the kind of thing the function was declared to hand back.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// This property holds the things the body works out on its way to its answer.
    /// </summary>
    public List<(string Name, Term Value)> Locals { get; init; }

    /// <summary>
    /// This property holds the expression the function comes back with.
    /// </summary>
    public Term Body { get; init; }

    /// <summary>
    /// This method binds the function into the scope it was written in.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        variables.SetValue(Name, new UserFunction(
            Name, ParameterNames, Defaults, Kind, Locals, Body, variables));
    }
}
