using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve a camera value.
/// </summary>
public class CameraResolver : NamedObjectResolver<Camera>
{
    /// <summary>
    /// This property holds the resolver for our camera's location property.
    /// </summary>
    public Resolver<Point> LocationResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for our camera's look at property.
    /// </summary>
    public Resolver<Point> LookAtResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for our camera's up direction property.
    /// </summary>
    public Resolver<Vector> UpResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for our camera's field of view property.
    /// </summary>
    public AngleResolver FieldOfViewResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the radius of our camera's lens.
    /// </summary>
    public Resolver<double> ApertureResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far ahead of our camera the plane of sharp focus
    /// lies, when the scene says so outright.
    /// </summary>
    public Resolver<double?> FocalDistanceResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the point our camera brings into focus, when the scene
    /// names one rather than giving a distance.
    /// </summary>
    public Resolver<Point> FocalPointResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many places across the lens each ray is taken from.
    /// </summary>
    public Resolver<int> BlurSamplesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the seed the lens's scatter is drawn from.
    /// </summary>
    public Resolver<int?> SeedResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a camera.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Camera value)
    {
        LocationResolver.AssignTo(value, target => target.Location, context, variables);
        LookAtResolver.AssignTo(value, target => target.LookAt, context, variables);
        UpResolver.AssignTo(value, target => target.Up, context, variables);
        FieldOfViewResolver.AssignTo(value, target => target.FieldOfView, context, variables);
        ApertureResolver.AssignTo(value, target => target.Aperture, context, variables);
        FocalDistanceResolver.AssignTo(value, target => target.FocalDistance, context, variables);
        FocalPointResolver.AssignTo(value, target => target.FocalPoint, context, variables);
        BlurSamplesResolver.AssignTo(value, target => target.BlurSamples, context, variables);
        SeedResolver.AssignTo(value, target => target.Seed, context, variables);

        base.SetProperties(context, variables, value);
    }
}
