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
    /// This method is used to handle the beginning of a parametric surface block.
    /// </summary>
    private void HandleStartParametricClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Parametric");

        ParametricResolver resolver = ParseParametricClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a parametric surface block.
    /// </summary>
    private ParametricResolver ParseParametricClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<ParametricResolver>(
                "parametricEntryClause", HandleParametricEntryClause),
            "parametricEntryClause", HandleParametricEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a parametric surface block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleParametricEntryClause(Clause clause)
    {
        ParametricResolver resolver = (ParametricResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "X":
                    resolver.XResolver = new ParametricExpressionResolver { Term = clause.Term() };
                    break;
                case "Y":
                    resolver.YResolver = new ParametricExpressionResolver { Term = clause.Term() };
                    break;
                case "Z":
                    resolver.ZResolver = new ParametricExpressionResolver { Term = clause.Term() };
                    break;
                case "u":
                    resolver.UDomainResolver = SpanFrom(clause);
                    break;
                case "v":
                    resolver.VDomainResolver = SpanFrom(clause);
                    break;
                case "accuracy":
                    resolver.AccuracyResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = accuracy => accuracy > 0
                            ? null
                            : "The accuracy must be greater than zero."
                    };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "parametric surface");
                    break;
            }
        });
    }

    /// <summary>
    /// This method returns the span a clause's two ends describe.
    /// </summary>
    /// <param name="clause">The clause holding the two ends.</param>
    /// <returns>A resolver for the span.</returns>
    private static IntervalResolver SpanFrom(Clause clause)
    {
        return new IntervalResolver { StartTerm = clause.Term(), EndTerm = clause.Term(1) };
    }
}
