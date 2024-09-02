using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Terms;

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

        CameraResolver resolver = ParseCameraClause();

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the resolver from a camera block.
    /// </summary>
    private CameraResolver ParseCameraClause()
    {
        return ParseObjectResolver<CameraResolver>(
            "cameraEntryClause", HandleCameraEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a camera block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCameraEntryClause(Clause clause)
    {
        CameraResolver resolver = (CameraResolver) _context.CurrentTarget;
        string field = clause.Text();
        Term term = clause.Term();

        switch (field)
        {
            case "named":
                resolver.NameResolver = new TermResolver<string> { Term = term };
                break;
            case "location":
                resolver.LocationResolver = new TermResolver<Point> { Term = term };
                break;
            case "look":
                resolver.LookAtResolver = new TermResolver<Point> { Term = term };
                break;
            case "up":
                resolver.UpResolver = new TermResolver<Vector> { Term = term };
                break;
            case "field":
                resolver.FieldOfViewResolver = new AngleResolver { Term = term };
                break;
            default:
                throw new Exception($"Internal error: unknown camera property found: {field}.");
        }
    }
}
