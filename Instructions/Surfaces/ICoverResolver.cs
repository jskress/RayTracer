using RayTracer.Basics;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This interface is what the resolver of a thing that fills itself over the whole of another surface holds of where
/// on it it may stand -- a field covering a surface all over, a grown tree climbing one -- so that both read the same
/// words the same way.  See <see cref="Geometry.ICoversASurface"/>.
/// </summary>
public interface ICoverResolver
{
    /// <summary>
    /// This property holds the resolver for the name of the surface covered.
    /// </summary>
    Resolver<string> AllOverResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the pattern saying where on the surface a place is kept.
    /// </summary>
    DensityShapeResolver PatchesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the patterned value below which a place is left.
    /// </summary>
    Resolver<double> PatchesFromResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the patterned value at and above which a place is always kept.
    /// </summary>
    Resolver<double> PatchesToResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the way the surface must face at a place for it to be kept.
    /// </summary>
    Resolver<Vector> FacingResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how strongly places favor that facing.
    /// </summary>
    Resolver<double> FacingPowerResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the lowest a place may be.
    /// </summary>
    Resolver<double> AboveResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the highest a place may be.
    /// </summary>
    Resolver<double> BelowResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the steepest a place may be.
    /// </summary>
    Resolver<double> SteepestResolver { get; set; }
}
