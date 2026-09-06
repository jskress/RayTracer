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
    /// This method is used to handle the beginning of a bilinear patch block.
    /// </summary>
    /// <param name="clause">The clause that starts the patch.</param>
    private void HandleStartBilinearPatchClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Patch");

        BilinearPatchResolver resolver = ParseBilinearPatchClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a bilinear patch block.
    /// </summary>
    /// <param name="clause">The clause that starts the patch.</param>
    /// <param name="tokenOffset">How many words of the block's own start are in this clause; an
    /// `object` reference carries none of them.</param>
    private BilinearPatchResolver ParseBilinearPatchClause(Clause clause, int tokenOffset = 1)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<BilinearPatchResolver>(
                "bilinearPatchEntryClause", HandleBilinearPatchEntryClause),
            "bilinearPatchEntryClause", HandleBilinearPatchEntryClause, tokenOffset);
    }

    /// <summary>
    /// This method is used to handle an item clause of a bilinear patch block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleBilinearPatchEntryClause(Clause clause)
    {
        BilinearPatchResolver resolver = (BilinearPatchResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "points":
                    resolver.CornerResolvers = Enumerable.Range(0, 4)
                        .Select(index => (Resolver<Point>) new TermResolver<Point>
                        {
                            Term = clause.Term(index)
                        })
                        .ToArray();
                    break;
                case "normals":
                    resolver.NormalResolvers = Enumerable.Range(0, 4)
                        .Select(index => (Resolver<Vector>) new TermResolver<Vector>
                        {
                            Term = clause.Term(index)
                        })
                        .ToArray();
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "patch");
                    break;
            }
        });
    }
}
