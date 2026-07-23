using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class is the base of everything that lights a scene.
/// <para>
/// A light answers two questions, and the different sorts of light answer them differently.  The
/// first is which way, and how far off, it lies from a point being shaded: a lamp in the room is a
/// direction that swings as one moves about and a distance that shrinks as one nears it, where the
/// sun is the same direction everywhere and no reachable distance at all.  The second is how much
/// of itself it aims that way, which is the whole of what a spotlight adds -- everything within its
/// cone and nothing outside it.  With those two answered, the shading and the shadowing are the
/// same for every light, and live here.
/// </para>
/// </summary>
public abstract class Light : NamedThing
{
    /// <summary>
    /// This property notes the colour of the light.
    /// </summary>
    public Color Color { get; set; } = Colors.White;

    /// <summary>
    /// This method works out which way the light lies from the given point, and how far a shadow
    /// ray must travel before it has gone past the light and stops being able to shade the point.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>The unit direction from the point toward the light, and the distance to it, which
    /// is <see cref="double.PositiveInfinity"/> for a light with no place, such as the sun.</returns>
    public abstract (Vector Direction, double Distance) TowardFrom(Point point);

    /// <summary>
    /// This method works out how much of the light is aimed at the given point, before anything in
    /// the way is taken into account: all of it for a light that shines everywhere alike, and less,
    /// down to none, for a spotlight as the point falls toward the edge of its cone and out of it.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>The fraction of the light aimed at the point, between zero and one.</returns>
    public virtual double IntensityToward(Point point) => 1;

    /// <summary>
    /// This property notes how many places this light is looked at from when a point is shaded.
    /// It is one for every light with no width to it -- a lamp, the sun, a spotlight -- and more
    /// only for an area light, which is looked at across its face so that its shadow may soften.
    /// </summary>
    public virtual int SampleCount => 1;

    /// <summary>
    /// This method works out one of the places this light is looked at from, of the
    /// <see cref="SampleCount"/> there are.  A light with no width is the whole of itself from its
    /// one place, so it ignores the index and answers as it lies; an area light spreads its
    /// samples across its face.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <param name="index">Which sample, from zero up to <see cref="SampleCount"/>.</param>
    /// <returns>The sample: which way it lies, how far off, and how much of the light it carries.</returns>
    public virtual LightSample SampleToward(Point point, int index)
    {
        (Vector direction, double distance) = TowardFrom(point);

        return new LightSample(direction, distance, IntensityToward(point));
    }

    /// <summary>
    /// This method shades a point under this light looked at from where it lies, which is the
    /// whole of it for every light but an area one.  It is the convenient form for a caller that
    /// has no sample of its own in hand.
    /// </summary>
    /// <param name="point">The point being illuminated.</param>
    /// <param name="eye">The eye vector.</param>
    /// <param name="normal">The surface normal vector.</param>
    /// <param name="surface">The surface being illuminated.</param>
    /// <param name="lightReaching">How much of this light arrives at the point.</param>
    /// <returns>The resulting color.</returns>
    public Color ApplyPhong(Point point, Vector eye, Vector normal, Surface surface, Color lightReaching)
    {
        return ApplyPhong(point, eye, normal, surface, SampleToward(point, 0), lightReaching);
    }

    /// <summary>
    /// This method works out the colour a surface takes on under this light, by Phong's reckoning:
    /// an ambient term that a shadow does not touch, a diffuse term for how squarely the surface
    /// faces the light, and a specular highlight for how nearly it mirrors the light into the eye.
    /// </summary>
    /// <param name="point">The point being illuminated.</param>
    /// <param name="eye">The eye vector.</param>
    /// <param name="normal">The surface normal vector.</param>
    /// <param name="surface">The surface being illuminated.</param>
    /// <param name="sample">The place on the light this shading looks at it from -- which way it
    /// lies, and how much of it is aimed this way.  For every light but an area light there is one
    /// such place; an area light is shaded once per sample and the results averaged.</param>
    /// <param name="lightReaching">How much of this light arrives at the point: white, if it is
    /// in full view of the light, black, if something opaque stands in the way, and something in
    /// between if what stands in the way lets light through.</param>
    /// <returns>The resulting color.</returns>
    public Color ApplyPhong(
        Point point, Vector eye, Vector normal, Surface surface, LightSample sample,
        Color lightReaching)
    {
        Material material = surface.Material ?? Material.Default;
        // The pigment's own colour is kept as well as the lit one, because a metallic highlight
        // tints by the surface's colour alone -- using the lit colour would fold the light in twice.
        Color pigmentColor = material.Pigment.GetColorFor(surface, point);
        Color color = pigmentColor * Color;
        Vector vector = sample.Direction;

        // Ambient light stands in for light that has bounced around the scene rather than come
        // straight from this source, so it is the one term a shadow does not take away.
        Color ambientColor = color * material.Ambient;

        // How much of the light is pointed this way at all, which is where a spotlight's cone comes
        // in.  It scales everything that depends on the light arriving, and leaves the ambient term
        // alone -- a point outside the cone is as good as one in shadow, but still catches whatever
        // bounced light the ambient stands for.  A plain light aims all of itself everywhere, so
        // the reaching colour is passed straight through untouched, which keeps such a light's
        // shading exactly what it was before any of this existed.
        double intensity = sample.Cone;
        Color reaching = intensity == 1 ? lightReaching : lightReaching * intensity;

        if (reaching.Matches(Colors.Black))
            return ambientColor;

        Color diffuseColor;
        Color specularColor;
        double lightDotNormal = vector.Dot(normal);

        if (lightDotNormal < 0)
            diffuseColor = specularColor = Colors.Black;
        else
        {
            // How much of the light the surface takes, before its colour is applied.  Brilliance
            // shapes how the falloff runs as the surface turns away, and the grain then takes fine
            // flecks back out of it -- both in that order, and both POV-Ray's.
            double surfaceIntensity = material.Brilliance == 1
                ? lightDotNormal
                : Math.Pow(lightDotNormal, material.Brilliance);

            surfaceIntensity = Math.Max(0, surfaceIntensity * material.Diffuse - material.GrainAt(point));

            diffuseColor = color * surfaceIntensity;

            Vector reflect = (-vector).Reflect(normal);
            double reflectDotEye = reflect.Dot(eye);

            if (reflectDotEye < 0)
                specularColor = Colors.Black;
            else
            {
                double factor = Math.Pow(reflectDotEye, material.Shininess);

                specularColor = Color * material.Specular * factor;

                // A metal's highlight takes the colour of the metal rather than of the light.  The
                // angle used is the light against the normal, which is the approximation POV-Ray
                // makes here too: the halfway vector would be more correct, but Phong exists
                // precisely to avoid computing it.
                if (material.Metallic != 0)
                    specularColor *= material.GetMetallicTint(pigmentColor, lightDotNormal);
            }
        }

        // Whatever the light lost on its way here is charged against both terms that depend on it
        // arriving.  Since it is a colour rather than a fraction, light that came through coloured
        // glass lights this surface in that colour.
        //
        // The two terms are charged separately rather than added and charged together, which looks
        // like the long way round and is done on purpose: it keeps the additions grouped exactly as
        // they were before any of this existed.  Floating point addition does not associate, so
        // regrouping them shifts the odd pixel by one level even when nothing has been lost -- and
        // a scene with nothing transparent in it should come out bit for bit unchanged.
        return ambientColor + diffuseColor * reaching + specularColor * reaching;
    }
}
