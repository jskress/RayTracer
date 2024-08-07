using Lex.Clauses;
using RayTracer.Terms;

namespace RayTracer.Extensions;

/// <summary>
/// This class provides us with some useful helpers relating to parsing with Lex.
/// </summary>
public static class ParserExtensions
{
    /// <summary>
    /// This method returns the text of a token in the clause.  If the index is out of
    /// range, an empty string is returned.
    /// </summary>
    /// <param name="clause">The clause to pull the text from.</param>
    /// <param name="index">The index of the desired token; this defaults to the first one.</param>
    /// <returns></returns>
    public static string Text(this Clause clause, int index = 0)
    {
        return index < clause.Tokens.Count ? clause.Tokens[index].Text : string.Empty;
    }

    /// <summary>
    /// This method returns a term from the clause.  If the index is out of
    /// range, <c>null</c> is returned.
    /// </summary>
    /// <param name="clause">The clause to pull the term from.</param>
    /// <param name="index">The index of the desired term; this defaults to the first one.</param>
    /// <returns></returns>
    public static Term Term(this Clause clause, int index = 0)
    {
        return index < clause.Expressions.Count ? (Term) clause.Expressions[index] : null;
    }
}
