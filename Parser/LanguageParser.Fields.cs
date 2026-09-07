using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Terms;
using RayTracer.Instructions;
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
                    // A named surface, or a call of a primitive.  The call is what makes `copies`
                    // worth having: the scene hands the copy's number in as an argument, and the
                    // primitive's own arithmetic makes each of them its own.
                    resolver.PrototypeResolver = clause.Tokens.Count > 2
                        ? ParseCall(clause)
                        : GetExtensibleItem<ISurfaceResolver>(clause.Tokens[1], false);
                    break;
                case "spacing":
                    resolver.SpacingResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "jitter":
                    resolver.JitterResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "copies":
                    resolver.CopiesResolver = new LiteralResolver<bool> { Value = true };

                    // A name after it is what a copy's number is called, the way a loop names its
                    // counter.  Without one, each place still gets its own surface -- which is only
                    // worth asking for if something else about it differs.
                    if (clause.Tokens.Count > 1)
                        resolver.CopyNumberName = clause.Tokens[1].Text;

                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "field");
                    break;
            }
        });
    }
}
