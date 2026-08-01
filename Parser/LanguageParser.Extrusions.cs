using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces.Extrusions;
using RayTracer.Instructions.Transforms;
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
    /// <param name="clause">The clause that starts the extrusion.</param>
    private void HandleStartExtrusionClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Extrusion");

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
    /// <param name="clause">The clause that starts the extrusion.</param>
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

        HandleEntryClause(resolver, clause, clause =>
        {
            if (clause.Text() == "path")
                resolver.GeneralPathResolver = ParseGeneralPathClause();
            else
                HandleExtrudedSurfaceClause(clause, resolver, "extrusion");
        });
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

        switch (clause.Text())
        {
            // A "text" block is a run of laid-out glyphs folded into the path, so it parses as
            // its own sub-block rather than as a flat command with a list of terms.
            case "text":
                resolver.PathCommands.Add(new PathCommand(ParseTextPathClause()));
                return;
            // The 2D transforms apply to the finished path rather than adding to it, so they go
            // to the path's transform list, not its command list.
            case "translate":
                AddPathTransform(resolver, TransformType.Translate, clause);
                return;
            case "scale":
                AddPathTransform(resolver, TransformType.Scale, clause);
                return;
            case "rotate":
                AddPathTransform(resolver, TransformType.Rotate, clause);
                return;
        }

        PathCommandType type = GetPathCommandType(clause);
        Term[] terms = clause.Expressions.Cast<Term>().ToArray();
        PathCommand command = new PathCommand(type, terms);

        resolver.PathCommands.Add(command);
    }

    /// <summary>
    /// This method adds one 2D transform to a path's transform list.  Translate and scale may
    /// name an X or Y axis; a rotate is always in the path's own plane (about Z), and the rest
    /// take a 2D point or a single number.
    /// </summary>
    /// <param name="resolver">The path resolver to add the transform to.</param>
    /// <param name="type">The sort of transform this is.</param>
    /// <param name="clause">The clause carrying the transform.</param>
    private static void AddPathTransform(GeneralPathResolver resolver, TransformType type, Clause clause)
    {
        TransformAxis axis = type == TransformType.Rotate
            ? TransformAxis.Z
            : clause.Text(1) switch
            {
                "X" => TransformAxis.X,
                "Y" => TransformAxis.Y,
                _ => TransformAxis.All
            };

        resolver.TransformResolver.Add(type, axis, clause.Term());
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
            "icon" => PathCommandType.Icon,
            _ => throw new Exception($"Unknown path command: {clause.Text()}")
        };
    }
}
