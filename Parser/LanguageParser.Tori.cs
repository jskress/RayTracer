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
    /// This method is used to handle the beginning of a torus block.
    /// </summary>
    private void HandleStartTorusClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Torus");

        TorusInstructionSet instructionSet = ParseTorusClause(clause);

        _ = new TopLevelObjectInstruction<Torus>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a torus block.
    /// </summary>
    private TorusInstructionSet ParseTorusClause(Clause clause)
    {
        if (BounderToken.LeftParen.Matches(clause.Tokens[1]))
        {
            Term first = clause.Term();
            Term second = clause.Term(1);
            TorusInstructionSet instructionSet = new (first, second);

            ParseTorusClause(instructionSet);

            return instructionSet;
        }

        return DetermineProperInstructionSet<TorusInstructionSet>(
            clause, null, ParseTorusClause);
    }

    /// <summary>
    /// This method is used to create the instruction set from a torus block.
    /// </summary>
    private void ParseTorusClause(TorusInstructionSet instructionSet)
    {
        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandleTorusEntryClause);

        _context.PopInstructionSet();
    }

    /// <summary>
    /// This method is used to handle an item clause of a torus block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleTorusEntryClause(Clause clause)
    {
        TorusInstructionSet instructionSet = (TorusInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "torus");
    }
}
