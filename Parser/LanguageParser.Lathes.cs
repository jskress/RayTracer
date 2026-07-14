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
    /// This method is used to handle the beginning of a lathe block.
    /// </summary>
    /// <param name="clause">The clause that starts the lathe.</param>
    private void HandleStartLatheClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Lathe");

        LatheResolver resolver = ParseLatheClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a lathe block.
    /// </summary>
    /// <param name="clause">The clause that starts the lathe.</param>
    private LatheResolver ParseLatheClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<LatheResolver>(
                "latheEntryClause", HandleLatheEntryClause),
            "latheEntryClause", HandleLatheEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a lathe block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleLatheEntryClause(Clause clause)
    {
        LatheResolver resolver = (LatheResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            if (clause.Text() == "path")
                resolver.GeneralPathResolver = ParseGeneralPathClause();
            else
                HandleSurfaceClause(clause, resolver, "lathe");
        });
    }
}
