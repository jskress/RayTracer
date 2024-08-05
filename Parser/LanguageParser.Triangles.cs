using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Instructions;
using RayTracer.Terms;

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

        TriangleInstructionSet instructionSet = ParseTriangleClause(clause);

        _ = new TopLevelObjectInstruction<Triangle>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a triangle block.
    /// </summary>
    private TriangleInstructionSet ParseTriangleClause(Clause clause)
    {
        if (BounderToken.LeftParen.Matches(clause.Tokens[1]))
        {
            Term first = (Term) clause.Expressions[0];
            Term second = (Term) clause.Expressions[1];
            Term third = (Term) clause.Expressions[2];
            TriangleInstructionSet instructionSet = new (first, second, third);

            ParseTriangleClause(instructionSet);

            return instructionSet;
        }

        return DetermineProperInstructionSet<TriangleInstructionSet>(
            clause, null,
            ParseTriangleClause);
    }

    /// <summary>
    /// This method is used to create the instruction set from a triangle block.
    /// </summary>
    private void ParseTriangleClause(TriangleInstructionSet instructionSet)
    {
        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandleTriangleEntryClause);

        _context.PopInstructionSet();
    }

    /// <summary>
    /// This method is used to handle an item clause of a triangle block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleTriangleEntryClause(Clause clause)
    {
        TriangleInstructionSet instructionSet = (TriangleInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "triangle");
    }

    /// <summary>
    /// This method is used to handle the beginning of a smooth triangle block.
    /// </summary>
    private void HandleStartSmoothTriangleClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Smooth triangle");

        SmoothTriangleInstructionSet instructionSet = ParseSmoothTriangleClause(clause);

        _ = new TopLevelObjectInstruction<SmoothTriangle>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a smooth triangle block.
    /// </summary>
    private SmoothTriangleInstructionSet ParseSmoothTriangleClause(Clause clause)
    {
        if (BounderToken.LeftParen.Matches(clause.Tokens[2]))
        {
            List<Term> terms = clause.Expressions
                .Cast<Term>()
                .ToList();
            SmoothTriangleInstructionSet instructionSet = new(
                terms[0], terms[1], terms[2],
                terms[3], terms[4], terms[5]);

            ParseSmoothTriangleClause(instructionSet);

            return instructionSet;
        }

        clause.Tokens.RemoveFirst();

        return DetermineProperInstructionSet<SmoothTriangleInstructionSet>(
            clause, null,
            ParseSmoothTriangleClause);
    }

    /// <summary>
    /// This method is used to create the instruction set from a smooth triangle block.
    /// </summary>
    private void ParseSmoothTriangleClause(SmoothTriangleInstructionSet instructionSet)
    {
        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandleSmoothTriangleEntryClause);

        _context.PopInstructionSet();
    }

    /// <summary>
    /// This method is used to handle an item clause of a smooth triangle block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSmoothTriangleEntryClause(Clause clause)
    {
        SmoothTriangleInstructionSet instructionSet = (SmoothTriangleInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "smooth triangle");
    }
}
