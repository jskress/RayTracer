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
    /// This method is used to handle the beginning of a ribbon block.
    /// </summary>
    /// <param name="clause">The clause that starts the ribbon.</param>
    private void HandleStartRibbonClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Ribbon");

        RibbonResolver resolver = ParseRibbonClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a ribbon block.
    /// </summary>
    /// <param name="clause">The clause that starts the ribbon.</param>
    private RibbonResolver ParseRibbonClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<RibbonResolver>(
                "ribbonEntryClause", HandleRibbonEntryClause),
            "ribbonEntryClause", HandleRibbonEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a ribbon block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleRibbonEntryClause(Clause clause)
    {
        RibbonResolver resolver = (RibbonResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (ToCmd(clause))
            {
                case "width":
                    resolver.PointResolvers.Add((
                        new TermResolver<double> { Term = clause.Term() },
                        new TermResolver<Point> { Term = clause.Term(1) }));
                    break;
                case "twist":
                    resolver.TwistResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "ribbon");
                    break;
            }
        });
    }
}
