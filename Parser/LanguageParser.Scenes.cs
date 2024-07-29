using Lex.Clauses;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Instructions;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a scene block.
    /// </summary>
    private void HandleStartSceneClause()
    {
        SceneInstructionSet sceneInstructionSet = new ();
        
        _context.PushInstructionSet(sceneInstructionSet);

        ParseBlock("sceneEntryClause", HandleSceneEntryClause);

        _context.PopInstructionSet();

        _ = new TopLevelObjectInstruction<Scene>(_context.InstructionContext, sceneInstructionSet);
    }

    /// <summary>
    /// This method is used to handle a clause for a scene.
    /// </summary>
    /// <param name="clause">The clause that represents the name for the scene.</param>
    private void HandleSceneEntryClause(Clause clause)
    {
        SceneInstructionSet instructionSet = (SceneInstructionSet) _context.CurrentSet;
        string field = clause.Tokens[0].Text;
        Term term = (Term) clause.Expressions.First();
        ObjectInstruction<Scene> instruction = field switch
        {
            "named" => CreateNamedInstruction<Scene>(term),
            "camera" => new AddChildInstruction<Scene, Camera>(
                ParseCameraClause(), scene => scene.Cameras),
            "point" => new AddChildInstruction<Scene, PointLight>(
                ParsePointLightClause(), scene => scene.Lights),
            "light" => new AddChildInstruction<Scene, PointLight>(
                ParsePointLightClause(), scene => scene.Lights),
            "plane" => new AddChildInstruction<Scene, Surface, Plane>(
                ParsePlaneClause(), scene => scene.Surfaces),
            "background" => new SetChildInstruction<Scene, Pigment>(
                ParsePigmentClause(), scene => scene.Background),
            _ => throw new Exception($"Internal error: unknown scene property found: {field}.")
        };

        instructionSet.AddInstruction(instruction);
    }
}
