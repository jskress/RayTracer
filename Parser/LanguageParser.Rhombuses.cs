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
    /// This method is used to handle the beginning of a triangle block.
    /// </summary>
    private void HandleStartRhombusClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Rhombus");

        RhombusResolver resolver = ParseRhombusClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a rhombus block.
    /// </summary>
    private RhombusResolver ParseRhombusClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<RhombusResolver>(
                "rhombusEntryClause", HandleRhombusEntryClause),
            "rhombusEntryClause", HandleRhombusEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a rhombus block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleRhombusEntryClause(Clause clause)
    {
        RhombusResolver resolver = (RhombusResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
        {
            switch (clause.Text())
            {
                case "at":
                    resolver.PointResolver = new TermResolver<Point> { Term = clause.Term() };
                    break;
                case "sides":
                    resolver.Side1Resolver = new TermResolver<Vector> { Term = clause.Term() };
                    resolver.Side2Resolver = new TermResolver<Vector> { Term = clause.Term(1) };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "rhombus");
                    break;
            }
        }
    }
}
