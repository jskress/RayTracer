using RayTracer.Core;
using RayTracer.General;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Pigments;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a scene value.
/// </summary>
public class SceneResolver : NamedObjectResolver<Scene>
{
    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of cameras
    /// for our scene.
    /// </summary>
    public List<CameraResolver> CameraResolvers { get; } = [];

    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of point
    /// lights for our scene.
    /// </summary>
    public List<PointLightResolver> PointLightResolvers { get; } = [];

    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of surfaces
    /// for our scene.
    /// </summary>
    public List<ISurfaceResolver> SurfaceResolvers { get; } = [];

    /// <summary>
    /// This property holds the pigment resolver that will evaluate to the background
    /// for our scene.
    /// </summary>
    public IPigmentResolver BackgroundResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a CSG
    /// surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Scene value)
    {
        value.Cameras.AddRange(CameraResolvers
            .Select(resolver => resolver.Resolve(context, variables)));
        value.Lights.AddRange(PointLightResolvers
            .Select(resolver => resolver.Resolve(context, variables)));
        value.Surfaces.AddRange(SurfaceResolvers
            .Select(resolver => resolver.ResolveToSurface(context, variables)));

        if (BackgroundResolver != null)
            value.Background = BackgroundResolver.ResolveToPigment(context, variables);

        base.SetProperties(context, variables, value);
    }
}
