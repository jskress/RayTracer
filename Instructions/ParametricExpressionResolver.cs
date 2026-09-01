using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class lowers one of a parametric surface's three expressions into the form a field is
/// compiled from, with <c>u</c> and <c>v</c> standing for the two parameters.
/// <para>
/// **The two parameters are bound as names rather than added to the field machinery**, which already
/// lets a name hold a piece of field arithmetic -- that is how one shape function comes to be handed
/// to another.  So <c>u</c> and <c>v</c> are simply names holding the first and second of the three
/// things a field is a function of, and nothing in <c>Fields</c> had to learn about them.
/// </para>
/// <para>
/// **A caution that has no cure yet**: a field's own names for those three are <c>x</c>, <c>y</c> and
/// <c>z</c>, and they are recognised before any name a scene has bound.  So <c>x</c> written inside
/// one of these expressions means the same thing <c>u</c> does, and <c>z</c> means nought.  Neither is
/// what anybody means by it.  Telling them apart afterward is impossible -- <c>u</c> lowers *into* the
/// first of the three -- so this is a thing the guide has to say rather than something the parser can
/// catch.
/// </para>
/// </summary>
public class ParametricExpressionResolver : Resolver<FieldExpression>
{
    /// <summary>
    /// This property holds the term that was written for this coordinate.
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
        Variables scope = new (variables);

        scope.SetValue("u", FieldVariable.X);
        scope.SetValue("v", FieldVariable.Y);

        return Term.ToField(scope);
    }
}
