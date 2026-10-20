using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class holds the facets of a surface brushed one way -- brushed steel, a turned pan bottom, the grooves
/// of a record -- which lean further across the grooves than along them.
/// <para>
/// **Brushing leaves grooves, and the facets are the grooves' sides.**  Along a groove the surface is nearly
/// flat; across it, it rises and falls, so the facets lean across and hardly at all along.  What they mirror
/// is spread across the grooves and not along them, which is why a lamp in brushed steel is a streak square
/// to the brushing, and a turned disc shows a bright line running out from its middle.  It is GGX's
/// distribution as every other rough surface's is, stretched: Heitz's own (2014, 2018) with a lean of its own
/// each way, and his way of picking the facets the eye can see, which handles a stretched distribution as
/// readily as a round one.
/// </para>
/// <para>
/// **How far is said as it is in Blender and in Disney's model**: a roughness, which stays the average --
/// the two leans multiply to the square of what a round surface of that roughness leans -- and an anisotropy
/// from nought, round, toward one, a third as rough along as across.  A surface that is not brushed is never
/// given one of these, and is worked out by <see cref="Microfacets"/> exactly as it always was.
/// </para>
/// <para>
/// Everything here is worked in the surface's own frame: <see cref="Along"/> the grooves, <see cref="Across"/>
/// them, and the normal.
/// </para>
/// </summary>
public sealed class Facets
{
    /// <summary>
    /// This constructor makes the facets of a surface brushed along a direction lying in it.
    /// </summary>
    /// <param name="normal">The surface's normal, of unit length.</param>
    /// <param name="along">Which way the grooves run, of unit length and square to the normal.</param>
    /// <param name="alphaAlong">How far the facets lean along the grooves.</param>
    /// <param name="alphaAcross">How far they lean across them.</param>
    public Facets(Vector normal, Vector along, double alphaAlong, double alphaAcross)
    {
        Normal = normal;
        Along = along;
        Across = normal.Cross(along);
        AlphaAlong = alphaAlong;
        AlphaAcross = alphaAcross;
    }

    /// <summary>
    /// This property holds the surface's normal.
    /// </summary>
    public Vector Normal { get; }

    /// <summary>
    /// This property holds which way the grooves run.
    /// </summary>
    public Vector Along { get; }

    /// <summary>
    /// This property holds the direction across the grooves.
    /// </summary>
    public Vector Across { get; }

    /// <summary>
    /// This property holds how far the facets lean along the grooves.
    /// </summary>
    public double AlphaAlong { get; }

    /// <summary>
    /// This property holds how far the facets lean across the grooves.
    /// </summary>
    public double AlphaAcross { get; }

    /// <summary>
    /// This method returns how far a brushed surface's facets lean along its grooves and across them, for a
    /// roughness and an anisotropy: <c>sqrt(1 - 0.9 a)</c> times the square of the roughness along them, and
    /// that much less across, so that the two multiply to the square of the roughness.
    /// </summary>
    /// <param name="roughness">The surface's roughness.</param>
    /// <param name="anisotropy">How much it is brushed, from nought to one.</param>
    /// <returns>The leans along the grooves and across them.</returns>
    public static (double Along, double Across) AlphasFor(double roughness, double anisotropy)
    {
        double alpha = roughness * roughness;
        double aspect = Math.Sqrt(1 - 0.9 * Math.Clamp(anisotropy, 0, 1));

        return (alpha * aspect, alpha / aspect);
    }

    /// <summary>
    /// This method returns the facets of the given surface where a ray met it, for a material that is
    /// brushed, or <c>null</c> for one that is not.
    /// <para>
    /// **Which way it is brushed is said in the surface's own space**, as a pattern is placed, so the grooves
    /// turn and stretch with it: along one direction, or round an axis through the space's middle.  That
    /// direction is carried out to the world and laid flat on the surface there.  Where it stands square to
    /// the surface, or a point lies on the axis it is brushed round, there are no grooves to speak of, and
    /// the frame is any at all.
    /// </para>
    /// </summary>
    /// <param name="material">The material to shade with.</param>
    /// <param name="surface">The surface the ray met.</param>
    /// <param name="point">Where it met it, in the world.</param>
    /// <param name="normal">The surface's normal there, in the world.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look at.</param>
    /// <param name="portals">The instances the surface was met through.</param>
    /// <returns>The facets, or <c>null</c>.</returns>
    public static Facets At(
        Material material, Surface surface, Point point, Vector normal, int timeIndex, Portals portals)
    {
        if (!material.IsAnisotropic)
            return null;

        Vector local = material.BrushedAround is { } axis
            ? axis.Cross(surface.WorldToSurface(point, timeIndex, portals) - Point.Zero)
            : material.BrushedAlong ?? new Vector(1, 0, 0);
        Vector world = surface.DirectionToWorld(local, timeIndex, portals);
        Vector along = world - normal * world.Dot(normal);

        along = along.Magnitude > 1e-9 ? along.Unit : Microfacets.FrameAround(normal).Across;

        (double alphaAlong, double alphaAcross) = AlphasFor(material.Roughness, material.Anisotropy);

        return new Facets(normal, along, alphaAlong, alphaAcross);
    }

    /// <summary>
    /// This method carries a direction in the world into the surface's own frame.
    /// </summary>
    public Vector ToLocal(Vector direction) =>
        new (direction.Dot(Along), direction.Dot(Across), direction.Dot(Normal));

    /// <summary>
    /// This method carries a direction in the surface's own frame out into the world, of unit length.
    /// </summary>
    public Vector ToWorld(Vector direction) =>
        (Along * direction.X + Across * direction.Y + Normal * direction.Z).Unit;

    /// <summary>
    /// This method returns how densely the facets crowd about a given facet normal, per unit of solid angle:
    /// GGX's distribution stretched, which, weighted by how squarely each facet faces out, comes to exactly one.
    /// </summary>
    /// <param name="facet">The facet's normal, in the surface's frame, of unit length.</param>
    /// <returns>The density of facets there.</returns>
    public double Distribution(Vector facet) => Distribution(facet, AlphaAlong, AlphaAcross);

    // The distribution, for facets leaning as far as given each way.
    private static double Distribution(Vector facet, double alphaAlong, double alphaAcross)
    {
        if (facet.Z <= 0)
            return 0;

        double x = facet.X / alphaAlong;
        double y = facet.Y / alphaAcross;
        double denominator = x * x + y * y + facet.Z * facet.Z;

        return 1 / (Math.PI * alphaAlong * alphaAcross * denominator * denominator);
    }

    /// <summary>
    /// This method returns how much of the surface can be seen from a direction rather than being hidden behind
    /// facets in front of it: Smith's masking term, with the lean the facets have in that direction's own
    /// plane.
    /// </summary>
    /// <param name="direction">The direction, in the surface's frame, of unit length.</param>
    /// <returns>The share of the facets visible from it.</returns>
    public double Masking(Vector direction) => Masking(direction, AlphaAlong, AlphaAcross);

    // The masking, for facets leaning as far as given each way.
    private static double Masking(Vector direction, double alphaAlong, double alphaAcross)
    {
        double cos = direction.Z;

        if (cos <= 0)
            return 0;

        double sideways = direction.X * direction.X + direction.Y * direction.Y;
        double alphaSquared = sideways > 0
            ? (alphaAlong * alphaAlong * direction.X * direction.X +
               alphaAcross * alphaAcross * direction.Y * direction.Y) / sideways
            : alphaAlong * alphaAcross;

        return 2 * cos / (cos + Math.Sqrt(alphaSquared + (1 - alphaSquared) * cos * cos));
    }

    /// <summary>
    /// This method picks a facet that the eye can see, in proportion to how much of the view it fills, by
    /// Heitz's method (2018) with the facets stretched each way by their own lean; see
    /// <see cref="Microfacets.SampleVisibleNormal"/>.
    /// </summary>
    /// <param name="eye">The direction toward the eye, in the surface's frame.</param>
    /// <param name="first">A number between nought and one choosing how far out on the disc.</param>
    /// <param name="second">A number between nought and one choosing which way round.</param>
    /// <returns>The facet's normal, in the surface's frame.</returns>
    public Vector SampleVisibleNormal(Vector eye, double first, double second)
    {
        Vector squashed = new Vector(AlphaAlong * eye.X, AlphaAcross * eye.Y, eye.Z).Unit;
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

        return new Vector(
            AlphaAlong * onHemisphere.X, AlphaAcross * onHemisphere.Y, Math.Max(0, onHemisphere.Z)).Unit;
    }

    /// <summary>
    /// This method returns how likely <see cref="SampleVisibleNormal"/> is to send the eye's ray off in a given
    /// direction, per unit of solid angle; see <see cref="Microfacets.DirectionOdds"/>.
    /// </summary>
    /// <param name="eye">The direction toward the eye, in the world, of unit length.</param>
    /// <param name="direction">The mirrored direction, in the world, of unit length.</param>
    /// <returns>The odds; nought for a direction into the surface.</returns>
    public double DirectionOdds(Vector eye, Vector direction)
    {
        Vector toEye = ToLocal(eye);
        Vector away = ToLocal(direction);

        if (toEye.Z <= 0 || away.Z <= 0)
            return 0;

        return Masking(toEye) * Distribution((toEye + away).Unit) / (4 * toEye.Z);
    }

    /// <summary>
    /// This method returns how brightly a lamp shows in the brushed surface, for a light of unit color; see
    /// <see cref="Microfacets.Highlight"/>.
    /// </summary>
    /// <param name="eye">The direction toward the eye, in the world, of unit length.</param>
    /// <param name="light">The direction toward the light, in the world, of unit length.</param>
    /// <param name="reflectance">How much a facet mirrors, given the cosine between the eye and the facet's
    /// own normal.</param>
    /// <param name="spread">The angle across the patch of an area light the lamp's sample stands for, which
    /// widens the facets each way as <see cref="Microfacets.WidenedBy"/> says; nought for any other lamp.</param>
    /// <returns>How bright the highlight is, for a light of unit color.</returns>
    public double Highlight(Vector eye, Vector light, Func<double, double> reflectance, double spread = 0)
    {
        Vector toEye = ToLocal(eye);
        Vector toLight = ToLocal(light);

        if (toEye.Z <= 0 || toLight.Z <= 0)
            return 0;

        Vector halfway = (toEye + toLight).Unit;
        double along = Microfacets.WidenedBy(AlphaAlong, spread);
        double across = Microfacets.WidenedBy(AlphaAcross, spread);

        return Math.PI * Distribution(halfway, along, across) * Masking(toEye, along, across) *
               Masking(toLight, along, across) * reflectance(toEye.Dot(halfway)) / (4 * toEye.Z);
    }
}
