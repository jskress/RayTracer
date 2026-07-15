using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a sweep block.
    /// </summary>
    /// <param name="clause">The clause that starts the sweep.</param>
    private void HandleStartSweepClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Sweep");

        SweepResolver resolver = ParseSweepClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a sweep block.
    /// </summary>
    /// <param name="clause">The clause that starts the sweep.</param>
    private SweepResolver ParseSweepClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<SweepResolver>(
                "sweepEntryClause", HandleSweepEntryClause),
            "sweepEntryClause", HandleSweepEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a sweep block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSweepEntryClause(Clause clause)
    {
        SweepResolver resolver = (SweepResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "profile":
                    resolver.ProfileResolver = ParseGeneralPathClause();
                    break;
                case "spline":
                    resolver.SplineResolver = ParseSplineClause();
                    break;
                case "discontinuous":
                    resolver.SplineResolver = ParseSplineClause();
                    resolver.SplineResolver.DiscontinuousResolver = new LiteralResolver<bool> { Value = true };
                    break;
                case "steps":
                    resolver.StepsResolver = new TermResolver<int> { Term = clause.Term() };
                    break;
                case "open":
                    resolver.OpenResolver = new LiteralResolver<bool> { Value = true };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "sweep");
                    break;
            }
        });
    }
}
