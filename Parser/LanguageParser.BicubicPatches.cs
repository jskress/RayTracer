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
    /// This method is used to handle the beginning of a patch block.
    /// </summary>
    private void HandleStartPatchClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Patch");

        BicubicPatchResolver resolver = ParsePatchClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a patch block.
    /// </summary>
    private BicubicPatchResolver ParsePatchClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<BicubicPatchResolver>(
                "patchEntryClause", HandlePatchEntryClause),
            "patchEntryClause", HandlePatchEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a patch block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandlePatchEntryClause(Clause clause)
    {
        BicubicPatchResolver resolver = (BicubicPatchResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "points":
                    Resolver<Point>[,] points = new Resolver<Point>[4, 4];

                    for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                        points[i, j] = new TermResolver<Point> { Term = clause.Term(i * 4 + j) };

                    resolver.PointResolvers = points;
                    break;
                case "uSteps":
                    resolver.UStepsResolver = new TermResolver<int> { Term = clause.Term() };
                    break;
                case "vSteps":
                    resolver.VStepsResolver = new TermResolver<int> { Term = clause.Term() };
                    break;
                case "flatness":
                    resolver.FlatnessResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "patch");
                    break;
            }
        });
    }
}
