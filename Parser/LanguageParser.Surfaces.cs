using Lex.Clauses;
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
