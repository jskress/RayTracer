using Lex.Clauses;
using Lex.Tokens;
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
    /// This method is used to parse a clause of zero or more transformations.
    /// </summary>
    private MaterialResolver ParseMaterialClause()
    {
        return ParseObjectResolver<MaterialResolver>(
            "materialEntryClause", HandleMaterialEntryClause);
    }

    /// <summary>
    /// This is a helper method for creating the right resolver, either by parsing an
    /// in-place definition or a variable reference.
    /// </summary>
    /// <param name="clause">The clause that tells us how to get the material resolver.</param>
    /// <returns>The proper resolver.</returns>
    private MaterialResolver GetMaterialResolver(Clause clause)
    {
        Token token = clause.Tokens[1];

        // Complete definition.
        if (BounderToken.OpenBrace.Matches(token))
            return ParseMaterialClause();

        if (token.Text == "inherited")
            return new MaterialResolver { SetToNull = true };

        bool extending = clause.Tokens.Count > 2;
        MaterialResolver resolver = GetExtensibleItem<MaterialResolver>(token, extending);

        if (extending)
            ParseObjectResolver("materialEntryClause", HandleMaterialEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a material block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleMaterialEntryClause(Clause clause)
    {
        MaterialResolver resolver = (MaterialResolver) _context.CurrentTarget;
        string field = clause.Text();
        Term term = clause.Term();

        switch (field)
        {
            case "pigment":
                resolver.PigmentResolver = ParsePigmentClause();
                break;
            case "ambient":
                resolver.AmbientResolver = new TermResolver<double>() { Term = term };
                break;
            case "diffuse":
                resolver.DiffuseResolver = new TermResolver<double>() { Term = term };
                break;
            case "specular":
                resolver.SpecularResolver = new TermResolver<double>() { Term = term };
                break;
            case "shininess":
                resolver.ShininessResolver = new TermResolver<double>() { Term = term };
                break;
            case "reflective":
                resolver.ReflectiveResolver = new TermResolver<double>() { Term = term };
                break;
            case "transparency":
                resolver.TransparencyResolver = new TermResolver<double>() { Term = term };
                break;
            case "index":
                resolver.IndexOfRefractionResolver = new TermResolver<double>() { Term = term };
                break;
            case "ior":
                resolver.IndexOfRefractionResolver = new TermResolver<double>() { Term = term };
                break;
            default:
                throw new Exception($"Internal error: unknown material property found: {field}.");
        }
    }
}
