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

        CameraInstructionSet cameraInstructionSet = new ();

        _context.PushInstructionSet(cameraInstructionSet);

        ParseBlock("cameraEntryClause");
        
        _context.PopInstructionSet();

        if (_context.CurrentSet is SceneInstructionSet sceneInstructionSet)
        {
            sceneInstructionSet.AddInstruction(new AddChildInstruction<Scene, Camera>(
                sceneInstructionSet, cameraInstructionSet,
                scene => scene.Cameras));
        }
        else
            _ = new TopLevelObjectInstruction<Camera>(_context.InstructionContext, cameraInstructionSet);
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
            "named" => new SetObjectPropertyInstruction<Camera, string>(
                target => target.Name, term),
            "location" => new SetObjectPropertyInstruction<Camera, Point>(
                target => target.Location, term),
            "look" => new SetObjectPropertyInstruction<Camera, Point>(
                target => target.LookAt, term),
            "up" => new SetObjectPropertyInstruction<Camera, Vector>(
                target => target.Up, term),
            "field" => new SetAnglePropertyInstruction<Camera>(
                target => target.FieldOfView, term),
            _ => throw new Exception($"Internal error: unknown camera prop found: {field}.")
        };

        instructionSet.AddInstruction(instruction);
    }
}
