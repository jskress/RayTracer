using System.Linq.Expressions;

namespace RayTracer.Fields;

/// <summary>
/// This class is the base of the small tree a field function is held in: the arithmetic of one
/// number, in three variables, and nothing else.
/// <para>
/// The DSL's own <see cref="RayTracer.Terms.Term"/> tree is what a scene writes, and it is built for
/// a different job -- it binds late, deals in values of any type, and is evaluated once per
/// instruction.  A field is asked for its value millions of times per frame, always in doubles, so
/// what a scene wrote is lowered into this before anything is done with it (see
/// <see cref="RayTracer.Terms.Term.ToField"/>).  Being a closed set of nodes over one type is what
/// makes the three things a marcher needs possible: the tree can be compiled to a delegate, it can be
/// differentiated to give a gradient, and it can be evaluated over a range of inputs to bound what it
/// might do there.
/// </para>
/// </summary>
public abstract class FieldExpression
{
    /// <summary>
    /// This property holds the value this expression always has, if it has one -- which is to say if
    /// it names no variable anywhere within it.  It is what lets the arithmetic of constants be done
    /// once, while the tree is being built, rather than at every point the field is asked about.
    /// </summary>
    public virtual double? ConstantValue => null;

    /// <summary>
    /// This method is used to emit this expression as the .NET expression tree that will be compiled.
    /// </summary>
    /// <param name="x">The parameter standing for the X of the point being asked about.</param>
    /// <param name="y">The parameter standing for its Y.</param>
    /// <param name="z">The parameter standing for its Z.</param>
    /// <returns>This expression, as a .NET expression.</returns>
    internal abstract Expression ToDotNet(
        ParameterExpression x, ParameterExpression y, ParameterExpression z);

    /// <summary>
    /// This method is used to differentiate this expression with respect to one of the three
    /// variables, giving another expression of the same kind.
    /// <para>
    /// A marcher needs the gradient for two things: the surface normal at a hit, and Newton's method
    /// for the last few digits of where that hit is.  Differentiating symbolically rather than by
    /// taking differences of the function at nearby points gives an exact answer with no step size to
    /// choose -- and choosing one is a poor bargain, since too large blurs a sharp edge and too small
    /// is swamped by the rounding of the subtraction.
    /// </para>
    /// </summary>
    /// <param name="axis">The variable to differentiate with respect to.</param>
    /// <returns>The derivative, as a field expression.</returns>
    public abstract FieldExpression Differentiate(FieldAxis axis);

    /// <summary>
    /// This method is used to work out the range of values this expression could take, given ranges
    /// for the three variables -- a box of space, in other words, and what the field might do anywhere
    /// within it.
    /// <para>
    /// This is what a marcher skips on, and the rule it must obey is one-sided: a range may be wider
    /// than the truth but never narrower.  Too wide only costs work, while too narrow makes a surface
    /// vanish in patches, so every rule errs outward and anything unknown answers
    /// <see cref="FieldRange.Anywhere"/>, which rules nothing out.
    /// </para>
    /// </summary>
    /// <param name="x">The range of X to consider.</param>
    /// <param name="y">The range of Y.</param>
    /// <param name="z">The range of Z.</param>
    /// <returns>The range of values the expression could take there.</returns>
    public abstract FieldRange Bound(FieldRange x, FieldRange y, FieldRange z);

    /// <summary>
    /// This method is used to describe this expression, for error messages and for tests that care
    /// about the shape of a tree rather than what it evaluates to.
    /// </summary>
    /// <returns>The expression, written out.</returns>
    public abstract override string ToString();
}
