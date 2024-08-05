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
        string second = clause.Tokens[1].Text;
        Term term = (Term) clause.Expressions.First();

        if (field == "object" && second != "file")
        {
            ParseObjectClause(clause, sceneInstructionSet: instructionSet);
            
            return;
        }

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
                ParsePlaneClause(clause), scene => scene.Surfaces),
            "sphere" => new AddChildInstruction<Scene, Surface, Sphere>(
                ParseSphereClause(clause), scene => scene.Surfaces),
            "cube" => new AddChildInstruction<Scene, Surface, Cube>(
                ParseCubeClause(clause), scene => scene.Surfaces),
            "cylinder" => new AddChildInstruction<Scene, Surface, Cylinder>(
                ParseCylinderClause(clause), scene => scene.Surfaces),
            "conic" => new AddChildInstruction<Scene, Surface, Conic>(
                ParseConicClause(clause), scene => scene.Surfaces),
            "open" when second == "cylinder" => new AddChildInstruction<Scene, Surface, Cylinder>(
                ParseCylinderClause(clause), scene => scene.Surfaces),
            "open" when second == "conic" => new AddChildInstruction<Scene, Surface, Conic>(
                ParseConicClause(clause), scene => scene.Surfaces),
            "torus" => new AddChildInstruction<Scene, Surface, Torus>(
                ParseTorusClause(clause), scene => scene.Surfaces),
            "triangle" => new AddChildInstruction<Scene, Surface, Triangle>(
                ParseTriangleClause(clause), scene => scene.Surfaces),
            "smooth" => new AddChildInstruction<Scene, Surface, SmoothTriangle>(
                ParseSmoothTriangleClause(clause), scene => scene.Surfaces),
            "object" => new AddChildInstruction<Scene, Surface, Group>(
                ParseObjectFileClause(clause), scene => scene.Surfaces),
            "union" => new AddChildInstruction<Scene, Surface, CsgSurface>(
                ParseCsgClause(clause), scene => scene.Surfaces),
            "difference" => new AddChildInstruction<Scene, Surface, CsgSurface>(
                ParseCsgClause(clause), scene => scene.Surfaces),
            "intersection" => new AddChildInstruction<Scene, Surface, CsgSurface>(
                ParseCsgClause(clause), scene => scene.Surfaces),
            "group" => new AddChildInstruction<Scene, Surface, Group>(
                ParseGroupClause(clause), scene => scene.Surfaces),
            "background" => new SetChildInstruction<Scene, Pigment>(
                ParsePigmentClause(), scene => scene.Background),
            _ => throw new Exception($"Internal error: unknown scene property found: {field}.")
        };

        instructionSet.AddInstruction(instruction);
    }
}
