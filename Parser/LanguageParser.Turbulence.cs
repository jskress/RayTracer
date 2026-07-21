using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to parse a turbulence clause.  It may be written either as a bare
    /// amount, which is the common case and names the amplitude alone, or as a block when there is
    /// more to say than that.
    /// </summary>
    private TurbulenceResolver ParseTurbulenceClause()
    {
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "turbulenceClause");

        if (clause == null)
            return TurbulenceResolver.NoiseWithNoTurbulenceResolver;

        // The shorthand: everything but the amplitude keeps its default.
        if (!BounderToken.OpenBrace.Matches(clause.Tokens[^1]))
        {
            return new TurbulenceResolver
            {
                AmplitudeResolver = new TermResolver<object> { Term = clause.Term() }
            };
        }

        TurbulenceResolver resolver = new ();

        _ = ParseObjectResolver("turbulenceEntryClause", HandleTurbulenceEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle one property of a turbulence block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleTurbulenceEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid turbulence property here.");

        TurbulenceResolver resolver = (TurbulenceResolver) _context.CurrentTarget;

        switch (ToCmd(clause))
        {
            case "amplitude":
                // Deliberately typed loosely: this may be one number, meaning the same push on
                // every axis, or a triple giving each its own.
                resolver.AmplitudeResolver = new TermResolver<object> { Term = clause.Term() };
                break;
            case "octaves":
                resolver.OctavesResolver = new TermResolver<int> { Term = clause.Term() };
                break;
            case "finer":
                resolver.FinerResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            case "fainter":
                resolver.FainterResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            case "with.seed":
                resolver.SeedResolver = new TermResolver<int?> { Term = clause.Term() };
                break;
            default:
                throw new NotSupportedException("Unknown turbulence property found.");
        }
    }

    /// <summary>
    /// This method is used to parse a resolver for an optional turbulence clause.
    /// </summary>
    /// <returns>A resolver for turbulence that is optional.</returns>
    private Resolver<Turbulence> ParseOptionalTurbulence()
    {
        Token token = CurrentParser.PeekNextToken();

        return token is KeywordToken && token.Text == "turbulence"
            ? ParseTurbulenceClause()
            : new LiteralResolver<Turbulence> { Value = null };
    }

    /// <summary>
    /// This method is used to parse the noise clause a mottled pigment carries.  It takes the
    /// layers and nothing else: an amplitude says how far to push a point, and mottling pushes no
    /// points.
    /// </summary>
    private Resolver<LayeredNoise> ParseNoiseClause()
    {
        // Consume the "noise {" that opens the block before reading what is inside it.
        _ = LanguageDsl.ParseClause(CurrentParser, "noiseClause");

        LayeredNoiseResolver<LayeredNoise> resolver = new ();

        _ = ParseObjectResolver("noiseEntryClause", HandleNoiseEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle one property of a noise block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleNoiseEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid noise property here.");

        LayeredNoiseResolver<LayeredNoise> resolver =
            (LayeredNoiseResolver<LayeredNoise>) _context.CurrentTarget;

        switch (ToCmd(clause))
        {
            case "octaves":
                resolver.OctavesResolver = new TermResolver<int> { Term = clause.Term() };
                break;
            case "finer":
                resolver.FinerResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            case "fainter":
                resolver.FainterResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            case "with.seed":
                resolver.SeedResolver = new TermResolver<int?> { Term = clause.Term() };
                break;
            default:
                throw new NotSupportedException("Unknown noise property found.");
        }
    }
}
