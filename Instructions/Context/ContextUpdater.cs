using RayTracer.General;

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
    /// This property provides the resolver, if any, for updating the gamma reporting
    /// property in the rendering context.
    /// </summary>
    public Resolver<bool> ReportGammaResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for setting the image width property
    /// in the rendering context.
    /// </summary>
    public Resolver<int> WidthResolver { get; set; }

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
        ReportGammaResolver.AssignTo(context, target => target.ReportGamma, context, variables);
        WidthResolver.AssignTo(context, target => target.Width, context, variables);
        HeightResolver.AssignTo(context, target => target.Height, context, variables);
        GammaResolver.AssignTo(context, target => target.Gamma, context, variables);

        ImageInfoUpdater?.Execute(context, variables);
    }
}
