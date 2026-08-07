using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to parse a function a scene writes for itself.
    /// <para>
    /// There are two quite different things called functions here and it is worth keeping them apart.
    /// The one an isosurface or a density is given is arithmetic over a point in space, compiled down
    /// to something that can be asked about a place millions of times over.  This one is a scene's
    /// own: named, taking values, and worked out wherever an expression may stand.  A leading word
    /// tells them apart, which is why this is written the way it is rather than hung off the
    /// assignment a scene already has.
    /// </para>
    /// </summary>
    /// <param name="clause">The clause that opens the declaration.</param>
    private void HandleStartFunctionClause(Clause clause)
    {
        string name = clause.Tokens[1].Text;
        (List<string> parameterNames, List<Term> defaults) = ParseFunctionParameters();
        Clause kind = ParseClause("functionKindClause");

        if (kind is null)
            throw CreateUnexpectedInputException("Expecting \"->\" and a kind here.");

        (List<(string, Term)> locals, Term body) = ParseFunctionBody(name);

        _context.InstructionContext.AddInstruction(new DeclareFunctionInstruction
        {
            Name = name,
            ParameterNames = parameterNames,
            Defaults = defaults,
            Kind = kind.Tokens[1].Text,
            Locals = locals,
            Body = body
        });
    }

    /// <summary>
    /// This method reads the values a function takes and what each falls back to.
    /// <para>
    /// A value with something to fall back to may not be followed by one without, since a call
    /// leaves values off the end: allowing it would let a scene write a function that could not be
    /// called in the way it appears to promise.
    /// </para>
    /// </summary>
    /// <returns>The names of the values and their fallbacks.</returns>
    private (List<string>, List<Term>) ParseFunctionParameters()
    {
        List<string> names = [];
        List<Term> defaults = [];

        while (true)
        {
            Clause clause = ParseClause("functionParameterClause");

            if (clause is null)
                break;

            string name = clause.Tokens[0].Text;

            if (names.Contains(name))
            {
                throw new TokenException($"The function already takes a value named '{name}'.")
                {
                    Token = clause.Tokens[0]
                };
            }

            Term fallback = clause.Expressions.Count > 0 ? clause.Term() : null;

            if (fallback is null && defaults.Count > 0 && defaults[^1] is not null)
            {
                throw new TokenException(
                    $"'{name}' has nothing to fall back to, but follows one that has; a call leaves " +
                    "values off the end, so those with fallbacks must come last.")
                {
                    Token = clause.Tokens[0]
                };
            }

            names.Add(name);
            defaults.Add(fallback);
        }

        CurrentParser.MatchToken(
            true, () => "Expecting a close parenthesis to end the parameter list.",
            BounderToken.RightParen);

        return (names, defaults);
    }

    /// <summary>
    /// This method reads what a function works out on its way to its answer, and the answer itself.
    /// <para>
    /// Things worked out along the way are what make a function readable and, sometimes, correct: a
    /// figure a body needs in three places should be arrived at once rather than three times, or the
    /// three will drift apart the first time one of them is edited.
    /// </para>
    /// </summary>
    /// <param name="name">The function's name, for complaining with.</param>
    /// <returns>The things worked out along the way, and the answer.</returns>
    private (List<(string, Term)>, Term) ParseFunctionBody(string name)
    {
        List<(string, Term)> locals = [];

        while (true)
        {
            Clause local = ParseClause("functionLocalClause");

            if (local is null)
                break;

            locals.Add((local.Tokens[0].Text, local.Term()));
        }

        Clause answer = ParseClause("functionReturnClause");

        if (answer is null)
        {
            throw CreateUnexpectedInputException(
                $"The function '{name}' never says what it gives back; it needs a \"return\".");
        }

        Term body = answer.Term();

        CurrentParser.MatchToken(
            true, () => $"Expecting a close brace to end the function '{name}'; nothing may follow " +
                        "its \"return\".", BounderToken.CloseBrace);

        return (locals, body);
    }
}
