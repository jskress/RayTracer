using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Geometry;
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
                case "plane":
                    resolver.ComponentResolvers.Add(ParseBlobPlaneComponentClause());
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

        if (HandleBlobComponentEntryClause(resolver, clause, "sphere"))
            return;

        switch (clause.Text())
        {
            case "center":
                resolver.CenterResolver = new TermResolver<Point> { Term = clause.Term() };
                break;
            case "radius":
                resolver.RadiusResolver = new TermResolver<double> { Term = clause.Term() };
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

        if (HandleBlobComponentEntryClause(resolver, clause, "cylinder"))
            return;

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
            default:
                throw new Exception($"Internal error: unknown blob cylinder component property found: {clause.Text()}.");
        }
    }

    /// <summary>
    /// This method is used to create the instruction set from a blob plane component block.
    /// </summary>
    private BlobPlaneComponentResolver ParseBlobPlaneComponentClause()
    {
        BlobPlaneComponentResolver resolver = new ();

        ParseObjectResolver("blobPlaneEntryClause", HandleBlobPlaneEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a blob plane component block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleBlobPlaneEntryClause(Clause clause)
    {
        BlobPlaneComponentResolver resolver = (BlobPlaneComponentResolver) _context.CurrentTarget;

        if (HandleBlobComponentEntryClause(resolver, clause, "plane"))
            return;

        switch (clause.Text())
        {
            case "at":
                resolver.PointResolver = new TermResolver<Point> { Term = clause.Term() };
                break;
            case "normal":
                resolver.NormalResolver = new TermResolver<Vector> { Term = clause.Term() };
                break;
            case "radius":
                resolver.RadiusResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            default:
                throw new Exception($"Internal error: unknown blob plane component property found: {clause.Text()}.");
        }
    }

    /// <summary>
    /// This method handles the properties every blob component shares: its strength, a pigment of
    /// its own and a transform of its own.
    /// <para>
    /// The transform is the reason a null clause has to be dealt with here rather than treated as an
    /// error.  A transform clause is not one of the component's named properties, so nothing in the
    /// component's own entry clause matches it and the parser hands back nothing at all; that is the
    /// signal to try reading a transform.  Coming back empty-handed from *that* too means the input
    /// was neither, and there is a real error to raise -- without which the block-parsing loop would
    /// spin forever on a token nothing ever consumes.
    /// </para>
    /// </summary>
    /// <param name="resolver">The component resolver being built up.</param>
    /// <param name="clause">The clause to process, or <c>null</c> if nothing matched.</param>
    /// <param name="noun">A noun for the sort of component, for use in errors.</param>
    /// <returns><c>true</c>, if the clause was dealt with here.</returns>
    private bool HandleBlobComponentEntryClause<TComponent>(
        BlobComponentResolver<TComponent> resolver, Clause clause, string noun)
        where TComponent : BlobComponent, new()
    {
        if (clause is null)
        {
            Instructions.Transforms.TransformResolver transformResolver = ParseTransformClause();

            // A `{*}` (zero-or-more) transform clause "succeeds" with an empty resolver even when
            // nothing was actually consumed, so a null check alone isn't enough here.
            if (transformResolver == null || transformResolver.TransformCreators.Count == 0)
            {
                throw CreateUnexpectedInputException(
                    $"Expecting a valid {noun} component property here.");
            }

            resolver.TransformResolver = transformResolver;

            return true;
        }

        switch (clause.Text())
        {
            case "strength":
                resolver.StrengthResolver = new TermResolver<double> { Term = clause.Term() };
                return true;
            case "pigment":
                resolver.PigmentResolver = ParsePigmentClause();
                return true;
            default:
                return false;
        }
    }
}
