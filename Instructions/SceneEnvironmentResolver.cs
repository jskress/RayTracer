using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to resolve what is true of the space between a scene's objects.
/// </summary>
public class SceneEnvironmentResolver : ObjectResolver<SceneEnvironment>
{
    /// <summary>
    /// This property holds the resolver for the index of refraction of that space.
    /// </summary>
    public Resolver<double> IndexOfRefractionResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an environment.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(
        RenderContext context, Variables variables, SceneEnvironment value)
    {
        IndexOfRefractionResolver.AssignTo(value, target => target.IndexOfRefraction, context, variables);
    }
}
