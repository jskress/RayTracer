using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Patterns;
using RayTracer.Instructions.Surfaces;
using RayTracer.Instructions.Transforms;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to parse a clause that defines how a surface is roughened.
    /// <para>
    /// It is written as the pigment's sibling and read much as one, since it is the same thing
    /// pointed at a different property: a pattern, placed over the surface, whose slope tilts the
    /// normal rather than choosing a colour.  Everything the pattern grammar offers -- turbulence,
    /// the waves, the transforms -- therefore comes along for nothing.
    /// </para>
    /// </summary>
    /// <returns>The surface normal resolver.</returns>
    private SurfaceNormalResolver ParseNormalClause()
    {
        Clause clause = ParseClause("withSeedClause");
        SurfaceNormalResolver resolver = new ();

        if (clause != null)
            resolver.SeedResolver = new TermResolver<int?> { Term = clause.Term() };

        clause = ParseClause("startNormalClause");

        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a pattern to follow \"normal\" here.");

        (IPatternResolver patternResolver, _) = ParsePatternClause(clause);

        resolver.PatternResolver = patternResolver;

        // The depth and the transforms may be written in either order, and as many times as the
        // scene likes, so both are looked for until neither is there.
        //
        // What stops this is where the parser stands rather than what came back.  A transform
        // clause is a repeating one, so it matches happily against no transforms at all and hands
        // back a resolver having read nothing; asking whether it gave us something would spin here
        // forever on the first property that is neither.
        while (true)
        {
            (int line, int column) = Position();
            Clause depth = ParseClause("normalEntryClause");

            if (depth != null)
                resolver.DepthResolver = new TermResolver<double> { Term = depth.Term() };
            else
                resolver.TransformResolver = ParseTransformClause(resolver.TransformResolver);

            if (Position() == (line, column))
                break;
        }

        CurrentParser.MatchToken(
            true, () => "Expecting a close brace here.", BounderToken.CloseBrace);

        return resolver;
    }

    /// <summary>
    /// This method reports where the parser is standing, so that a loop may tell whether reading
    /// something actually moved it along.
    /// </summary>
    /// <returns>The line and column the next token starts at.</returns>
    private (int Line, int Column) Position()
    {
        Token token = CurrentParser.PeekNextToken();

        return token is null ? (-1, -1) : (token.Line, token.Column);
    }
}
