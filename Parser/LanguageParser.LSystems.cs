using System.Text;
using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Geometry.LSystems;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Surfaces.LSystems;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of an L-system block.
    /// </summary>
    /// <param name="clause">The clause that starts the L-system block.</param>
    private void HandleStartLSystemClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Text");

        LSystemResolver resolver = ParseLSystemClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create an L-system resolver from an L-system resolver block.
    /// </summary>
    /// <param name="clause">The clause that starts the L-system.</param>
    private LSystemResolver ParseLSystemClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<LSystemResolver>(
                // ReSharper disable once StringLiteralTypo
                "lsystemEntryClause", HandleLSystemEntryClause),
            // ReSharper disable once StringLiteralTypo
            "lsystemEntryClause", HandleLSystemEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of an L-system block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleLSystemEntryClause(Clause clause)
    {
        LSystemResolver resolver = (LSystemResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
        {
            Term term = clause.Term();

            switch (ToCmd(clause))
            {
                case "axiom":
                    resolver.AxiomResolver = new TermResolver<string> { Term = term };
                    break;
                case "generations":
                    resolver.GenerationsResolver = new TermResolver<int> { Term = term };
                    break;
                case "controls":
                    resolver.RenderingControlsResolver = ParseLSystemRenderingControlsClause();
                    break;
                case "commands":
                    ParseCommandMappingsClause(resolver, clause);
                    break;
                case "productions":
                    resolver.ProductionRuleResolvers = ParseProductionRulesClause(clause);
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "l-system");
                    break;
            }
        }
    }

    /// <summary>
    /// This method is used to create an L-system rendering controls resolver from an
    /// L-system rendering controls resolver block.
    /// </summary>
    private LSystemRenderingControlsResolver ParseLSystemRenderingControlsClause()
    {
        return ParseObjectResolver<LSystemRenderingControlsResolver>(
            // ReSharper disable once StringLiteralTypo
            "lsystemRenderingControlsEntryClause", HandleLSystemRenderingControlsEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of an L-system rendering controls block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleLSystemRenderingControlsEntryClause(Clause clause)
    {
        LSystemRenderingControlsResolver resolver = (LSystemRenderingControlsResolver) _context.CurrentTarget;
        Term term = clause.Term();

        switch (ToCmd(clause))
        {
            case "extrusion":
                resolver.RenderTypeResolver = new LiteralResolver<LSystemRendererType>
                {
                    Value = LSystemRendererType.Extrusion
                };
                break;
            case "pipes":
                resolver.RenderTypeResolver = new LiteralResolver<LSystemRendererType>
                {
                    Value = LSystemRendererType.Pipes
                };
                break;
            case "angle":
                resolver.AngleResolver = new AngleResolver { Term = term };
                break;
            case "length":
                resolver.LengthResolver = new TermResolver<double> { Term = term };
                break;
            case "diameter":
                resolver.DiameterResolver = new TermResolver<double> { Term = term };
                break;
            case "factor":
                resolver.FactorResolver = new TermResolver<double> { Term = term };
                break;
            default:
                throw new NotSupportedException("Unknown rendering controls property found.");
        }
    }

    /// <summary>
    /// This method is used to handle a commands block.
    /// </summary>
    /// <param name="resolver">The resolver to add the command mappings to.</param>
    /// <param name="clause">The clause to process.</param>
    private static void ParseCommandMappingsClause(LSystemResolver resolver, Clause clause)
    {
        int tokenIndex = 2;

        while (tokenIndex < clause.Tokens.Count && !BounderToken.CloseBrace.Matches(clause.Tokens[tokenIndex]))
        {
            Rune commandCharacter = ParseCommandCharacter(clause, tokenIndex);

            tokenIndex += 2;

            TurtleCommand command = Enum.Parse<TurtleCommand>(clause.Tokens[tokenIndex].Text, true);

            resolver.CommandMappings.Add(new LSystemRenderCommandMapping
            {
                CommandCharacter = commandCharacter,
                TurtleCommand = command
            });

            tokenIndex++;
        }
    }

    /// <summary>
    /// This method is used to parse the command character for a render command mapping.
    /// </summary>
    /// <param name="clause">The clause to pull the variable from.</param>
    /// <param name="tokenIndex">The index at which to expect the variable.</param>
    /// <returns>The token index following the variable.</returns>
    private static Rune ParseCommandCharacter(Clause clause, int tokenIndex)
    {
        string command = clause.Tokens[tokenIndex].Text;
        Rune[] runes = command.AsRunes();

        if (runes.IsNullOrEmpty() || runes.Length > 1)
        {
            throw new TokenException("The command character must contain exactly one Unicode character.")
            {
                Token = clause.Tokens[tokenIndex]
            };
        }

        return runes[0];
    }

    /// <summary>
    /// This method is used to handle a production rules block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <returns>The list of production rule resolvers.</returns>
    private static List<ProductionRuleResolver> ParseProductionRulesClause(Clause clause)
    {
        List<ProductionRuleResolver> rules = [];

        int tokenIndex = 2;

        while (tokenIndex < clause.Tokens.Count && !BounderToken.CloseBrace.Matches(clause.Tokens[tokenIndex]))
        {
            ProductionRuleResolver resolver = new ProductionRuleResolver();

            tokenIndex = ParseProductionRuleVariable(
                resolver, clause, tokenIndex);
            tokenIndex = ParseProductionRuleProbability(resolver, clause, tokenIndex);

            resolver.ProductionResolver = new TermResolver<string>
            {
                Term = (Term) clause.Expressions.RemoveFirst()
            };

            rules.Add(resolver);

            tokenIndex++;
        }

        return rules;
    }

    /// <summary>
    /// This method is used to parse the variable resolver for a production rule.
    /// </summary>
    /// <param name="resolver">The resolver to add the variable resolver to.</param>
    /// <param name="clause">The clause to pull the variable from.</param>
    /// <param name="tokenIndex">The index at which to expect the variable.</param>
    /// <returns>The token index following the variable.</returns>
    private static int ParseProductionRuleVariable(
        ProductionRuleResolver resolver, Clause clause, int tokenIndex)
    {
        string variable = clause.Tokens[tokenIndex].Text;
        Rune[] runes = variable.AsRunes();

        if (runes.IsNullOrEmpty() || runes.Length > 1)
        {
            throw new TokenException("The variable must contain exactly one Unicode character.")
            {
                Token = clause.Tokens[tokenIndex]
            };
        }

        resolver.VariableResolver = new LiteralResolver<string> { Value = variable };

        return tokenIndex + 1;
    }

    /// <summary>
    /// This method is used to parse the probability clause for a production rule.
    /// </summary>
    /// <param name="resolver">The resolver to add the probability resolver to.</param>
    /// <param name="clause">The clause to pull the probability from.</param>
    /// <param name="tokenIndex">The index at which to expect the probability.</param>
    /// <returns>The token index following the probability clause.</returns>
    private static int ParseProductionRuleProbability(
        ProductionRuleResolver resolver, Clause clause, int tokenIndex)
    {
        if (tokenIndex >= clause.Tokens.Count || !BounderToken.LeftParen.Matches(clause.Tokens[tokenIndex]))
            return tokenIndex;

        Term term = (Term) clause.Expressions.RemoveFirst();
        bool isPercent = clause.Tokens[++tokenIndex] == OperatorToken.Modulo;

        if (isPercent)
        {
            resolver.BreakValueResolver = new PercentResolver { Term = term };

            tokenIndex++;
        }
        else
            resolver.BreakValueResolver = new TermResolver<double> { Term = term };

        return tokenIndex + 1;
    }
}
