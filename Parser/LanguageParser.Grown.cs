using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Surfaces;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a grown tree block.
    /// </summary>
    /// <param name="clause">The clause that starts the tree.</param>
    private void HandleStartGrownClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Grown tree");

        GrownTreeResolver resolver = ParseGrownClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a grown tree block.
    /// </summary>
    /// <param name="clause">The clause that starts the tree.</param>
    private GrownTreeResolver ParseGrownClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<GrownTreeResolver>(
                "grownEntryClause", HandleGrownEntryClause),
            "grownEntryClause", HandleGrownEntryClause);
    }

    /// <summary>
    /// This method reads a surface named after a word of a grown tree's: a group written out in a
    /// block, if the word is followed by an open brace, else a named surface or a call of a primitive.
    /// </summary>
    /// <param name="clause">The clause holding the word and what followed it.</param>
    /// <returns>The resolver for the surface.</returns>
    private ISurfaceResolver ParseGrownSurface(Clause clause)
    {
        if (BounderToken.OpenBrace.Matches(clause.Tokens[1]))
        {
            return ParseObjectResolver<GroupResolver>(
                "groupEntryClause", HandleGroupEntryClause);
        }

        return clause.Tokens.Count > 2
            ? ParseCall(clause)
            : GetExtensibleItem<ISurfaceResolver>(clause.Tokens[1], false);
    }

    /// <summary>
    /// This method is used to handle an item clause of a grown tree block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleGrownEntryClause(Clause clause)
    {
        GrownTreeResolver resolver = (GrownTreeResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "crown":
                    resolver.CrownResolver = ParseGrownSurface(clause);
                    break;
                case "leaf":
                    // One, or several after commas, and then whether they hang.
                    List<ISurfaceResolver> leaves = [];

                    for (Clause leaf = clause; leaf is not null; leaf = ParseClause("grownLeafChoiceClause"))
                        leaves.Add(ParseGrownSurface(leaf));

                    resolver.LeafResolvers = leaves;

                    if (ParseClause("grownHangingClause") is not null)
                        resolver.HangingResolver = new LiteralResolver<bool> { Value = true };

                    break;
                case "points":
                    resolver.PointsResolver = new TermResolver<int>
                    {
                        Term = clause.Term(),
                        Validator = value => value >= 1 ? null : "A grown tree needs at least one point to grow toward."
                    };
                    break;
                case "reach":
                    resolver.ReachResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value > 0 ? null : "A grown tree's reach must be more than nought."
                    };
                    break;
                case "kill":
                    resolver.KillResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value > 0 ? null : "A grown tree's kill distance must be more than nought."
                    };
                    break;
                case "step":
                    resolver.StepResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value > 0 ? null : "A grown tree's step must be more than nought."
                    };
                    break;
                case "tropism":
                    resolver.TropismResolver = new TermResolver<Vector> { Term = clause.Term() };
                    break;
                case "susceptibility":
                    resolver.SusceptibilityResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "twig":
                    resolver.TwigResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value > 0 ? null : "A grown tree's twigs must be thicker than nought."
                    };
                    break;
                case "joining":
                    resolver.JoiningResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = value => value > 0 ? null : "A grown tree's joining must be more than nought."
                    };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "grown tree");
                    break;
            }
        });
    }
}
