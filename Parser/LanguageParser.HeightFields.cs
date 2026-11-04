using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
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
                case "function":
                    resolver.FunctionResolver = new FieldExpressionResolver { Term = clause.Term() };
                    break;
                case "samples":
                    resolver.SamplesResolver = new TermResolver<int>
                    {
                        Term = clause.Term(),
                        Validator = samples => samples < 2
                            ? "A height field needs at least two samples in each direction."
                            : null
                    };
                    break;
                case "clip":
                    resolver.ClipResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "open":
                    resolver.ClosedResolver = new LiteralResolver<bool> { Value = false };
                    break;
                case "smooth":
                    resolver.SmoothResolver = new LiteralResolver<bool> { Value = true };
                    break;
                case "erosion":
                    resolver.ErosionResolver = ParseObjectResolver<ErosionResolver>(
                        "erosionEntryClause", HandleErosionEntryClause);
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "height field");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to handle an item clause of an erosion block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleErosionEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid erosion property here.");

        ErosionResolver resolver = (ErosionResolver) _context.CurrentTarget;

        switch (ToCmd(clause))
        {
            case "rivers":
                resolver.RiversResolver = NotNegative("How far rivers have cut back");
                break;
            case "scree":
                resolver.ScreeResolver = NotNegative("How long crags have shed scree");
                break;
            case "rain":
                resolver.RainResolver = NotNegative("How much rain has fallen");
                break;
            case "repose":
                resolver.ReposeResolver = new AngleResolver
                {
                    Term = clause.Term(),
                    Validator = value => value is > 0 and < Math.PI / 2
                        ? null
                        : "The angle loose rock rests at must lie between level and sheer."
                };
                break;
            case "sea":
                resolver.SeaLevelResolver = new TermResolver<double?> { Term = clause.Term() };
                break;
            case "with.seed":
                resolver.SeedResolver = new TermResolver<int?> { Term = clause.Term() };
                break;
        }

        TermResolver<double> NotNegative(string what) => new ()
        {
            Term = clause.Term(),
            Validator = value => value < 0 ? $"{what} cannot be less than nothing." : null
        };
    }
}
