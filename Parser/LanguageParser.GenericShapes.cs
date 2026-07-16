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
    /// This method is used to handle the beginning of a generic shape block.
    /// </summary>
    private void HandleStartGenericShapeClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Generic shape");

        GenericShapeResolver resolver = ParseGenericShapeClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a generic shape block.
    /// </summary>
    private GenericShapeResolver ParseGenericShapeClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<GenericShapeResolver>(
                "genericShapeEntryClause", HandleGenericShapeEntryClause),
            "genericShapeEntryClause", HandleGenericShapeEntryClause, tokenOffset: 1);
    }

    /// <summary>
    /// This method is used to handle an item clause of a generic shape block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleGenericShapeEntryClause(Clause clause)
    {
        GenericShapeResolver resolver = (GenericShapeResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            if (clause.Text() == "path")
                resolver.PathResolver = ParseGeneralPathClause();
            else
                HandleSurfaceClause(clause, resolver, "generic shape");
        });
    }
}
