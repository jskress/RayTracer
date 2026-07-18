using System.Text;
using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Geometry.LSystems;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Surfaces;
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

        HandleEntryClause(resolver, clause, clause =>
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
                case "ignore":
                    ParseLSystemIgnoreClause(clause, resolver);
                    break;
                case "controls":
                    ParseLSystemRenderingControlsClause(resolver);
                    break;
                case "commands":
                    ParseCommandMappingsClause(resolver, clause);
                    break;
                case "productions":
                    resolver.ProductionRuleResolvers = ParseProductionRulesClause(clause);
                    break;
                case "leaf":
                    resolver.LeafSurfaceResolver =
                        GetExtensibleItem<ISurfaceResolver>(clause.Tokens[1], false);
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "l-system");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to parse an "ignore" clause for an L-system.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <param name="resolver">The L-system resoler to update.</param>
    private void ParseLSystemIgnoreClause(Clause clause, LSystemResolver resolver)
    {
        string first = clause.Text(1);
        string extras = null;

        if (first == "commands")
        {
            resolver.IgnoreOrientationCommandsResolver = new LiteralResolver<bool> { Value = true };
            
            if (clause.Tokens.Count > 3)
                extras = clause.Text(3);
        }
        else
            extras = first;

        if (extras != null)
        {
            extras = extras.RemoveAllWhitespace();

            if (extras.Length == 0)
            {
                throw new TokenException("No extra symbols provided to ignore.")
                {
                    Token = clause.Tokens.Last()
                };
            }
            
            resolver.SymbolsToIgnoreResolver =
                new LiteralResolver<Rune[]> { Value = extras.AsRunes() };
        }
    }

    /// <summary>
    /// This method is used to create an L-system rendering controls resolver from an
    /// L-system rendering controls resolver block.
    /// </summary>
    private void ParseLSystemRenderingControlsClause(LSystemResolver resolver)
    {
        resolver.RenderingControlsResolver ??= new LSystemRenderingControlsResolver();

        _ = ParseObjectResolver(
            // ReSharper disable once StringLiteralTypo
            "lsystemRenderingControlsEntryClause", HandleLSystemRenderingControlsEntryClause,
            resolver.RenderingControlsResolver);
    }

    /// <summary>
    /// This method is used to handle an item clause of an L-system rendering controls block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleLSystemRenderingControlsEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid rendering controls property here.");

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
            case "tubes":
                resolver.RenderTypeResolver = new LiteralResolver<LSystemRendererType>
                {
                    Value = LSystemRendererType.Tubes
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
        ClauseReader reader = clause.Reader();

        reader.NextToken(); // The "commands" keyword.
        reader.NextToken(); // The opening brace.

        while (!reader.SkipIfNextTextIs("}"))
        {
            Rune commandCharacter = ParseCommandCharacter(reader.NextToken());

            reader.NextToken(); // The arrow.

            TurtleCommand command = Enum.Parse<TurtleCommand>(reader.NextText(), true);

            resolver.CommandMappings.Add(new LSystemRenderCommandMapping
            {
                CommandCharacter = commandCharacter,
                TurtleCommand = command
            });
        }
    }

    /// <summary>
    /// This method is used to parse the command character for a render command mapping.
    /// </summary>
    /// <param name="token">The token to pull the command character from.</param>
    /// <returns>The command character found in the token.</returns>
    private static Rune ParseCommandCharacter(Token token)
    {
        Rune[] runes = token.Text.AsRunes();

        if (runes.IsNullOrEmpty() || runes.Length > 1)
        {
            throw new TokenException("The command character must contain exactly one Unicode character.")
            {
                Token = token
            };
        }

        return runes[0];
    }

    /// <summary>
    /// This method is used to handle a production rules block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <returns>The list of production rule resolvers.</returns>
    private static List<ProductionRuleSpecResolver> ParseProductionRulesClause(Clause clause)
    {
        List<ProductionRuleSpecResolver> rules = [];
        ClauseReader reader = clause.Reader();

        reader.NextToken(); // The "productions" keyword.
        reader.NextToken(); // The opening brace.

        while (!reader.SkipIfNextTextIs("}"))
        {
            ProductionRuleSpecResolver resolver = new ProductionRuleSpecResolver();

            ParseProductionRuleSpecKeyInfo(resolver, reader);
            ParseProductionRuleProbability(resolver, reader);

            reader.NextToken(); // The arrow.

            resolver.ProductionResolver = new TermResolver<string>
            {
                Term = (Term) reader.NextExpression()
            };

            rules.Add(resolver);
        }

        return rules;
    }

    /// <summary>
    /// This method is used to parse the key information resolvers for a production rule
    /// specification.
    /// </summary>
    /// <param name="resolver">The resolver to add the variable resolver to.</param>
    /// <param name="reader">The reader to pull the variable from.</param>
    private static void ParseProductionRuleSpecKeyInfo(
        ProductionRuleSpecResolver resolver, ClauseReader reader)
    {
        Token keyToken = reader.NextToken();
        string key = keyToken.Text.RemoveAllWhitespace();
        Rune[] runes = key.AsRunes();
        int leftIndex = Array.IndexOf(runes, new Rune('<'));
        int rightIndex = Array.IndexOf(runes, new Rune('>'));
        int vStart = leftIndex < 0 ? 0 : leftIndex + 1;
        int vEnd = rightIndex < 0 ? runes.Length : rightIndex;
        Rune[] left = leftIndex < 0 ? null : runes[..leftIndex];
        Rune[] right = rightIndex < 0 ? null : runes[(rightIndex + 1)..];
        Rune[] variable = vEnd <= vStart ? [] : runes[vStart..vEnd];
        string message = null;

        if (left is { Length: 0 })
            message = "Left context indicated but not provided.";
        else if (right is { Length: 0 })
            message = "Right context indicated but not provided.";
        else if (variable.Length != 1)
            message = "The variable must contain exactly one Unicode character.";

        if (message != null)
        {
            throw new TokenException($"The production rule key is not valid. {message}")
            {
                Token = keyToken
            };
        }

        resolver.KeyResolver = new LiteralResolver<string> { Value = key };
        resolver.VariableResolver = new LiteralResolver<Rune> { Value = variable[0] };
        resolver.LeftContextResolver = new LiteralResolver<ProductionBranch>
        {
            Value = left == null ? null : ProductionBranch.Parse(left)
        };
        resolver.RightContextResolver = new LiteralResolver<ProductionBranch>
        {
            Value = right == null ? null : ProductionBranch.Parse(right)
        };
    }

    /// <summary>
    /// This method is used to parse the probability clause for a production rule.
    /// </summary>
    /// <param name="resolver">The resolver to add the probability resolver to.</param>
    /// <param name="reader">The reader to pull the probability from.</param>
    private static void ParseProductionRuleProbability(
        ProductionRuleSpecResolver resolver, ClauseReader reader)
    {
        if (!BounderToken.LeftParen.Matches(reader.PeekToken()))
            return;

        reader.NextToken(); // The opening parenthesis.

        Term term = (Term) reader.NextExpression();
        bool isPercent = OperatorToken.Modulo.Matches(reader.PeekToken());

        if (isPercent)
        {
            resolver.BreakValueResolver = new PercentResolver { Term = term };

            reader.NextToken(); // The percent sign.
        }
        else
            resolver.BreakValueResolver = new TermResolver<double> { Term = term };

        reader.NextToken(); // The closing parenthesis.
    }
}
