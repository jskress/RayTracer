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
    private void HandleStartTriangleClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Triangle");

        TriangleResolver resolver = ParseTriangleClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a triangle block.
    /// </summary>
    private TriangleResolver ParseTriangleClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<TriangleResolver>(
                "triangleEntryClause", HandleTriangleEntryClause),
            "triangleEntryClause", HandleTriangleEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a triangle block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleTriangleEntryClause(Clause clause)
    {
        TriangleResolver resolver = (TriangleResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "points":
                    resolver.Point1Resolver = new TermResolver<Point> { Term = clause.Term() };
                    resolver.Point2Resolver = new TermResolver<Point> { Term = clause.Term(1) };
                    resolver.Point3Resolver = new TermResolver<Point> { Term = clause.Term(2) };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "triangle");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to handle the beginning of a smooth triangle block.
    /// </summary>
    private void HandleStartSmoothTriangleClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Smooth triangle");

        SmoothTriangleResolver resolver = ParseSmoothTriangleClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a smooth triangle block.
    /// </summary>
    private SmoothTriangleResolver ParseSmoothTriangleClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<SmoothTriangleResolver>(
                "smoothTriangleEntryClause", HandleSmoothTriangleEntryClause),
            "smoothTriangleEntryClause", HandleSmoothTriangleEntryClause, tokenOffset: 1);
    }

    /// <summary>
    /// This method is used to handle an item clause of a smooth triangle block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSmoothTriangleEntryClause(Clause clause)
    {
        SmoothTriangleResolver resolver = (SmoothTriangleResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "points":
                    resolver.Point1Resolver = new TermResolver<Point> { Term = clause.Term() };
                    resolver.Point2Resolver = new TermResolver<Point> { Term = clause.Term(1) };
                    resolver.Point3Resolver = new TermResolver<Point> { Term = clause.Term(2) };
                    break;
                case "normals":
                    resolver.Normal1Resolver = new TermResolver<Vector> { Term = clause.Term() };
                    resolver.Normal2Resolver = new TermResolver<Vector> { Term = clause.Term(1) };
                    resolver.Normal3Resolver = new TermResolver<Vector> { Term = clause.Term(2) };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "smooth triangle");
                    break;
            }
        });
    }
}
