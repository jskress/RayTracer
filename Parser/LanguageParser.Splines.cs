using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to create the instruction set from a spline block.  Unlike most
    /// of our other object types, a spline has no independent existence in the grammar --
    /// like a general path, it's always a sub-block of whatever consumes it (a future sweep
    /// surface's path through space, most likely), so there's no corresponding "start"
    /// clause; the caller is expected to have already matched the "spline" keyword and the
    /// open brace that follows it.
    /// </summary>
    private SplineResolver ParseSplineClause()
    {
        SplineResolver resolver = new SplineResolver();

        ParseObjectResolver("splineEntryClause", HandleSplineEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a spline block.  A spline must start
    /// with a "move to" command, and that command may not appear again after the first.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSplineEntryClause(Clause clause)
    {
        SplineResolver resolver = (SplineResolver) _context.CurrentTarget;
        SplineCommandType type = GetSplineCommandType(clause);

        if (resolver.SplineCommands.Count == 0 && type != SplineCommandType.MoveTo)
            throw CreateUnexpectedInputException("Expecting a spline to start with \"move to\" here.");

        if (resolver.SplineCommands.Count > 0 && type == SplineCommandType.MoveTo)
            throw CreateUnexpectedInputException("A spline's start point may only be set once.");

        Term[] terms = clause.Expressions.Cast<Term>().ToArray();

        resolver.SplineCommands.Add(new SplineCommand(type, terms));
    }

    /// <summary>
    /// This method is used to get the proper spline command type from the given clause.
    /// </summary>
    /// <param name="clause">The clause to determine the command type from.</param>
    /// <returns>The proper spline command type.</returns>
    private static SplineCommandType GetSplineCommandType(Clause clause)
    {
        return clause.Text() switch
        {
            "move" => SplineCommandType.MoveTo,
            "line" => SplineCommandType.LineTo,
            "quad" => SplineCommandType.QuadTo,
            "curve" => SplineCommandType.CurveTo,
            _ => throw new Exception($"Unknown spline command: {clause.Text()}.")
        };
    }
}
