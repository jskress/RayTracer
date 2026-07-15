using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Extensions;
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
    /// This method is used to handle the beginning of a tube block.
    /// </summary>
    /// <param name="clause">The clause that starts the tube.</param>
    private void HandleStartTubeClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Tube");

        TubeResolver resolver = ParseTubeClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a tube block.
    /// </summary>
    /// <param name="clause">The clause that starts the tube.</param>
    private TubeResolver ParseTubeClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<TubeResolver>(
                "tubeEntryClause", HandleTubeEntryClause),
            "tubeEntryClause", HandleTubeEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a tube block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleTubeEntryClause(Clause clause)
    {
        TubeResolver resolver = (TubeResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Tag)
            {
                case "point":
                    HandleTubePointEntry(resolver, ParseTubeControlPoint(clause, 0));
                    break;
                case "quad":
                    resolver.SegmentResolvers.Add(new TubeSegmentSpecResolver
                    {
                        Control1Resolver = ParseTubeControlPoint(clause, 0),
                        EndResolver = ParseTubeControlPoint(clause, 2)
                    });
                    break;
                case "curve":
                    resolver.SegmentResolvers.Add(new TubeSegmentSpecResolver
                    {
                        Control1Resolver = ParseTubeControlPoint(clause, 0),
                        Control2Resolver = ParseTubeControlPoint(clause, 2),
                        EndResolver = ParseTubeControlPoint(clause, 4)
                    });
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "tube");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to handle a plain "radius ... at ..." tube point entry: the
    /// first one becomes the tube's start, and every one after that becomes a straight-line
    /// segment from wherever the tube currently ends.
    /// </summary>
    /// <param name="resolver">The tube resolver being built up.</param>
    /// <param name="point">The resolver for the point that was just parsed.</param>
    private static void HandleTubePointEntry(TubeResolver resolver, TubeControlPointResolver point)
    {
        if (resolver.StartResolver == null)
            resolver.StartResolver = point;
        else
            resolver.SegmentResolvers.Add(new TubeSegmentSpecResolver { EndResolver = point });
    }

    /// <summary>
    /// This method is used to build a control point resolver from a radius/location pair of
    /// terms found at the given starting term index within a clause.
    /// </summary>
    /// <param name="clause">The clause to pull the terms from.</param>
    /// <param name="termIndex">The index of the radius term; the location term is assumed
    /// to immediately follow it.</param>
    private static TubeControlPointResolver ParseTubeControlPoint(Clause clause, int termIndex)
    {
        return new TubeControlPointResolver
        {
            RadiusResolver = new TermResolver<double> { Term = clause.Term(termIndex) },
            CenterResolver = new TermResolver<Point> { Term = clause.Term(termIndex + 1) }
        };
    }
}
