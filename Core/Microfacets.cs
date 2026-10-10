using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class holds the arithmetic of a rough surface: one made of microscopic flat facets, each a
/// perfect mirror, turned every which way about the surface's own normal.  Seen from any distance the
/// facets are too small to make out, and what shows is the blur of everything they mirror between
/// them.  How far they lean is given by GGX's distribution (Trowbridge and Reitz's, from 1975), the
/// one every physically based renderer has settled on: it has the long tail that makes a real
/// highlight fade out softly rather than stop at an edge.
/// <para>
/// **Everything here is worked in the surface's own frame**, with the normal along Z, and with
/// <c>alpha</c> -- the square of a material's roughness -- saying how far the facets lean.  At
/// nothing they all face straight out, and the surface is a mirror.
/// </para>
/// </summary>
public static class Microfacets
{
    /// <summary>
    /// This method returns how densely the facets crowd at a given lean, per unit of solid angle: the
    /// distribution itself, <c>D</c>.  Over the whole hemisphere, weighted by how squarely each facet
    /// faces out, it comes to exactly one.
    /// </summary>
    /// <param name="cosLean">The cosine of the angle between a facet's normal and the surface's.</param>
    /// <param name="alpha">How far the facets lean: the square of the roughness.</param>
    /// <returns>The density of facets at that lean.</returns>
    public static double Distribution(double cosLean, double alpha)
    {
        if (cosLean <= 0)
            return 0;

        double alphaSquared = alpha * alpha;
        double denominator = cosLean * cosLean * (alphaSquared - 1) + 1;

        return alphaSquared / (Math.PI * denominator * denominator);
    }

    /// <summary>
    /// This method returns how much of the surface can be seen from a direction rather than being
    /// hidden behind facets in front of it: Smith's masking term, <c>G1</c>.  It is all of it looking
    /// straight down and falls away toward a graze, more steeply the rougher the surface.
    /// </summary>
    /// <param name="cos">The cosine of the angle between the direction and the surface normal.</param>
    /// <param name="alpha">How far the facets lean.</param>
    /// <returns>The share of the facets visible from that direction.</returns>
    public static double Masking(double cos, double alpha)
    {
        if (cos <= 0)
            return 0;

        double alphaSquared = alpha * alpha;

        return 2 * cos / (cos + Math.Sqrt(alphaSquared + (1 - alphaSquared) * cos * cos));
    }

    /// <summary>
    /// This method picks a facet that the eye can see, in proportion to how much of the view it fills.
    /// It is Heitz's method (2018): squash the facets back to a hemisphere, pick a point on the half of
    /// the disc beneath it that the eye can see, and stretch it back.  Picking facets this way rather
    /// than by their distribution alone wastes nothing on facets turned away from the eye or hidden
    /// behind others, which is what makes a rough reflection settle in few samples.
    /// </summary>
    /// <param name="eye">The direction toward the eye, in the surface's frame (normal along Z).</param>
    /// <param name="alpha">How far the facets lean.</param>
    /// <param name="first">A number between nought and one choosing how far out on the disc.</param>
    /// <param name="second">A number between nought and one choosing which way round.</param>
    /// <returns>The facet's normal, in the surface's frame.</returns>
    public static Vector SampleVisibleNormal(Vector eye, double alpha, double first, double second)
    {
        Vector squashed = new Vector(alpha * eye.X, alpha * eye.Y, eye.Z).Unit;
        double lengthSquared = squashed.X * squashed.X + squashed.Y * squashed.Y;
        Vector across = lengthSquared > 0
            ? new Vector(-squashed.Y, squashed.X, 0) / Math.Sqrt(lengthSquared)
            : new Vector(1, 0, 0);
        Vector along = squashed.Cross(across);
        double radius = Math.Sqrt(first);
        double angle = 2 * Math.PI * second;
        double x = radius * Math.Cos(angle);
        double y = radius * Math.Sin(angle);
        double shift = 0.5 * (1 + squashed.Z);

        y = (1 - shift) * Math.Sqrt(1 - x * x) + shift * y;

        Vector onHemisphere = across * x + along * y + squashed * Math.Sqrt(Math.Max(0, 1 - x * x - y * y));

        return new Vector(alpha * onHemisphere.X, alpha * onHemisphere.Y, Math.Max(0, onHemisphere.Z)).Unit;
    }

    /// <summary>
    /// This method returns how likely <see cref="SampleVisibleNormal"/> is to send the eye's ray off in
    /// a given direction, per unit of solid angle: the visible facets' density at the facet that
    /// mirrors the one into the other, over the four times the cosine that turning a facet's lean into
    /// a mirrored direction costs.  It is what a light picked another way is weighed against, so that
    /// the two ways of finding it share it fairly.
    /// </summary>
    /// <param name="normal">The surface normal, of unit length.</param>
    /// <param name="eye">The direction toward the eye, of unit length.</param>
    /// <param name="direction">The mirrored direction, of unit length.</param>
    /// <param name="alpha">How far the facets lean.</param>
    /// <returns>The odds, per unit of solid angle; nought for a direction into the surface.</returns>
    public static double DirectionOdds(Vector normal, Vector eye, Vector direction, double alpha)
    {
        double cosEye = normal.Dot(eye);

        if (cosEye <= 0 || normal.Dot(direction) <= 0)
            return 0;

        Vector halfway = (eye + direction).Unit;

        return Masking(cosEye, alpha) * Distribution(normal.Dot(halfway), alpha) / (4 * cosEye);
    }

    /// <summary>
    /// This method returns how much a facet of glass mirrors, seen at a given angle, of light meeting it
    /// from a substance of one index of refraction toward another: Schlick's approximation, the same
    /// one a smooth crossing uses, and all of it where the light cannot get out at that angle.  Between
    /// two substances of the same index there is no surface to mirror anything at all.
    /// </summary>
    /// <param name="cos">The cosine of the angle between the eye and the facet's normal.</param>
    /// <param name="n1">The index of refraction on the eye's side.</param>
    /// <param name="n2">The index of refraction on the far side.</param>
    /// <returns>The share mirrored, between 0 and 1.</returns>
    public static double GlassReflectance(double cos, double n1, double n2)
    {
        if (n1 == n2)
            return 0;

        cos = Math.Clamp(cos, 0, 1);

        if (n1 > n2)
        {
            double ratio = n1 / n2;
            double sin2T = ratio * ratio * (1 - cos * cos);

            if (sin2T > 1)
                return 1;

            cos = Math.Sqrt(1 - sin2T);
        }

        double factor = (n1 - n2) / (n1 + n2);
        double squareOn = factor * factor;

        return squareOn + (1 - squareOn) * Math.Pow(1 - cos, 5);
    }

    /// <summary>
    /// This method returns which way the eye's ray goes on through one facet of glass, by Snell's law,
    /// or <c>null</c> where the facet turns it all back instead.
    /// </summary>
    /// <param name="eye">The direction toward the eye, of unit length.</param>
    /// <param name="facet">The facet's normal, of unit length, on the eye's side.</param>
    /// <param name="ratio">The eye's side's index of refraction over the far side's.</param>
    /// <returns>The direction the ray goes on in, of unit length.</returns>
    public static Vector Refract(Vector eye, Vector facet, double ratio)
    {
        double cosIn = eye.Dot(facet);
        double sin2Out = ratio * ratio * (1 - cosIn * cosIn);

        if (sin2Out > 1)
            return null;

        return facet * (ratio * cosIn - Math.Sqrt(1 - sin2Out)) - eye * ratio;
    }

    /// <summary>
    /// This method returns how brightly a lamp on the far side of a rough surface of glass shows through
    /// it toward the eye, for a light of unit color -- the soft glow a lamp makes behind frosted glass.
    /// <para>
    /// It is the facets' share of the light that gets through, as Walter and his colleagues worked it
    /// out (2007): the facets that would bend the light toward the eye are the ones whose normal lies
    /// along the generalized half vector between the two directions, weighted by how many there are, how
    /// much of each is seen and lit, and how much each lets through.  Times π, for the same reason as
    /// <see cref="Highlight"/>.  It leaves out the squared ratio of the indices that light carried
    /// into a denser substance gathers, as every ray this renderer bends through glass does, so that
    /// the glow and what those rays see agree; going in and coming back out, it cancels.
    /// </para>
    /// </summary>
    /// <param name="normal">The surface normal, of unit length, on the eye's side.</param>
    /// <param name="eye">The direction toward the eye, of unit length.</param>
    /// <param name="light">The direction toward the light, of unit length, on the far side.</param>
    /// <param name="alpha">How far the facets lean.</param>
    /// <param name="n1">The index of refraction on the eye's side.</param>
    /// <param name="n2">The index of refraction on the light's side.</param>
    /// <returns>How bright the glow is, for a light of unit color.</returns>
    public static double TransmittedHighlight(
        Vector normal, Vector eye, Vector light, double alpha, double n1, double n2)
    {
        double cosEye = normal.Dot(eye);
        double cosLight = normal.Dot(light);

        if (cosEye <= 0 || cosLight >= 0 || n1 == n2)
            return 0;

        double ratio = n2 / n1;
        Vector facet = light * ratio + eye;

        if (facet.Dot(facet) == 0)
            return 0;

        facet = facet.Unit;

        if (facet.Dot(normal) < 0)
            facet = -facet;

        double eyeOnFacet = eye.Dot(facet);
        double lightOnFacet = light.Dot(facet);

        // A facet the eye sees from behind, or the light reaches from in front, is not one the light
        // could have come through.
        if (eyeOnFacet <= 0 || lightOnFacet >= 0)
            return 0;

        double through = 1 - GlassReflectance(eyeOnFacet, n1, n2);
        double spread = lightOnFacet + eyeOnFacet / ratio;

        return Math.PI * Distribution(facet.Dot(normal), alpha) * Masking(cosEye, alpha) *
               Masking(-cosLight, alpha) * through * Math.Abs(lightOnFacet * eyeOnFacet) /
               (spread * spread * cosEye);
    }

    /// <summary>
    /// This method returns how likely rough glass's rays are to be bent off in a given direction on the
    /// far side, per unit of solid angle: the visible facets' density at the facet that bends the one
    /// into the other, times how much a change of facet moves the bent direction.  It is what a light
    /// seen through the glass another way is weighed against.
    /// </summary>
    /// <param name="normal">The surface normal, of unit length, on the eye's side.</param>
    /// <param name="eye">The direction toward the eye, of unit length.</param>
    /// <param name="light">The bent direction, of unit length, on the far side.</param>
    /// <param name="alpha">How far the facets lean.</param>
    /// <param name="n1">The index of refraction on the eye's side.</param>
    /// <param name="n2">The index of refraction on the far side.</param>
    /// <returns>The odds, per unit of solid angle; nought for a direction no facet bends the eye's ray
    /// into.</returns>
    public static double RefractionOdds(
        Vector normal, Vector eye, Vector light, double alpha, double n1, double n2)
    {
        double cosEye = normal.Dot(eye);

        if (cosEye <= 0 || normal.Dot(light) >= 0 || n1 == n2)
            return 0;

        double ratio = n2 / n1;
        Vector facet = light * ratio + eye;

        if (facet.Dot(facet) == 0)
            return 0;

        facet = facet.Unit;

        if (facet.Dot(normal) < 0)
            facet = -facet;

        double eyeOnFacet = eye.Dot(facet);
        double lightOnFacet = light.Dot(facet);

        if (eyeOnFacet <= 0 || lightOnFacet >= 0)
            return 0;

        double spread = lightOnFacet + eyeOnFacet / ratio;

        return Masking(cosEye, alpha) * eyeOnFacet * Distribution(facet.Dot(normal), alpha) / cosEye *
               Math.Abs(lightOnFacet) / (spread * spread);
    }

    /// <summary>
    /// This method returns two directions lying in a surface, square to each other and to its normal,
    /// which with the normal make the frame the rest of this class works in.  Which way round they
    /// point is of no consequence, the facets leaning alike every way.
    /// </summary>
    /// <param name="normal">The surface's normal, of unit length.</param>
    /// <returns>The two directions.</returns>
    public static (Vector Across, Vector Along) FrameAround(Vector normal)
    {
        Vector helper = Math.Abs(normal.X) < 0.9 ? new Vector(1, 0, 0) : new Vector(0, 1, 0);
        Vector across = helper.Cross(normal).Unit;

        return (across, normal.Cross(across));
    }

    /// <summary>
    /// This method returns how brightly a lamp shows in a rough surface: the facets' reflection of a
    /// light arriving from one direction, toward the eye, for a light of unit color.
    /// <para>
    /// It is the facets' share of the light, <c>D G F / 4 (n·l)(n·v)</c>, times the cosine the light
    /// arrives at -- and times π, because this renderer's lights carry their irradiance divided by π,
    /// which is what lets the diffuse term be the pigment times the cosine and nothing else.  A
    /// highlight worked out any other way would disagree with the same lamp's reflection seen through
    /// the surface's own rays.
    /// </para>
    /// </summary>
    /// <param name="normal">The surface normal, of unit length.</param>
    /// <param name="eye">The direction toward the eye, of unit length.</param>
    /// <param name="light">The direction toward the light, of unit length.</param>
    /// <param name="alpha">How far the facets lean.</param>
    /// <param name="reflectance">How much a facet mirrors, given the cosine between the eye and the
    /// facet's own normal.</param>
    /// <returns>How bright the highlight is, for a light of unit color.</returns>
    public static double Highlight(
        Vector normal, Vector eye, Vector light, double alpha, Func<double, double> reflectance)
    {
        double cosEye = normal.Dot(eye);
        double cosLight = normal.Dot(light);

        if (cosEye <= 0 || cosLight <= 0)
            return 0;

        Vector halfway = (eye + light).Unit;
        double cosHalfway = normal.Dot(halfway);

        return Math.PI * Distribution(cosHalfway, alpha) * Masking(cosEye, alpha) *
               Masking(cosLight, alpha) * reflectance(eye.Dot(halfway)) / (4 * cosEye);
    }
}
