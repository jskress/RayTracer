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

    public SetVariableInstruction(string variableName, Term term)
    {
        _variableName = variableName;
        _term = term;
    }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        object value = _term.GetValue(variables);

        variables.SetValue(_variableName, value);
    }
}

/// <summary>
/// This class is used to assign an instruction set-generated value to a variable.
/// </summary>
public class SetVariableInstruction<TObject> : Instruction
    where TObject : class
{
    private readonly string _variableName;
    private readonly InstructionSet<TObject> _instructionSet;

    public SetVariableInstruction(string variableName, InstructionSet<TObject> instructionSet)
    {
        _variableName = variableName;
        _instructionSet = instructionSet;
    }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        _instructionSet.Execute(context, variables);

        variables.SetValue(_variableName, _instructionSet.CreatedObject);
    }
}
