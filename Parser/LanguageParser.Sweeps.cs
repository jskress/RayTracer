using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Geometry;
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
                    resolver.ProfileResolver = GetPathResolver(clause);
                    break;
                case "spline" or "discontinuous":
                {
                    // A spline written out, or the name of one: a name is looked up where the sweep is made, so it
                    // may be a primitive's parameter as well as one the scene set.
                    Token last = clause.Tokens[^1];

                    if (BounderToken.OpenBrace.Matches(last))
                    {
                        resolver.SplineResolver = clause.Text() == "discontinuous"
                            ? ParseDiscontinuousSpline()
                            : ParseSplineClause();
                    }
                    else
                        resolver.SplineResolver = new TermResolver<Spline> { Term = new VariableTerm(last) };

                    break;
                }
                case "steps":
                    resolver.StepsResolver = new TermResolver<int> { Term = clause.Term() };
                    break;
                case "open":
                    resolver.OpenResolver = new LiteralResolver<bool> { Value = true };
                    break;
                case "no":
                    // The only "no" clause a sweep has is "no center", so seeing "no" is
                    // enough to know the profile should not be recentered.
                    resolver.CenterResolver = new LiteralResolver<bool> { Value = false };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "sweep");
                    break;
            }
        });
    }
}
