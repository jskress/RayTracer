namespace RayTracer.Instructions.Core;

/// <summary>
/// This interface marks a resolver that produces a light.
/// <para>
/// It is the light's answer to <see cref="RayTracer.Instructions.Surfaces.ISurfaceResolver"/>, and it
/// exists for the same one reason: so that a light given a name may be found again without knowing
/// which sort it was.  A scene writes <c>light dusk</c>, not <c>sky light dusk</c> -- what sort of
/// light <c>dusk</c> is was settled where it was described, and saying it twice is the kind of thing
/// that gets edited in one place and not the other.
/// </para>
/// </summary>
public interface ILightResolver : IObjectResolver, ICloneable;
