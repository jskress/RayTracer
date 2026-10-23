using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions.Context;

/// <summary>
/// This class contains the resolvers that will be used to assign values to the current
/// rendering context.
/// </summary>
public class ContextUpdater : Instruction
{
    /// <summary>
    /// This property provides the resolver, if any, for updating the scanner in the
    /// rendering context.
    /// </summary>
    public ScannerResolver ScannerResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how long the scene runs as an animation, in seconds or, if
    /// <see cref="AnimationInFrames"/>, in frames.
    /// </summary>
    public Resolver<double> AnimationRunResolver { get; set; }

    /// <summary>
    /// This property notes whether the animation's run was said in frames rather than seconds.
    /// </summary>
    public bool AnimationInFrames { get; set; }

    /// <summary>
    /// This property holds the resolver for the rate the animation runs at, in frames a second.
    /// </summary>
    public Resolver<double> AnimationRateResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how gravity pulls the scene's bodies and swarms.
    /// </summary>
    public Resolver<Vector> GravityResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how the air moves.
    /// </summary>
    public Resolver<Vector> WindResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the frame that stands for the animation as a still.
    /// </summary>
    public Resolver<double> AnimationStillResolver { get; set; }

    /// <summary>
    /// This method returns the animation this block asks for, or null if it asks for none.  It is worked out
    /// before anything else in the scene, since the frames' times must be known before the scene can be
    /// carried out at any of them.
    /// </summary>
    /// <param name="context">A render context to work it out in.</param>
    /// <param name="variables">The names it may be written in terms of.</param>
    public Animation AnimationOf(RenderContext context, Variables variables)
    {
        if (AnimationRunResolver is null)
            return null;

        double run = AnimationRunResolver.Resolve(context, variables);

        return new Animation
        {
            Length = AnimationInFrames ? null : run,
            Frames = AnimationInFrames ? (long) Math.Round(run) : null,
            Rate = AnimationRateResolver?.Resolve(context, variables),
            Still = AnimationStillResolver is null ? 0 : (long) Math.Round(AnimationStillResolver.Resolve(context, variables))
        };
    }

    /// <summary>
    /// This property provides the resolver, if any, for updating the "angles are radians"
    /// property in the rendering context.
    /// </summary>
    public Resolver<bool> AnglesAreRadiansResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for updating the "apply gamma" property
    /// in the rendering context.
    /// </summary>
    public Resolver<bool> ApplyGammaResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for updating the shadow suppression
    /// property in the rendering context.
    /// </summary>
    public Resolver<bool> SuppressAllShadowsResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for setting the image width property
    /// in the rendering context.
    /// </summary>
    public Resolver<int> WidthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many places a scattering medium is sampled at.
    /// </summary>
    public Resolver<int> MediumSamplesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many directions a rough surface looks in.
    /// </summary>
    public Resolver<int> ReflectionSamplesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many walks a surface whose light comes up from under it takes.
    /// </summary>
    public Resolver<int> SubsurfaceSamplesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many directions the weathering patterns look about in.
    /// </summary>
    public Resolver<int> WeatheringSamplesResolver { get; set; }

    /// <summary>
    /// What to multiply every material's ambient by once the scene is whole.
    /// </summary>
    public Resolver<double> AmbientScaleResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many turns of a path through a medium are followed.
    /// </summary>
    public Resolver<int> MediumBouncesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many times light is followed from one matte surface to
    /// the next.
    /// </summary>
    public Resolver<int> DiffuseBouncesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many directions a matte surface looks in for bounced
    /// light.
    /// </summary>
    public Resolver<int> DiffuseSamplesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far a cache of bounced light may stray, when it is asked for.
    /// </summary>
    public Resolver<double> DiffuseCacheResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many photons the scene's caustics are made of.
    /// </summary>
    public Resolver<int> CausticPhotonsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many surfaces a ray from the eye is followed through.
    /// </summary>
    public Resolver<int?> TraceDepthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how the light a render ends with is developed into a picture.
    /// </summary>
    public Resolver<FilmCurve> FilmCurveResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many bits of each color channel reach the file.
    /// </summary>
    public Resolver<int> BitsPerChannelResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the image is written without color.
    /// </summary>
    public Resolver<bool> GrayscaleResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether light is carried wavelength by wavelength.
    /// </summary>
    public Resolver<bool> SpectralResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the temperature the picture is balanced to show as white.
    /// </summary>
    public Resolver<double> WhiteBalanceResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how deeply the sampler looks into a pixel.
    /// </summary>
    public Resolver<int> AntiAliasingDepthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how different two samples must be before the sampler
    /// looks closer.
    /// </summary>
    public Resolver<double> AntiAliasingThresholdResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for setting the image height property
    /// in the rendering context.
    /// </summary>
    public Resolver<int> HeightResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for setting the gamma property in the
    /// rendering context.
    /// </summary>
    public Resolver<double> GammaResolver { get; set; }

    /// <summary>
    /// This property holds the nested image information updater.
    /// </summary>
    public ImageInfoUpdater ImageInfoUpdater { get; set; }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        ScannerResolver.AssignTo(context, target => target.Scanner, context, variables);
        AnglesAreRadiansResolver.AssignTo(context, target => target.AnglesAreRadians, context, variables);
        ApplyGammaResolver.AssignTo(context, target => target.ApplyGamma, context, variables);
        SuppressAllShadowsResolver.AssignTo(context, target => target.SuppressAllShadows, context, variables);
        WidthResolver.AssignTo(context, target => target.Width, context, variables);
        MediumSamplesResolver.AssignTo(context, target => target.MediumSamples, context, variables);
        ReflectionSamplesResolver.AssignTo(context, target => target.ReflectionSamples, context, variables);
        SubsurfaceSamplesResolver.AssignTo(context, target => target.SubsurfaceSamples, context, variables);
        WeatheringSamplesResolver.AssignTo(context, target => target.WeatheringSamples, context, variables);
        AmbientScaleResolver.AssignTo(context, target => target.AmbientScale, context, variables);
        MediumBouncesResolver.AssignTo(context, target => target.MediumBounces, context, variables);
        DiffuseBouncesResolver.AssignTo(context, target => target.DiffuseBounces, context, variables);
        DiffuseSamplesResolver.AssignTo(context, target => target.DiffuseSamples, context, variables);
        DiffuseCacheResolver.AssignTo(context, target => target.DiffuseCacheError, context, variables);
        CausticPhotonsResolver.AssignTo(context, target => target.CausticPhotons, context, variables);
        TraceDepthResolver.AssignTo(context, target => target.TraceDepth, context, variables);
        FilmCurveResolver.AssignTo(context, target => target.FilmCurve, context, variables);
        BitsPerChannelResolver.AssignTo(context, target => target.BitsPerChannel, context, variables);
        GrayscaleResolver.AssignTo(context, target => target.Grayscale, context, variables);
        SpectralResolver.AssignTo(context, target => target.Spectral, context, variables);
        GravityResolver.AssignTo(context, target => target.Gravity, context, variables);
        WindResolver.AssignTo(context, target => target.Wind, context, variables);

        if (WhiteBalanceResolver is not null)
            context.WhiteBalance = WhiteBalanceResolver.Resolve(context, variables);

        // These land on the context's own aliasing option rather than on the context, since asking
        // for either of them is what asks for the sampler at all.
        AntiAliasingDepthResolver.AssignTo(
            context.AntiAliasing, target => target.AdaptiveDepth, context, variables);
        AntiAliasingThresholdResolver.AssignTo(
            context.AntiAliasing, target => target.AdaptiveThreshold, context, variables);
        HeightResolver.AssignTo(context, target => target.Height, context, variables);
        GammaResolver.AssignTo(context, target => target.Gamma, context, variables);

        ImageInfoUpdater?.Execute(context, variables);
    }
}
