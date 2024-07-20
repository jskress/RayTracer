using System.Linq.Expressions;
using Lex.Parser;
using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the work of setting a property to a value.
/// </summary>
public class SetObjectPropertyInstruction<TObject, TValue>
    : AffectObjectPropertyInstruction<TObject, TValue>
    where TObject : class
{
    private readonly Func<TValue, string> _validator;
    private readonly Term _term;
    private readonly TValue _value;

    private SetObjectPropertyInstruction(
        Expression<Func<TObject, TValue>> propertyLambda, Func<TValue, string> validator,
        Term term, TValue value) : base(propertyLambda)
    {
        _validator = validator;
        _term = term;
        _value = value;
    }

    public SetObjectPropertyInstruction(
        Expression<Func<TObject, TValue>> propertyLambda, Term term, Func<TValue, string> validator = null)
        : this(propertyLambda, validator, term, default) {}

    public SetObjectPropertyInstruction(
        Expression<Func<TObject, TValue>> propertyLambda, TValue value)
        : this(propertyLambda, null, null, value) {}

    /// <summary>
    /// This method is used to execute the instruction to set a string property.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        TValue value = _term == null
            ? _value
            : (TValue) _term.GetValue(variables, typeof(TValue));
        string message = _validator?.Invoke(value);

        if (message != null)
            throw new TokenException(message) { Token = _term?.ErrorToken };

        Setter.Invoke(Target, [Adjust(context, value)]);
    }

    /// <summary>
    /// This method gives any subclasses the opportunity to adjust the given value before
    /// we actually store it.  By default, we do nothing to the value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="value">The value to adjust.</param>
    /// <returns>The (possibly) adjusted value.</returns>
    protected virtual TValue Adjust(RenderContext context, TValue value)
    {
        return value;
    }
}
