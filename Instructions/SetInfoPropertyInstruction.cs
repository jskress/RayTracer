using System.Linq.Expressions;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the work of setting a property on the current render context's
/// image information to a value.
/// </summary>
public class SetInfoPropertyInstruction<TValue> : SetObjectPropertyInstruction<ImageInformation, TValue>
{
    public SetInfoPropertyInstruction(
        Expression<Func<ImageInformation, TValue>> propertyLambda, Term term,
        Func<TValue, string> validator = null)
        : base(propertyLambda, term, validator) {}

    /// <summary>
    /// This method is used to execute the instruction to set a string property.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        context.ImageInformation ??= new ImageInformation();

        Target = context.ImageInformation;

        base.Execute(context, variables);
    }
}
