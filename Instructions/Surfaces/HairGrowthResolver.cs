using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a description of the hair a surface grows.
/// </summary>
public class HairGrowthResolver : ObjectResolver<HairGrowth>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for how many strands grow on each unit of area.
    /// </summary>
    public Resolver<double?> DensityResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for exactly how many strands grow.
    /// </summary>
    public Resolver<int?> CountResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how long a strand is.
    /// </summary>
    public Resolver<double> LengthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much strands' lengths differ.
    /// </summary>
    public Resolver<double> VaryResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how wide a strand is at its root.
    /// </summary>
    public Resolver<double?> ThicknessResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far a strand leans.
    /// </summary>
    public Resolver<double> LeanResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the direction strands lean toward.
    /// </summary>
    public Resolver<Vector> CombResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many times a strand winds about its line.
    /// </summary>
    public Resolver<double> CurlResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far from its line a curling strand winds.
    /// </summary>
    public Resolver<double?> CurlRadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far a strand wanders.
    /// </summary>
    public Resolver<double> FrizzResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how strongly strands gather into locks.
    /// </summary>
    public Resolver<double> ClumpResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many strands make a lock.
    /// </summary>
    public Resolver<int> ClumpSizeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far a strand sags.
    /// </summary>
    public Resolver<double> DroopResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the seed the hair's randomness starts from.
    /// </summary>
    public Resolver<int?> SeedResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the hair's own material, if it has one.
    /// </summary>
    public Resolver<Material> MaterialResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the description.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, HairGrowth value)
    {
        DensityResolver.AssignTo(value, target => target.Density, context, variables);
        CountResolver.AssignTo(value, target => target.Count, context, variables);
        LengthResolver.AssignTo(value, target => target.Length, context, variables);
        VaryResolver.AssignTo(value, target => target.Vary, context, variables);
        ThicknessResolver.AssignTo(value, target => target.Thickness, context, variables);
        LeanResolver.AssignTo(value, target => target.Lean, context, variables);
        CombResolver.AssignTo(value, target => target.Comb, context, variables);
        CurlResolver.AssignTo(value, target => target.Curl, context, variables);
        CurlRadiusResolver.AssignTo(value, target => target.CurlRadius, context, variables);
        FrizzResolver.AssignTo(value, target => target.Frizz, context, variables);
        ClumpResolver.AssignTo(value, target => target.Clump, context, variables);
        ClumpSizeResolver.AssignTo(value, target => target.ClumpSize, context, variables);
        DroopResolver.AssignTo(value, target => target.Droop, context, variables);
        SeedResolver.AssignTo(value, target => target.Seed, context, variables);
        MaterialResolver.AssignTo(value, target => target.Material, context, variables);
    }

    /// <summary>
    /// This method validates the state of this resolver.
    /// </summary>
    /// <returns>A message to show the user, or <c>null</c>, if all is well.</returns>
    public string Validate()
    {
        if (DensityResolver is null && CountResolver is null)
            return "Hair needs a density or a count.";

        if (DensityResolver is not null && CountResolver is not null)
            return "Hair takes a density or a count, but not both.";

        return LengthResolver is null ? "Hair needs a length." : null;
    }
}
