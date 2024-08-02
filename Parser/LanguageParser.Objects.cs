using Lex.Clauses;
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
    /// This method is used to handle the beginning of an object file block.
    /// </summary>
    private void HandleStartObjectFileClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Object file");

        ObjectFileInstructionSet instructionSet = ParseObjectFileClause(clause);

        _ = new TopLevelObjectInstruction<Group>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from an object file block.
    /// </summary>
    private ObjectFileInstructionSet ParseObjectFileClause(Clause clause)
    {
        Term fileName = (Term) clause.Expressions[0];

        ObjectFileInstructionSet instructionSet = new (CurrentDirectory, fileName);

        _context.PushInstructionSet(instructionSet);

        ParseBlock("surfaceEntryClause", HandleObjectFileEntryClause);

        _context.PopInstructionSet();

        instructionSet.AddInstruction(new FinalizeGroupInstruction());

        return instructionSet;
    }

    /// <summary>
    /// This method is used to handle an item clause of an object file block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleObjectFileEntryClause(Clause clause)
    {
        ObjectFileInstructionSet instructionSet = (ObjectFileInstructionSet) _context.CurrentSet;

        if (clause == null) // We must have hit a transform property...
            HandleSurfaceTransform(instructionSet);
        else
            HandleSurfaceClause(clause, instructionSet, "object file");
    }
}
