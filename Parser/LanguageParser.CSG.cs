using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a CSG block.
    /// </summary>
    private void HandleStartCsgClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, clause.Text());

        CsgSurfaceResolver resolver = ParseCsgClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a csg block.
    /// </summary>
    /// <param name="clause">The clause that started the CSG.</param>
    private CsgSurfaceResolver ParseCsgClause(Clause clause)
    {
        string text = clause.Text();

        // Handle the case when the only option is a variable reference.
        if (text == "csg" || text == "object")
        {
            return GetSurfaceResolver<CsgSurfaceResolver>(
                clause, null, "csgEntryClause", HandleCsgEntryClause);
        }

        CsgOperation operation = text switch
        {
            "union" => CsgOperation.Union,
            "difference" => CsgOperation.Difference,
            "intersection" => CsgOperation.Intersection,
            _ => throw new Exception($"Internal error: unknown CSG type: {text}.")
        };

        return GetSurfaceResolver(
            clause, () =>
            {
                CsgSurfaceResolver resolver = new CsgSurfaceResolver { Operation = operation };

                ParseObjectResolver("csgEntryClause", HandleCsgEntryClause, resolver);

                return resolver;
            },
            "csgEntryClause", HandleCsgEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a csg block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCsgEntryClause(Clause clause)
    {
        CsgSurfaceResolver resolver = (CsgSurfaceResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
        {
            switch (clause.Tag)
            {
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
                case "triangle":
                    resolver.SurfaceResolvers.Add(ParseTriangleClause(clause));
                    break;
                case "smoothTriangle":
                    resolver.SurfaceResolvers.Add(ParseSmoothTriangleClause(clause));
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
                case "surface":
                    HandleSurfaceClause(clause, resolver, "CSG object");
                    break;
                default:
                    throw new Exception($"Internal error: unknown {clause.Tag} property found on a CSG object.");
            }
        }
    }
}
