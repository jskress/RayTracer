using RayTracer.General;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class is used to resolve a physical sky pigment: where the sun stands, how hazy the air is,
/// and how finely the sky is worked out.
/// </summary>
public class PhysicalSkyPigmentResolver : PigmentResolver<PhysicalSkyPigment>
{
    /// <summary>
    /// This property holds the resolver for how high the sun stands above the horizon.
    /// </summary>
    public Resolver<double> SunElevationResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for which way round the sun lies.
    /// </summary>
    public Resolver<double> SunAzimuthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how hazy the air is.
    /// </summary>
    public Resolver<double> TurbidityResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far above sea level the scene stands.
    /// </summary>
    public Resolver<double> HeightResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for what the whole sky is multiplied by.
    /// </summary>
    public Resolver<double> BrightnessResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many heights in the sky are kept.
    /// </summary>
    public Resolver<int> RowsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many ways round are kept.
    /// </summary>
    public Resolver<int> ColumnsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the sky supplies its own sun.
    /// </summary>
    public Resolver<bool> MakesItsOwnLightResolver { get; set; }

    /// <summary>
    /// This method is used to execute the resolver to produce a physical sky.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The sky this resolves to.</returns>
    public override PhysicalSkyPigment Resolve(RenderContext context, Variables variables)
    {
        PhysicalSkyPigment sky = new ();

        SunElevationResolver.AssignTo(sky, target => target.SunElevation, context, variables);
        SunAzimuthResolver.AssignTo(sky, target => target.SunAzimuth, context, variables);
        TurbidityResolver.AssignTo(sky, target => target.Turbidity, context, variables);
        HeightResolver.AssignTo(sky, target => target.Height, context, variables);
        BrightnessResolver.AssignTo(sky, target => target.Brightness, context, variables);
        RowsResolver.AssignTo(sky, target => target.Rows, context, variables);
        ColumnsResolver.AssignTo(sky, target => target.Columns, context, variables);
        MakesItsOwnLightResolver.AssignTo(sky, target => target.MakesItsOwnLight, context, variables);

        SetProperties(context, variables, sky);

        return sky;
    }
}
