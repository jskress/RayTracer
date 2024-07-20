using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the work of setting the copyright statement for generated
/// images.
/// </summary>
public class SetCopyrightInstruction : Instruction
{
    private readonly Term _term;

    public SetCopyrightInstruction(Term term)
    {
        _term = term;
    }

    /// <summary>
    /// This method is used to execute the instruction to set the copyright statement.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        object value = _term.GetValue(variables, typeof(string), typeof(bool));

        if (value is bool booleanValue)
        {
            context.ImageInformation.Copyright = booleanValue
                ? $"Copyright \u00a9 {DateTime.Now.Year}"
                : null;
        }
        else
            context.ImageInformation.Copyright = value.ToString();
    }
}
