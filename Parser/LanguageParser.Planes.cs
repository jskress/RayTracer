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
    /// This method is used to handle the beginning of a plane block.
    /// </summary>
    /// <param name="clause">The clause that starts the plane.</param>
    private void HandleStartPlaneClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Plane");

        PlaneResolver resolver = ParsePlaneClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a plane block.
    /// </summary>
    /// <param name="clause">The clause that starts the plane.</param>
    private PlaneResolver ParsePlaneClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<PlaneResolver>(
                "surfaceEntryClause", HandlePlaneEntryClause),
            "surfaceEntryClause", HandlePlaneEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a plane block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandlePlaneEntryClause(Clause clause)
    {
        PlaneResolver resolver = (PlaneResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
            HandleSurfaceClause(clause, resolver, "plane");
    }
}
