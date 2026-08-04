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
    /// This method is used to handle the beginning of an isosurface block.
    /// </summary>
    private void HandleStartIsosurfaceClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Isosurface");

        IsosurfaceResolver resolver = ParseIsosurfaceClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from an isosurface block.
    /// </summary>
    private IsosurfaceResolver ParseIsosurfaceClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<IsosurfaceResolver>(
                "isosurfaceEntryClause", HandleIsosurfaceEntryClause),
            "isosurfaceEntryClause", HandleIsosurfaceEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of an isosurface block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleIsosurfaceEntryClause(Clause clause)
    {
        IsosurfaceResolver resolver = (IsosurfaceResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "function":
                    resolver.FunctionResolver = new FieldExpressionResolver { Term = clause.Term() };
                    break;
                case "threshold":
                    resolver.ThresholdResolver = new TermResolver<double> { Term = clause.Term() };
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
                    HandleSurfaceClause(clause, resolver, "isosurface");
                    break;
            }
        });
    }
}
