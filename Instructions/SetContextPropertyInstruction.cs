using System.Linq.Expressions;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the work of setting a property on the current render context to
/// a value.
/// </summary>
public class SetContextPropertyInstruction<TValue> : SetObjectPropertyInstruction<RenderContext, TValue>
{
    public SetContextPropertyInstruction(
        Expression<Func<RenderContext, TValue>> propertyLambda, Term term,
        Func<TValue, string> validator = null) : base(propertyLambda, term, validator) {}

    public SetContextPropertyInstruction(
        Expression<Func<RenderContext, TValue>> propertyLambda, TValue value)
        : base(propertyLambda, value) {}

    /// <summary>
    /// This method is used to execute the instruction to set a string property.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        Target = context;
        
        base.Execute(context, variables);
    }
}
