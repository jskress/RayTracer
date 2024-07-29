using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Instructions;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a camera block.
    /// </summary>
    private void HandleStartCameraClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Camera");

        CameraInstructionSet instructionSet = ParseCameraClause();

        _ = new TopLevelObjectInstruction<Camera>(_context.InstructionContext, instructionSet);
    }

    /// <summary>
    /// This method is used to create the instruction set from a camera block.
    /// </summary>
    private CameraInstructionSet ParseCameraClause()
    {
        CameraInstructionSet instructionSet = new ();

        _context.PushInstructionSet(instructionSet);

        ParseBlock("cameraEntryClause", HandleCameraEntryClause);

        _context.PopInstructionSet();

        return instructionSet;
    }

    /// <summary>
    /// This method is used to handle an item clause of a camera block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCameraEntryClause(Clause clause)
    {
        CameraInstructionSet instructionSet = (CameraInstructionSet) _context.CurrentSet;
        string field = clause.Tokens[0].Text;
        Term term = (Term) clause.Expressions.First();

        ObjectInstruction<Camera> instruction = field switch
        {
            "named" => CreateNamedInstruction<Camera>(term),
            "location" => new SetObjectPropertyInstruction<Camera, Point>(
                target => target.Location, term),
            "look" => new SetObjectPropertyInstruction<Camera, Point>(
                target => target.LookAt, term),
            "up" => new SetObjectPropertyInstruction<Camera, Vector>(
                target => target.Up, term),
            "field" => new SetAnglePropertyInstruction<Camera>(
                target => target.FieldOfView, term),
            _ => throw new Exception($"Internal error: unknown camera property found: {field}.")
        };

        instructionSet.AddInstruction(instruction);
    }
}
