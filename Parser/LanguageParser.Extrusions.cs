using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces.Extrusions;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of an extrusion block.
    /// </summary>
    /// <param name="clause">The clause that starts the cylinder.</param>
    private void HandleStartExtrusionClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Cylinder");

        ExtrusionResolver resolver = ParseExtrusionClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from an extrusion block.
    /// </summary>
    /// <param name="clause">The clause that starts the cylinder.</param>
    private ExtrusionResolver ParseExtrusionClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<ExtrusionResolver>(
                "extrusionEntryClause", HandleExtrusionEntryClause),
            "extrusionEntryClause", HandleExtrusionEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of an extrusion block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleExtrusionEntryClause(Clause clause)
    {
        ExtrusionResolver resolver = (ExtrusionResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else if (clause.Text() == "path")
            resolver.GeneralPathResolver = ParseGeneralPathClause();
        else
            HandleExtrudedSurfaceClause(clause, resolver, "extrusion");
    }

    /// <summary>
    /// This method is used to create the instruction set from a path block.
    /// </summary>
    private GeneralPathResolver ParseGeneralPathClause()
    {
        GeneralPathResolver resolver = new GeneralPathResolver();
        
        ParseObjectResolver("extrusionPathClause", HandlePathEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of an extrusion's path block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandlePathEntryClause(Clause clause)
    {
        GeneralPathResolver resolver = (GeneralPathResolver) _context.CurrentTarget;
        PathCommandType type = GetPathCommandType(clause);
        Term[] terms = clause.Expressions.Cast<Term>().ToArray();
        PathCommand command = new PathCommand(type, terms);

        resolver.PathCommands.Add(command);
    }

    /// <summary>
    /// This method is used to get the proper path command type from the given clause.
    /// </summary>
    /// <param name="clause">The clause to determine the command type from.</param>
    /// <returns>The proper path command type.</returns>
    private static PathCommandType GetPathCommandType(Clause clause)
    {
        return clause.Text() switch
        {
            "move" => PathCommandType.MoveTo,
            "line" => PathCommandType.LineTo,
            "quad" => PathCommandType.QuadTo,
            "curve" => PathCommandType.CurveTo,
            "close" => PathCommandType.Close,
            "svg" => PathCommandType.Svg,
            _ => throw new Exception($"Unknown path command: {clause.Text()}")
        };
    }
}
