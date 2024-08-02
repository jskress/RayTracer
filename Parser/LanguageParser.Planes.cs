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
    private void HandleStartPlaneClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Plane");

        PlaneInstructionSet instructionSet = ParsePlaneClause();

        _ = new TopLevelObjectInstruction<Plane>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a plane block.
    /// </summary>
    private PlaneInstructionSet ParsePlaneClause()
    {
        PlaneInstructionSet instructionSet = new ();

        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandlePlaneEntryClause);

        _context.PopInstructionSet();

        return instructionSet;
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
