using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
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
    /// This is a helper method for the common case in a surface's entry-clause handler
    /// where none of that surface's own properties matched, which leaves a transform
    /// clause as the only other valid possibility.  If a transform clause doesn't match
    /// either, the input is neither a valid property nor a valid transform, so we raise a
    /// proper error here instead of silently consuming nothing — which would otherwise
    /// leave the caller's block-parsing loop spinning forever on the same unconsumed
    /// token.
    /// </summary>
    /// <param name="resolver">The resolver whose transform should be set.</param>
    private void HandleTransformOnlyEntryClause<TObject>(SurfaceResolver<TObject> resolver)
        where TObject : Surface, new()
    {
        Instructions.Transforms.TransformResolver transformResolver = ParseTransformClause();

        // A `{*}` (zero-or-more) transform clause "succeeds" with an empty resolver even
        // when nothing was actually consumed, so a null check alone isn't enough here.
        if (transformResolver == null || transformResolver.TransformCreators.Count == 0)
            throw CreateUnexpectedInputException("Expecting a valid property here.");

        resolver.TransformResolver = transformResolver;
    }

    /// <summary>
    /// This is a helper method for the common shape every surface's entry-clause handler
    /// follows: if the clause is null, it must be a transform; otherwise, hand it to the
    /// given handler for the surface's own properties.  Centralizing this means a new
    /// surface type can't forget the null check the way several already did (see
    /// <see cref="HandleTransformOnlyEntryClause{TObject}"/>).
    /// </summary>
    /// <param name="resolver">The resolver being built up.</param>
    /// <param name="clause">The clause to process, or <c>null</c> if none of the surface's
    /// own properties matched.</param>
    /// <param name="handlePropertyClause">The action that handles a non-null clause.</param>
    private void HandleEntryClause<TObject>(
        SurfaceResolver<TObject> resolver, Clause clause, Action<Clause> handlePropertyClause)
        where TObject : Surface, new()
    {
        if (clause == null) // We must have hit a transform property...
            HandleTransformOnlyEntryClause(resolver);
        else
            handlePropertyClause(clause);
    }

    /// <summary>
    /// This is a helper method for surface types that have no properties of their own
    /// beyond the generic ones every surface supports (see <see cref="HandleSurfaceClause"/>).
    /// </summary>
    /// <param name="resolver">The resolver being built up.</param>
    /// <param name="clause">The clause to process, or <c>null</c> if none of the surface's
    /// own properties matched.</param>
    /// <param name="noun">A noun to use for the object type in case of errors.</param>
    private void HandleGenericSurfaceEntryClause<TObject>(
        SurfaceResolver<TObject> resolver, Clause clause, string noun)
        where TObject : Surface, new()
    {
        HandleEntryClause(resolver, clause, clause => HandleSurfaceClause(clause, resolver, noun));
    }

    /// <summary>
    /// This method is used to handle a clause for general surface properties.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <param name="resolver">The resolver to use.</param>
    /// <param name="noun">A noun to use for the object type in case of errors.</param>
    private void HandleSurfaceClause<TObject>(
        Clause clause, SurfaceResolver<TObject> resolver, string noun)
        where TObject : Surface, new()
    {
        string field = ToCmd(clause);
        Term term = clause.Term();

        switch (field)
        {
            case "named":
                resolver.NameResolver = new TermResolver<string> { Term = term };
                break;
            case "with.seed":
                resolver.SeedResolver = new TermResolver<int?> { Term = term };
                break;
            case "material":
                resolver.MaterialResolver = GetMaterialResolver(clause);
                break;
            case "no.shadow":
                resolver.NoShadowResolver = new LiteralResolver<bool> { Value = true };
                break;
            case "bounded.by":
                resolver.BoundingBoxResolver = new BoundingBoxResolver
                {
                    FirstPointResolver = new TermResolver<Point> { Term = term },
                    SecondPointResolver = new TermResolver<Point> { Term = clause.Term(1) }
                };
                break;
            case "transform":
                resolver.TransformResolver = GetTransformResolver(clause);
                break;
            default:
                throw new Exception($"Internal error: unknown {noun} property found: {field}.");
        }
    }

    /// <summary>
    /// This is a helper method for creating the right resolver, either by parsing an
    /// in-place definition or a variable reference.
    /// </summary>
    /// <param name="clause">The clause that tells us how to get the material resolver.</param>
    /// <param name="parser">The lambda to use for parsing the full resolver spec.</param>
    /// <param name="entryBlockName">The name of the clause for parsing entries for what
    /// the resolver resolves.</param>
    /// <param name="handler">The action that will handle parsed entry clauses.</param>
    /// <returns>The proper resolver.</returns>
    private TResolver GetSurfaceResolver<TResolver>(
        Clause clause, Func<TResolver> parser, string entryBlockName, Action<Clause> handler)
        where TResolver : class, ICloneable, IObjectResolver, new()
    {
        Token token = clause.Tokens[1];

        // Complete definition.
        if (BounderToken.OpenBrace.Matches(token))
            return parser();

        bool extending = clause.Tokens.Count > 2;
        TResolver resolver = GetExtensibleItem<TResolver>(token, extending);

        if (extending)
            ParseObjectResolver(entryBlockName, handler, resolver);

        return resolver;
    }
}
