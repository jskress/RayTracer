using Lex.Clauses;
using RayTracer.Geometry;
using RayTracer.Instructions;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a cube block.
    /// </summary>
    private void HandleStartCubeClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Cube");

        CubeInstructionSet instructionSet = ParseCubeClause(clause);

        _ = new TopLevelObjectInstruction<Cube>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a cube block.
    /// </summary>
    /// <param name="clause">The clause that starts the cube.</param>
    private CubeInstructionSet ParseCubeClause(Clause clause)
    {
        return DetermineProperInstructionSet(
            clause, () => new CubeInstructionSet(), 
            ParseCubeClause);
    }

    /// <summary>
    /// This method is used to create the instruction set from a cube block.
    /// </summary>
    private void ParseCubeClause(CubeInstructionSet instructionSet)
    {
        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandleCubeEntryClause);

        _context.PopInstructionSet();
    }

    /// <summary>
    /// This method is used to handle an item clause of a cube block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCubeEntryClause(Clause clause)
    {
        CubeInstructionSet instructionSet = (CubeInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "cube");
    }
}
