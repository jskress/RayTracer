using System.Linq.Expressions;
using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the work of setting a string property to a value by either direct
/// setting or by appending a new text value.
/// </summary>
public class AppendStringPropertyInstruction<TObject> : AffectObjectPropertyInstruction<TObject, string>
    where TObject : class
{
    private readonly Term _term;

    public AppendStringPropertyInstruction(Expression<Func<TObject, string>> propertyLambda, Term term)
        : base(propertyLambda)
    {
        _term = term;
    }

    /// <summary>
    /// This method is used to execute the instruction to set a string property.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        string current = (string)Getter.Invoke(Target, null);
        string value = (string) _term.GetValue(variables, typeof(string));

        value = string.IsNullOrEmpty(current) || value == null
            ? value
            : $"{current}\n{value}";

        Setter.Invoke(Target, [value]);
    }
}
