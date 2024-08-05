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
    /// This method is used to handle the beginning of a plane block.
    /// </summary>
    /// <param name="clause">The clause that starts the plane.</param>
    private void HandleStartPlaneClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Plane");

        PlaneInstructionSet instructionSet = ParsePlaneClause(clause);

        _ = new TopLevelObjectInstruction<Plane>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a plane block.
    /// </summary>
    /// <param name="clause">The clause that starts the plane.</param>
    private PlaneInstructionSet ParsePlaneClause(Clause clause)
    {
        return DetermineProperInstructionSet(
            clause, () => new PlaneInstructionSet(), 
            ParsePlaneClause);
    }

    /// <summary>
    /// This method is used to create the instruction set from a plane block.
    /// </summary>
    /// <param name="instructionSet">The instruction set to parse out.</param>
    private void ParsePlaneClause(PlaneInstructionSet instructionSet)
    {
        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandlePlaneEntryClause);

        _context.PopInstructionSet();
    }

    /// <summary>
    /// This method is used to handle an item clause of a plane block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandlePlaneEntryClause(Clause clause)
    {
        PlaneInstructionSet instructionSet = (PlaneInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "plane");
    }
}
