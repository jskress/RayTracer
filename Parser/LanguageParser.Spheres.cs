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
    /// This method is used to handle the beginning of a sphere block.
    /// </summary>
    private void HandleStartSphereClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Sphere");

        SphereInstructionSet instructionSet = ParseSphereClause(clause);

        _ = new TopLevelObjectInstruction<Sphere>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a sphere block.
    /// </summary>
    /// <param name="clause">The clause that starts the sphere.</param>
    private SphereInstructionSet ParseSphereClause(Clause clause)
    {
        return DetermineProperInstructionSet(
            clause, () => new SphereInstructionSet(), 
            ParseSphereClause);
    }

    /// <summary>
    /// This method is used to create the instruction set from a sphere block.
    /// </summary>
    /// <param name="instructionSet">The instruction set to parse out.</param>
    private void ParseSphereClause(SphereInstructionSet instructionSet)
    {
        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandleSphereEntryClause);

        _context.PopInstructionSet();
    }

    /// <summary>
    /// This method is used to handle an item clause of a sphere block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSphereEntryClause(Clause clause)
    {
        SphereInstructionSet instructionSet = (SphereInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "sphere");
    }
}
