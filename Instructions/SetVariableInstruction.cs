using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to assign a value to a variable.
/// </summary>
public class SetVariableInstruction : Instruction
{
    private readonly string _variableName;
    private readonly Term _term;
    private readonly IObjectResolver _objectResolver;

    public SetVariableInstruction(string variableName, Term term = null, IObjectResolver objectResolver = null)
    {
        _variableName = variableName;
        _term = term;
        _objectResolver = objectResolver;
    }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        object value = _term == null
            ? _objectResolver.ResolveToObject(context, variables)
            : _term.GetValue(variables);

        variables.SetValue(_variableName, value);
    }
}
