using Lex.Clauses;
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
    /// This method is used to parse a clause of one required turbulence.
    /// </summary>
    private TurbulenceResolver ParseTurbulenceClause()
    {
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "turbulenceClause");

        if (clause == null)
            return TurbulenceResolver.NoiseWithNoTurbulenceResolver;

        Resolver<int> depthResolver = new TermResolver<int> { Term = clause.Term() };
        Resolver<int> tightnessResolver = null;
        Resolver<double> scaleResolver = null;
        bool phased = clause.Tokens.Count > 1;

        if (phased)
        {
            tightnessResolver = new TermResolver<int> { Term = clause.Term(1) };

            if (clause.Tokens.Count > 2)
                scaleResolver = new TermResolver<double> { Term = clause.Term(2) };
        }

        return new TurbulenceResolver
        {
            DepthResolver = depthResolver,
            PhasedResolver = new LiteralResolver<bool> { Value = phased },
            TightnessResolver = tightnessResolver,
            ScaleResolver = scaleResolver
        };
    }
}
