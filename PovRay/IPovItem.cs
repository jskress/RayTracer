namespace RayTracer.PovRay;

/// <summary>
/// This interface marks the things that may appear inside a POV-Ray block.  There are three of
/// them: a named property such as <c>turbulence 0.4</c>, a nested block such as a color map, and a
/// bare value, since a color map's entries are colors and a pigment may be nothing but a color.
/// </summary>
public interface IPovItem;
