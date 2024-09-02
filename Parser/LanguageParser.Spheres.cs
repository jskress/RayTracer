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
    /// This method is used to handle the beginning of a sphere block.
    /// </summary>
    private void HandleStartSphereClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Sphere");

        SphereResolver resolver = ParseSphereClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a sphere block.
    /// </summary>
    /// <param name="clause">The clause that starts the sphere.</param>
    private SphereResolver ParseSphereClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<SphereResolver>(
                "surfaceEntryClause", HandleSphereEntryClause),
            "surfaceEntryClause", HandleSphereEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a sphere block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSphereEntryClause(Clause clause)
    {
        SphereResolver resolver = (SphereResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
            HandleSurfaceClause(clause, resolver, "sphere");
    }
}
