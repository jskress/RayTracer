using Lex.Parser;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class resolves how much of each melanin hair holds: a named natural shade, an amount of
/// eumelanin alone, or amounts of eumelanin and pheomelanin.
/// </summary>
public class MelaninResolver : Resolver<Melanin?>
{
    /// <summary>
    /// This property holds the term for the shade's name or the amount of eumelanin.
    /// </summary>
    public Term First { get; init; }

    /// <summary>
    /// This property holds the term for the amount of pheomelanin, if one was given.
    /// </summary>
    public Term Second { get; init; }

    /// <summary>
    /// This method resolves the melanin.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>How much of each melanin.</returns>
    public override Melanin? Resolve(RenderContext context, Variables variables)
    {
        object first = First.GetValue(variables, typeof(double), typeof(string));

        if (first is string name)
        {
            if (Second is not null)
                throw Complaint(First, "A named shade of hair is written alone.");

            if (Melanin.Shades.TryGetValue(name.ToLowerInvariant(), out Melanin shade))
                return shade;

            throw Complaint(
                First, $"There is no shade of hair called '{name}'; the shades are " +
                       $"{string.Join(", ", Melanin.Shades.Keys.SkipLast(1))} and {Melanin.Shades.Keys.Last()}.");
        }

        if (first is not double eumelanin)
            throw Complaint(First, "Melanin is a named shade of hair or an amount.");

        double pheomelanin = Second is null ? 0 : Second.GetValue<double>(variables);

        if (eumelanin < 0 || pheomelanin < 0)
            throw Complaint(eumelanin < 0 ? First : Second, "An amount of melanin may not be less than nought.");

        return new Melanin(eumelanin, pheomelanin);
    }

    private static TokenException Complaint(Term term, string message) => new (message) { Token = term.ErrorToken };
}
