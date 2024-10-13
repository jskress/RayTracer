using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
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
        SceneResolver resolver = ParseObjectResolver<SceneResolver>(
            "sceneEntryClause", HandleSceneEntryClause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to handle a clause for a scene.
    /// </summary>
    /// <param name="clause">The clause that represents the name for the scene.</param>
    private void HandleSceneEntryClause(Clause clause)
    {
        SceneResolver resolver = (SceneResolver) _context.CurrentTarget;
        Term term = clause.Term();

        switch (clause.Tag)
        {
            case "name":
                resolver.NameResolver = new TermResolver<string> { Term = term };
                break;
            case "camera":
                resolver.CameraResolvers.Add(ParseCameraClause());
                break;
            case "pointLight":
                resolver.PointLightResolvers.Add(ParsePointLightClause());
                break;
            case "plane":
                resolver.SurfaceResolvers.Add(ParsePlaneClause(clause));
                break;
            case "sphere":
                resolver.SurfaceResolvers.Add(ParseSphereClause(clause));
                break;
            case "cube":
                resolver.SurfaceResolvers.Add(ParseCubeClause(clause));
                break;
            case "cylinder":
                resolver.SurfaceResolvers.Add(ParseCylinderClause(clause));
                break;
            case "conic":
                resolver.SurfaceResolvers.Add(ParseConicClause(clause));
                break;
            case "torus":
                resolver.SurfaceResolvers.Add(ParseTorusClause(clause));
                break;
            case "extrusion":
                resolver.SurfaceResolvers.Add(ParseExtrusionClause(clause));
                break;
            case "text":
                resolver.SurfaceResolvers.Add(ParseTextClause(clause));
                break;
            case "triangle":
                resolver.SurfaceResolvers.Add(ParseTriangleClause(clause));
                break;
            case "smoothTriangle":
                resolver.SurfaceResolvers.Add(ParseSmoothTriangleClause(clause));
                break;
            case "parallelogram":
                resolver.SurfaceResolvers.Add(ParseParallelogramClause(clause));
                break;
            case "objectFile":
                resolver.SurfaceResolvers.Add(ParseObjectFileClause(clause));
                break;
            case "object":
                resolver.SurfaceResolvers.Add(GetSurfaceResolver(clause));
                break;
            case "csg":
                resolver.SurfaceResolvers.Add(ParseCsgClause(clause));
                break;
            case "group":
                resolver.SurfaceResolvers.Add(ParseGroupClause(clause));
                break;
            case "background":
                resolver.BackgroundResolver = ParsePigmentClause();
                break;
            default:
                throw new Exception($"Internal error: unknown {clause.Tag} property found on a scene.");
        }
    }
}
