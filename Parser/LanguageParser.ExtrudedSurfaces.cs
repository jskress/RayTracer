using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Geometry;
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
    /// This method is used to handle the beginning of a cylinder block.
    /// </summary>
    /// <param name="clause">The clause that starts the cylinder.</param>
    private void HandleStartCylinderClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Cylinder");

        CylinderResolver resolver = ParseCylinderClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a cylinder block.
    /// </summary>
    /// <param name="clause">The clause that starts the cylinder.</param>
    private CylinderResolver ParseCylinderClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<CylinderResolver>(
                "extrudedSurfaceEntryClause", HandleCylinderEntryClause),
            "extrudedSurfaceEntryClause", HandleCylinderEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a cylinder block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCylinderEntryClause(Clause clause)
    {
        CylinderResolver resolver = (CylinderResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
            HandleExtrudedSurfaceClause(clause, resolver, "cylinder");
    }

    /// <summary>
    /// This method is used to handle the beginning of a conic block.
    /// </summary>
    /// <param name="clause">The clause that starts the conic.</param>
    private void HandleStartConicClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Conic");

        ConicResolver resolver = ParseConicClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a conic block.
    /// </summary>
    /// <param name="clause">The clause that starts the conic.</param>
    private ConicResolver ParseConicClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<ConicResolver>(
                "extrudedSurfaceEntryClause", HandleConicEntryClause),
            "extrudedSurfaceEntryClause", HandleConicEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a conic block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleConicEntryClause(Clause clause)
    {
        ConicResolver resolver = (ConicResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
            HandleExtrudedSurfaceClause(clause, resolver, "conic");
    }

    /// <summary>
    /// This method is used to handle a clause for extruded surface properties.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <param name="resolver">The resolver to use.</param>
    /// <param name="noun">A noun to use for the object type in case of errors.</param>
    private void HandleExtrudedSurfaceClause<TObject>(
        Clause clause, ExtrudedSurfaceResolver<TObject> resolver, string noun)
        where TObject : ExtrudedSurface, new()
    {
        string field = ToCmd(clause);
        Term term = clause.Term();

        switch (field)
        {
            case "min":
                resolver.MinimumYResolver = new TermResolver<double> { Term = term };
                break;
            case "max":
                resolver.MaximumYResolver = new TermResolver<double> { Term = term };
                break;
            case "open":
                resolver.ClosedResolver = new LiteralResolver<bool> { Value = false };
                break;
            default:
                HandleSurfaceClause(clause, resolver, noun);
                break;
        }
    }
}
