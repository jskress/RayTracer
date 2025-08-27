using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to parse a clause of one required turbulence.
    /// </summary>
    private TurbulenceResolver ParseTurbulenceClause()
    {
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "turbulenceClause");

        if (clause == null)
            return TurbulenceResolver.NoiseWithNoTurbulenceResolver;

        Resolver<int?> seedResolver = null;
        Resolver<int> depthResolver = new TermResolver<int> { Term = clause.Term() };
        Resolver<int> tightnessResolver = null;
        Resolver<double> scaleResolver = null;
        bool phased = clause.Text(1) == "phased";

        if (phased)
        {
            tightnessResolver = new TermResolver<int> { Term = clause.Term(1) };

            if (clause.Tokens.Count > 2)
                scaleResolver = new TermResolver<double> { Term = clause.Term(2) };
        }

        if (clause.Tokens.Select(token => token.Text).Any(text => text == "seed"))
        {
            Term seedTerm = (Term) clause.Expressions.Last();
            
            seedResolver = new TermResolver<int?> { Term = seedTerm };
        }

        return new TurbulenceResolver
        {
            SeedResolver = seedResolver,
            DepthResolver = depthResolver,
            PhasedResolver = new LiteralResolver<bool> { Value = phased },
            TightnessResolver = tightnessResolver,
            ScaleResolver = scaleResolver
        };
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
}
