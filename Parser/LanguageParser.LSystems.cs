using System.Text;
using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
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
                case "surfaces":
                    ParseSurfaceBindingsClause(resolver, clause);
                    break;
                case "materials":
                    ParseMaterialBindingsClause(resolver, clause);
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
            case "tropism":
                resolver.TropismResolver = new TermResolver<Vector> { Term = term };
                break;
            case "susceptibility":
                resolver.SusceptibilityResolver = new TermResolver<double> { Term = term };
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
    /// This method is used to handle a surfaces block: each entry ties a character to a named
    /// surface, so that a production naming that character after a <c>~</c> stamps that surface.
    /// It cannot be static, the way the commands block's parsing is, because it has to look each
    /// named surface up among the extensible items.
    /// </summary>
    /// <param name="resolver">The resolver to add the surface bindings to.</param>
    /// <param name="clause">The clause to process.</param>
    private void ParseSurfaceBindingsClause(LSystemResolver resolver, Clause clause)
    {
        ClauseReader reader = clause.Reader();

        reader.NextToken(); // The "surfaces" keyword.
        reader.NextToken(); // The opening brace.

        while (!reader.SkipIfNextTextIs("}"))
        {
            Rune character = ParseCommandCharacter(reader.NextToken());

            reader.NextToken(); // The arrow.

            resolver.SurfaceBindings.Add(new LSystemSurfaceBinding
            {
                Character = character,
                Resolver = GetExtensibleItem<ISurfaceResolver>(reader.NextToken(), false)
            });
        }
    }

    /// <summary>
    /// This method is used to handle a materials block.  Each entry ties a named material either
    /// to a character, so that reaching that character in a production changes what the turtle
    /// draws with from there on, or to a branching depth, so that a plant may be colored from
    /// trunk to twig without a production rule for every level.
    /// </summary>
    /// <param name="resolver">The resolver to add the material bindings to.</param>
    /// <param name="clause">The clause to process.</param>
    private void ParseMaterialBindingsClause(LSystemResolver resolver, Clause clause)
    {
        ClauseReader reader = clause.Reader();

        reader.NextToken(); // The "materials" keyword.
        reader.NextToken(); // The opening brace.

        while (!reader.SkipIfNextTextIs("}"))
        {
            bool byDepth = reader.SkipIfNextTextIs("depth");
            int depth = -1;
            Rune character = default;

            if (byDepth)
            {
                Token token = reader.NextToken();

                if (!int.TryParse(token.Text, out depth) || depth < 0)
                {
                    throw new TokenException("A depth must be a whole number, zero or greater.")
                    {
                        Token = token
                    };
                }
            }
            else
                character = ParseCommandCharacter(reader.NextToken());

            reader.NextToken(); // The arrow.

            resolver.MaterialBindings.Add(new LSystemMaterialBinding
            {
                Character = character,
                Depth = depth,
                Resolver = GetExtensibleItem<MaterialResolver>(reader.NextToken(), false)
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

        // The condition comes off FIRST, and the order matters a great deal.  A condition may
        // perfectly well read "t > 5", and the context markers this method goes looking for next
        // are '<' and '>' -- so a conditional rule read the other way round would be taken to have
        // a right context of "5" and would then match nothing at all, silently.
        (string predecessor, string condition) = SplitOffCondition(keyToken.Text);

        string key = ModuleWord.StripWhitespaceBetweenModules(predecessor);
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
        else if (variable.Length == 0)
            message = "The variable is missing.";

        if (message != null)
        {
            throw new TokenException($"The production rule key is not valid. {message}")
            {
                Token = keyToken
            };
        }

        // The predecessor may now be a module rather than a bare letter, so the letter and the
        // names it binds are read out of it together.
        (Rune letter, string[] formals) = ReadPredecessor(variable, keyToken);

        // The key is what tells one rule from another, and it has to carry the condition.  Two
        // rules for the same predecessor differing only in their condition -- which is how every
        // grow-or-stop model in the book is written -- would otherwise share a key, and rules
        // sharing a key are stochastic alternatives of one rule rather than two rules.  The pair
        // would then be laid out across the probability interval and one of them chosen at random.
        string ruleKey = condition is null
            ? key
            : $"{key}:{condition.RemoveAllWhitespace()}";

        resolver.KeyResolver = new LiteralResolver<string> { Value = ruleKey };
        resolver.VariableResolver = new LiteralResolver<Rune> { Value = letter };
        resolver.FormalsResolver = new LiteralResolver<string[]> { Value = formals };
        resolver.ConditionResolver = new LiteralResolver<string> { Value = condition };
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
    /// This method splits a production's key into the predecessor and the condition guarding it,
    /// at the first colon that is not inside a module's parentheses.
    /// </summary>
    /// <param name="text">The key as written.</param>
    /// <returns>The predecessor and the condition, the latter null when there is none.</returns>
    private static (string Predecessor, string Condition) SplitOffCondition(string text)
    {
        Rune[] runes = text.AsRunes();
        Rune colon = new (':');
        Rune open = new ('(');
        Rune close = new (')');
        int depth = 0;

        for (int index = 0; index < runes.Length; index++)
        {
            if (runes[index] == open)
                depth++;
            else if (runes[index] == close)
                depth--;
            else if (runes[index] == colon && depth == 0)
            {
                string before = string.Concat(runes[..index].Select(rune => rune.ToString()));
                string after = string.Concat(runes[(index + 1)..].Select(rune => rune.ToString()));

                if (after.Trim().Length == 0)
                    return (before, null);

                return (before, after);
            }
        }

        return (text, null);
    }

    /// <summary>
    /// This method reads a production's predecessor into the letter it rewrites and the names it
    /// binds, turning anything wrong with it into an error against the rule's own token.
    /// </summary>
    private static (Rune Letter, string[] Formals) ReadPredecessor(Rune[] variable, Token keyToken)
    {
        string text = string.Concat(variable.Select(rune => rune.ToString()));

        try
        {
            return ModuleWord.ParsePredecessor(text);
        }
        catch (Exception exception)
        {
            throw new TokenException($"The production rule key is not valid. {exception.Message}")
            {
                Token = keyToken
            };
        }
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
