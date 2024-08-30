using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Patterns;
using RayTracer.Instructions.Pigments;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to parse a clause that defines a pigment.
    /// </summary>
    private IPigmentResolver ParsePigmentClause()
    {
        Clause clause = ParseClause("pigmentClause");
        string text = clause.Text();
        IPigmentResolver resolver;
        
        switch (text)
        {
            case "" or "color":
            {
                Term term = (Term) clause.Expressions.RemoveFirst();

                return new SinglePigmentResolver { Term = term };
            }
            case "blend" or "layer":
                resolver = ParseBlendedPigmentClause(text is "layer");
                break;
            case "noisy":
                resolver = ParseNoisyPigmentClause();
                break;
            default:
                resolver = ParsePatternPigmentClause(clause);
                break;
        }

        CurrentParser.MatchToken(
            true, () => "Expecting a close brace here.", BounderToken.CloseBrace);

        return resolver;
    }

    /// <summary>
    /// This method is used to parse the definition of a blended or layered pigment.
    /// </summary>
    /// <param name="layer">A flag noting whether we need to layer or blend the child
    /// pigments.</param>
    /// <returns>The blended pigment resolver.</returns>
    private BlendedPigmentResolver ParseBlendedPigmentClause(bool layer)
    {
        List<IPigmentResolver> resolvers = [ParsePigmentClause()];

        while (CurrentParser.IsNext(OperatorToken.Comma))
        {
            CurrentParser.GetNextToken(); // Eat the comma.

            resolvers.Add(ParsePigmentClause());
        }

        return new BlendedPigmentResolver
        {
            PigmentResolvers = resolvers,
            LayerResolver = new LiteralResolver<bool> { Value = layer },
            TransformResolver = ParseTransformClause()
        };
    }

    /// <summary>
    /// This method is used to parse the definition of a noisy pigment.
    /// </summary>
    /// <returns>The noisy pigment resolver.</returns>
    private NoisyPigmentResolver ParseNoisyPigmentClause()
    {
        return new NoisyPigmentResolver
        {
            PigmentResolver = ParsePigmentClause(),
            TurbulenceResolver = ParseTurbulenceClause(),
            TransformResolver = ParseTransformClause()
        };
    }

    /// <summary>
    /// This method is used to parse the definition of a patterned pigment.
    /// </summary>
    /// <param name="clause">The clause that defines the pattern.</param>
    /// <returns>The pattern pigment resolver.</returns>
    private PatternPigmentResolver ParsePatternPigmentClause(Clause clause)
    {
        (IPatternResolver resolver, int discretePigmentsNeeded) = ParsePatternClause(clause);

        return new PatternPigmentResolver
        {
            PatternResolver = resolver,
            PigmentSetResolver = discretePigmentsNeeded == 0
                ? ParsePigmentMapClause()
                : ParsePigmentListClause(discretePigmentsNeeded),
            TransformResolver = ParseTransformClause()
        };
    }

    /// <summary>
    /// This method is used to parse out a pigment set resolver.
    /// </summary>
    /// <returns>The appropriate resolver for a pigment set.</returns>
    private Resolver<PigmentSet> ParsePigmentMapClause()
    {
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "pigmentMapClause");
        PigmentMapResolver resolver = new ()
        {
            PigmentResolvers = [],
            BreakValueResolvers = [],
            BandedResolver = new LiteralResolver<bool> { Value = clause.Text() == "banded" }
        };

        while (!CurrentParser.IsNext(BounderToken.CloseBracket))
        {
            Term breakValueTerm = (Term) LanguageDsl.ParseExpression(CurrentParser);

            CurrentParser.MatchToken(true, () => "Expecting a comma here.", OperatorToken.Comma);

            IPigmentResolver pigmentResolver = ParsePigmentClause();
            
            resolver.PigmentResolvers.Add(pigmentResolver);
            resolver.BreakValueResolvers.Add(new TermResolver<double> { Term = breakValueTerm });

            if (!CurrentParser.IsNext(OperatorToken.Comma, BounderToken.CloseBracket))
            {
                throw new TokenException("Expecting a comma or close bracket here.")
                {
                    Token = CurrentParser.GetNextToken()
                };
            }

            if (CurrentParser.IsNext(OperatorToken.Comma))
                CurrentParser.GetNextToken();
        }

        Token bracket = CurrentParser.GetNextToken();

        if (resolver.PigmentResolvers.Count < 2)
        {
            throw new TokenException("At least two pigments should be provided here.")
            {
                Token = bracket
            };
        }

        return resolver;
    }

    /// <summary>
    /// This method is used to parse out a pigment set resolver.
    /// </summary>
    /// <param name="discretePigmentsNeeded"></param>
    /// <returns>The appropriate resolver for a list of pigments.</returns>
    private Resolver<PigmentSet> ParsePigmentListClause(int discretePigmentsNeeded)
    {
        List<IPigmentResolver> resolvers = [];

        for (int _ = 0; _ < discretePigmentsNeeded; _++)
        {
            if (resolvers.Count > 0)
                CurrentParser.MatchToken(true, () => "Expecting a comma here.", OperatorToken.Comma);

            resolvers.Add(ParsePigmentClause());
        }

        return new PigmentListResolver { PigmentResolvers = resolvers };
    }
}
