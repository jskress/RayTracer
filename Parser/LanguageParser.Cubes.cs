using Lex.Clauses;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a cube block.
    /// </summary>
    private void HandleStartCubeClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Cube");

        CubeResolver resolver = ParseCubeClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a cube block.
    /// </summary>
    /// <param name="clause">The clause that starts the cube.</param>
    private CubeResolver ParseCubeClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<CubeResolver>(
                "surfaceEntryClause", HandleCubeEntryClause),
            "surfaceEntryClause", HandleCubeEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a cube block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCubeEntryClause(Clause clause)
    {
        CubeResolver resolver = (CubeResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
            HandleSurfaceClause(clause, resolver, "cube");
    }
}
