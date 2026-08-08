using Lex.Tokens;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class resolves one call of an interior a scene wrote for itself, and is the material call's
/// twin in every respect but what it makes.
/// </summary>
public class InteriorCallResolver : InteriorResolver
{
    /// <summary>
    /// This property holds the name of the interior being called.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds the values the call supplies.
    /// </summary>
    public List<Term> Arguments { get; init; }

    /// <summary>
    /// This property holds the token to hang any complaint on.
    /// </summary>
    public Token ErrorToken { get; init; }

    /// <summary>
    /// This method makes the interior this call describes.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The interior this call makes.</returns>
    public override Interior Resolve(RenderContext context, Variables variables)
    {
        if (variables.GetValue(Name, typeof(UserPrimitive)) is not UserPrimitive primitive)
            throw new Exception($"Internal error: nothing named {Name} is a primitive.");

        object[] given = Arguments
            .Select(argument => argument.GetValue(variables))
            .ToArray();
        Interior made = (Interior) primitive.Body
            .ResolveToObject(context, primitive.ScopeFor(given, ErrorToken));

        ApplyTo(context, variables, made);

        return made;
    }
}
