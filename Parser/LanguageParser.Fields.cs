using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Terms;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a field block.
    /// </summary>
    /// <param name="clause">The clause that starts the field.</param>
    private void HandleStartFieldClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Field");

        FieldResolver resolver = ParseFieldClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a field block.
    /// </summary>
    /// <param name="clause">The clause that starts the field.</param>
    private FieldResolver ParseFieldClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<FieldResolver>(
                "fieldEntryClause", HandleFieldEntryClause),
            "fieldEntryClause", HandleFieldEntryClause);
    }

    /// <summary>
    /// This method reads how often one of the things a field is filled with is picked, if the scene
    /// says: `weight` and a number greater than nought.
    /// </summary>
    /// <returns>The resolver for the weight, or <c>null</c> if none was written.</returns>
    private Resolver<double> ParseFieldWeight()
    {
        Clause clause = ParseClause("fieldWeightClause");

        return clause is null ? null : new TermResolver<double>
        {
            Term = clause.Term(),
            Validator = value => value > 0 ? null : "A weight must be greater than nought."
        };
    }

    /// <summary>
    /// This method is used to handle an item clause of a field block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleFieldEntryClause(Clause clause)
    {
        FieldResolver resolver = (FieldResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (ToCmd(clause))
            {
                case "within":
                    // Written out here, or named.  A name is read as a term rather than looked up
                    // now, because the name may be a primitive's parameter -- which has no value
                    // until the primitive is called.
                    resolver.OutlineResolver = BounderToken.OpenBrace.Matches(clause.Tokens[1])
                        ? ParseGeneralPathClause()
                        : new TermResolver<GeneralPath>
                        {
                            Term = new VariableTerm(clause.Tokens[1])
                        };
                    break;
                case "of":
                    // A named surface, or a call of a primitive, and then as many more as are written
                    // after commas, each with how often it is picked.  The call is what makes `copies`
                    // worth having: the scene hands the copy's number in as an argument, and the
                    // primitive's own arithmetic makes each of them its own.
                    List<(ISurfaceResolver, Resolver<double>)> choices = [];

                    for (Clause choice = clause; choice is not null; choice = ParseClause("fieldChoiceClause"))
                    {
                        ISurfaceResolver surface = choice.Tokens.Count > 2
                            ? ParseCall(choice)
                            : GetExtensibleItem<ISurfaceResolver>(choice.Tokens[1], false);

                        choices.Add((surface, ParseFieldWeight()));
                    }

                    resolver.ChoiceResolvers = choices;
                    break;
                case "cycle":
                    resolver.CycleResolver = new LiteralResolver<bool> { Value = true };
                    break;
                case "turned":
                    // In the scene's own unit of angle, as a rotation is: the whole spread of turns.
                    resolver.TurnResolver = new AngleResolver
                    {
                        Term = clause.Term(),
                        Validator = value => value >= 0 ? null : "A field's turn may not be less than nought."
                    };
                    break;
                case "sized":
                    resolver.SmallestSizeResolver = new TermResolver<double> { Term = clause.Term() };
                    resolver.LargestSizeResolver = new TermResolver<double>
                    {
                        Term = clause.Expressions.Count > 1 ? clause.Term(1) : clause.Term()
                    };
                    break;
                case "spacing":
                    resolver.SpacingResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "jitter":
                    resolver.JitterResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "index":
                    // The whole clause or none of it: naming the number is the only reason to write
                    // it, since a shape that cannot be shared is copied anyway -- the field asks the
                    // shape rather than the scene.  See Instance.MayBeShared.  So `in <name>` is
                    // required by the grammar, and what follows it is a copy's own number, named the
                    // way a loop names its counter.
                    resolver.CopiesResolver = new LiteralResolver<bool> { Value = true };
                    resolver.CopyNumberName = clause.Tokens[2].Text;

                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "field");
                    break;
            }
        });
    }
}
