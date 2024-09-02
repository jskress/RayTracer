using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a point light block.
    /// </summary>
    private void HandleStartPointLightClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Point light");

        PointLightResolver resolver = ParsePointLightClause();

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the resolver from a point light block.
    /// </summary>
    private PointLightResolver ParsePointLightClause()
    {
        return ParseObjectResolver<PointLightResolver>(
            "pointLightEntryClause", HandlePointLightEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a light block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandlePointLightEntryClause(Clause clause)
    {
        PointLightResolver resolver = (PointLightResolver) _context.CurrentTarget;
        string field = clause.Text();
        Term term = clause.Term();
    
        switch (field)
        {
            case "named":
                resolver.NameResolver = new TermResolver<string> { Term = term };
                break;
            case "location":
                resolver.LocationResolver = new TermResolver<Point> { Term = term };
                break;
            case "color":
                resolver.ColorResolver = new TermResolver<Color> { Term = term };
                break;
            default:
                throw new Exception($"Internal error: unknown light property found: {field}.");
        }
    }
}
