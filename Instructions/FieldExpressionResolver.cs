using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to resolve the function of an isosurface: not to a value, as most resolvers do,
/// but to the arithmetic itself, lowered into the form a field is compiled from.
/// <para>
/// It happens here, when the scene is resolved, rather than when it was read, because that is when the
/// scene's own variables have their values -- and a name a scene gave a number to becomes that number
/// in the field, since a compiled field has nowhere to look one up.  A function that cannot mean
/// anything in a field is reported now too, against the text that wrote it, which is well before any
/// ray goes looking for it.
/// </para>
/// </summary>
public class FieldExpressionResolver : Resolver<FieldExpression>
{
    /// <summary>
    /// This property holds the term that was written for the function.
    /// </summary>
    public Term Term { get; init; }

    /// <summary>
    /// This method is used to lower the term into a field expression.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The term, as a field expression.</returns>
    public override FieldExpression Resolve(RenderContext context, Variables variables)
    {
        return Term.ToField(variables);
    }
}
