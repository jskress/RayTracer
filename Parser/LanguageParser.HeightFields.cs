using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a height field block.
    /// </summary>
    private void HandleStartHeightFieldClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Height field");

        HeightFieldResolver resolver = ParseHeightFieldClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a height field block.
    /// </summary>
    private HeightFieldResolver ParseHeightFieldClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<HeightFieldResolver>(
                "heightFieldEntryClause", HandleHeightFieldEntryClause),
            "heightFieldEntryClause", HandleHeightFieldEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a height field block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleHeightFieldEntryClause(Clause clause)
    {
        HeightFieldResolver resolver = (HeightFieldResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "uncached":
                case "image":
                    resolver.ImageReferenceResolver = ParseImageReference(clause);
                    break;
                case "clip":
                    resolver.ClipResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "open":
                    resolver.ClosedResolver = new LiteralResolver<bool> { Value = false };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "height field");
                    break;
            }
        });
    }
}
