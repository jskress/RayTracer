using Lex.Clauses;
using RayTracer.Basics;
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
    /// This method is used to handle the beginning of a strand block.
    /// </summary>
    /// <param name="clause">The clause that starts the strand.</param>
    private void HandleStartStrandClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Strand");

        StrandResolver resolver = ParseStrandClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a strand block.
    /// </summary>
    /// <param name="clause">The clause that starts the strand.</param>
    private StrandResolver ParseStrandClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<StrandResolver>(
                "strandEntryClause", HandleStrandEntryClause),
            "strandEntryClause", HandleStrandEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a strand block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleStrandEntryClause(Clause clause)
    {
        StrandResolver resolver = (StrandResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (ToCmd(clause))
            {
                case "width":
                    resolver.PointResolvers.Add((
                        new TermResolver<double> { Term = clause.Term() },
                        new TermResolver<Point> { Term = clause.Term(1) }));
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "strand");
                    break;
            }
        });
    }
}
