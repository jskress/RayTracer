using Lex.Clauses;
using RayTracer.Basics;
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
    /// This method is used to handle the beginning of a blob block.
    /// </summary>
    /// <param name="clause">The clause that starts the blob.</param>
    private void HandleStartBlobClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Blob");

        BlobResolver resolver = ParseBlobClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a blob block.
    /// </summary>
    /// <param name="clause">The clause that starts the blob.</param>
    private BlobResolver ParseBlobClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<BlobResolver>(
                "blobEntryClause", HandleBlobEntryClause),
            "blobEntryClause", HandleBlobEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a blob block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleBlobEntryClause(Clause clause)
    {
        BlobResolver resolver = (BlobResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "threshold":
                    resolver.ThresholdResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "sphere":
                    resolver.ComponentResolvers.Add(ParseBlobSphereComponentClause());
                    break;
                case "cylinder":
                    resolver.ComponentResolvers.Add(ParseBlobCylinderComponentClause());
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "blob");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a blob sphere component
    /// block.
    /// </summary>
    private BlobSphereComponentResolver ParseBlobSphereComponentClause()
    {
        BlobSphereComponentResolver resolver = new ();

        ParseObjectResolver("blobSphereEntryClause", HandleBlobSphereEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a blob sphere component block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleBlobSphereEntryClause(Clause clause)
    {
        BlobSphereComponentResolver resolver = (BlobSphereComponentResolver) _context.CurrentTarget;

        switch (clause.Text())
        {
            case "center":
                resolver.CenterResolver = new TermResolver<Point> { Term = clause.Term() };
                break;
            case "radius":
                resolver.RadiusResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            case "strength":
                resolver.StrengthResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            default:
                throw new Exception($"Internal error: unknown blob sphere component property found: {clause.Text()}.");
        }
    }

    /// <summary>
    /// This method is used to create the instruction set from a blob cylinder component
    /// block.
    /// </summary>
    private BlobCylinderComponentResolver ParseBlobCylinderComponentClause()
    {
        BlobCylinderComponentResolver resolver = new ();

        ParseObjectResolver("blobCylinderEntryClause", HandleBlobCylinderEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a blob cylinder component block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleBlobCylinderEntryClause(Clause clause)
    {
        BlobCylinderComponentResolver resolver = (BlobCylinderComponentResolver) _context.CurrentTarget;

        switch (clause.Text())
        {
            case "from":
                resolver.StartResolver = new TermResolver<Point> { Term = clause.Term() };
                break;
            case "to":
                resolver.EndResolver = new TermResolver<Point> { Term = clause.Term() };
                break;
            case "radius":
                resolver.RadiusResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            case "strength":
                resolver.StrengthResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            default:
                throw new Exception($"Internal error: unknown blob cylinder component property found: {clause.Text()}.");
        }
    }
}
