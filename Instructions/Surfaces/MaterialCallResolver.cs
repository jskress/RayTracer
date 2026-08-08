using Lex.Tokens;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class resolves one call of a material a scene wrote for itself.
/// <para>
/// It is a material resolver rather than something beside one, because a surface holds a material
/// resolver and nothing else would fit there.  What it adds is that the material is made by running a
/// recipe with values rather than by reading a block -- and then whatever the call itself said is laid
/// over the result, in the caller's own names, exactly as a surface call works.
/// </para>
/// </summary>
public class MaterialCallResolver : MaterialResolver
{
    /// <summary>
    /// This property holds the name of the material being called.
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
    /// This method makes the material this call describes.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The material this call makes.</returns>
    public override Material Resolve(RenderContext context, Variables variables)
    {
        if (variables.GetValue(Name, typeof(UserPrimitive)) is not UserPrimitive primitive)
            throw new Exception($"Internal error: nothing named {Name} is a primitive.");

        object[] given = Arguments
            .Select(argument => argument.GetValue(variables))
            .ToArray();
        Material made = (Material) primitive.Body
            .ResolveToObject(context, primitive.ScopeFor(given, ErrorToken));

        // And now whatever the call itself said, among the names the call was written in.
        ApplyTo(context, variables, made);

        return made;
    }
}
