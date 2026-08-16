using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class carries one place a light is looked at from a point being shaded.
/// <para>
/// A lamp, the sun and a spotlight are each looked at from a single place, and are the whole of
/// themselves seen from there.  An area light is not: it has width, so a point may see part of it
/// past an edge that hides the rest, which is what softens the edge of its shadow.  Shading it
/// means looking at it from several places across its face and averaging, and this is one of those
/// looks -- which way the sample lies, how far off it is, and how much of the light is aimed this
/// way once a cone is taken into account.
/// </para>
/// </summary>
/// <param name="Direction">The unit direction from the shaded point toward the sample.</param>
/// <param name="Distance">The distance to the sample, which bounds how far a shadow ray need
/// travel before it has passed the light; infinite for a light with no place, such as the sun.</param>
/// <param name="Carried">The color this particular sample carries, for a light whose color differs
/// from one of its places to the next -- a volume of glowing stuff is a different color and brightness
/// at every point inside it.  Null for every light that is one color all over, which is most of them.
/// </param>
/// <param name="Cone">What this sample is worth: one for a light looked at from one place, less for a
/// spotlight as the point falls toward the edge of its cone, and more than one where a light was sampled
/// over part of itself rather than the whole -- a sky looked at over the half a surface faces counts each
/// of those samples double, the cosine weighing they are about to be given averaging a half.</param>
public readonly record struct LightSample(
    Vector Direction, double Distance, double Cone, Color Carried = null);
