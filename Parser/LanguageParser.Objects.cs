using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Instructions.Surfaces.Extrusions;
using RayTracer.Instructions.Surfaces.LSystems;

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

        ObjectFileResolver resolver = ParseObjectFileClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from an object file block.
    /// </summary>
    private ObjectFileResolver ParseObjectFileClause(Clause clause, int tokenOffset = 1)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<ObjectFileResolver>(
                "objectFileEntryClause", HandleObjectFileEntryClause),
            "objectFileEntryClause", HandleObjectFileEntryClause, tokenOffset);
    }

    /// <summary>
    /// This method is used to handle an item clause of an object file block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleObjectFileEntryClause(Clause clause)
    {
        ObjectFileResolver resolver = (ObjectFileResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "source":
                    resolver.Directory = CurrentDirectory;
                    resolver.FileNameResolver = new TermResolver<string> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "object file");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to handle the beginning of an object block.
    /// </summary>
    private void HandleStartObjectClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Object");

        ISurfaceResolver resolver = GetSurfaceResolver(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to get the appropriate resolver for an object reference.
    /// </summary>
    /// <param name="clause">The clause that starts the plane.</param>
    /// <returns>The appropriate resolver.</returns>
    private ISurfaceResolver GetSurfaceResolver(Clause clause)
    {
        // See if we have the resolver.  This will throw an exception if we don't.
        ISurfaceResolver resolver = GetExtensibleItem<ISurfaceResolver>(clause.Tokens[1], false);

        switch (resolver)
        {
            case PlaneResolver:
                return ParsePlaneClause(clause);
            case SphereResolver:
                return ParseSphereClause(clause);
            case CubeResolver:
                return ParseCubeClause(clause);
            case CylinderResolver:
                return ParseCylinderClause(clause);
            case ConicResolver:
                return ParseConicClause(clause);
            case TorusResolver:
                return ParseTorusClause(clause);
            case EggResolver:
                return ParseEggClause(clause);
            case SuperellipsoidResolver:
                return ParseSuperellipsoidClause(clause);
            case ParaboloidResolver:
                return ParseParaboloidClause(clause);
            case SignedDistanceSurfaceResolver:
                return ParseSdfClause(clause);
            case JuliaFractalResolver:
                return ParseJuliaClause(clause);
            case HyperboloidResolver:
                return ParseHyperboloidClause(clause);
            case SaddleResolver:
                return ParseSaddleClause(clause);
            case QuadricResolver:
                return ParseQuadricClause(clause);
            case BilinearPatchResolver:
                return ParseBilinearPatchClause(clause, tokenOffset: 0);
            case BicubicPatchResolver:
                return ParsePatchClause(clause);
            case LatheResolver:
                return ParseLatheClause(clause);
            case BlobResolver:
                return ParseBlobClause(clause);
            case SwellsResolver:
                return ParseSwellsClause(clause);
            case TubeResolver:
                return ParseTubeClause(clause);
            case SweepResolver:
                return ParseSweepClause(clause);
            case TaperedExtrusionResolver:
                return ParseTaperedExtrusionClause(clause, tokenOffset: 0);
            case ExtrusionResolver:
                return ParseExtrusionClause(clause);
            case TaperedTextSolidResolver:
                return ParseTaperedTextClause(clause, tokenOffset: 0);
            case TextSolidResolver:
                return ParseTextClause(clause);
            case LSystemResolver:
                return ParseLSystemClause(clause);
            case HeightFieldResolver:
                return ParseHeightFieldClause(clause);
            case ParallelogramResolver:
                return ParseParallelogramClause(clause);
            case DiscResolver:
                return ParseDiscClause(clause);
            case GenericShapeResolver:
                return ParseGenericShapeClause(clause, tokenOffset: 0);
            case TriangleResolver:
                return ParseTriangleClause(clause);
            case SmoothTriangleResolver:
                return ParseSmoothTriangleClause(clause, tokenOffset: 0);
            case ObjectFileResolver:
                return ParseObjectFileClause(clause, tokenOffset: 0);
            case IsosurfaceResolver:
                return ParseIsosurfaceClause(clause);
            case ParametricResolver:
                return ParseParametricClause(clause);
            case CsgSurfaceResolver:
                return ParseCsgClause(clause);
            case GroupResolver:
                return ParseGroupClause(clause);
            default:
                throw new Exception($"Internal error: unknown resolver type found in an object reference.");
        }
    }
}
