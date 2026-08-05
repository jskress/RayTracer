namespace RayTracer.Core;

/// <summary>
/// This enumeration notes the shapes of scattering a medium may follow: how the light it turns aside
/// is spread over the directions it might leave in.
/// </summary>
public enum PhaseFunction
{
    /// <summary>
    /// This entry notes the Henyey-Greenstein shape, the standard one-parameter family, whose
    /// parameter is the medium's anisotropy.  At an anisotropy of nothing it is exactly an even
    /// spread, so this one entry covers both that and every degree of forward or backward preference.
    /// </summary>
    HenyeyGreenstein,

    /// <summary>
    /// This entry notes Rayleigh's shape, for particles far smaller than the wavelength of the light
    /// crossing them.  It is what makes a clear sky blue.
    /// </summary>
    Rayleigh
}
