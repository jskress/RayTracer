using System.Collections.Concurrent;
using RayTracer.Basics;
using RayTracer.Graphics;

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
    /// This method returns how much a rough surface whose facets each mirror all the light meeting them gives back,
    /// toward a direction at the given angle, of light from all round, in light mirrored by ONE facet: Kulla and
    /// Conty's directional albedo, <c>E</c>.
    /// <para>
    /// **The rest is lost to this renderer's facets but not to a real surface's.**  A facet whose mirror image points
    /// at another facet sends its light on to that one, and from there, mostly, out; the facets here mirror once and
    /// what they send at each other is dropped.  It is little for a surface of little roughness, and a great deal for
    /// a rough one: of light from all round, seen square on, nearly a third at roughness 0.7 and nearly three fifths at
    /// 0.9.
    /// See <see cref="Compensation"/>, which gives it back.
    /// </para>
    /// <para>
    /// **It is read from a table worked out for exactly these facets**: <see cref="Distribution"/>, and
    /// <see cref="Masking"/> taken for the eye and the light apart (Smith's separable form, not the height-correlated
    /// one most published tables are for).  The table runs evenly in the square root of the cosine, which crowds its
    /// steps toward a graze where the answer changes fastest, and evenly in the roughness, and is read between its
    /// steps in both: within 0.4% of the answer everywhere, and 0.02% on average.
    /// </para>
    /// </summary>
    /// <param name="cos">The cosine of the angle between the direction and the surface normal.</param>
    /// <param name="alpha">How far the facets lean: the square of the roughness.</param>
    /// <returns>The share given back in a single mirroring, from nought to one; one for a mirror.</returns>
    public static double Albedo(double cos, double alpha)
    {
        if (alpha <= 0)
            return 1;

        return Read(Albedos.Value, AlbedoSteps, cos, alpha);
    }

    /// <summary>
    /// This method returns how much of the light from all round meeting a rough surface of glass its facets give
    /// back toward a direction at the given angle in ONE meeting, mirrored and let through together: the glass's
    /// own albedo, <c>E</c>, as <see cref="Albedo"/> is a mirror's.
    /// <para>
    /// **Glass absorbs nothing at its surface**, so all that its facets lose, they lose to one another: what one
    /// mirrors or lets through and another meets is dropped.  Little is lost going in, the light bent in steeply;
    /// a great deal coming out, the light leaving bent toward a graze and hidden by the facets round it, and some
    /// turned back where it cannot get out.  From inside glass of index 1.5 a single meeting gives back 0.76 at
    /// roughness 0.7, and 0.42 at roughness 1; from outside, 0.97 and 0.90.  So a frosted ball, every ray going in
    /// and out of it, came out not half as bright as the light round it.
    /// </para>
    /// <para>
    /// **It is read from tables worked out for exactly what the glass's rays are counted by**: facets picked as
    /// <see cref="SampleVisibleNormal"/> picks them, each mirroring <see cref="GlassReflectance"/> of the light and
    /// letting the rest through, bent by <see cref="Refract"/>, each way counted by how much of the surface it can
    /// see.  Each runs as the mirror's does, but in half as many steps each way: within 0.1% of the answer on
    /// average, and far worse only within a hundredth of a degree of a graze.
    /// </para>
    /// <para>
    /// **Only the ratio of the two indices counts**, and the tables stand at steps of 5% in it, each worked out the
    /// first time it is needed, and are read between: within 0.15% of a table for the ratio itself.  A table for
    /// every pair of indices met, a dispersing glass's every band and every photon's own wavelength each made one,
    /// and a spectral render's caustics never finished.
    /// </para>
    /// </summary>
    /// <param name="cos">The cosine of the angle between the direction and the surface normal.</param>
    /// <param name="alpha">How far the facets lean: the square of the roughness.</param>
    /// <param name="n1">The index of refraction on the direction's side.</param>
    /// <param name="n2">The index of refraction on the far side.</param>
    /// <returns>The share given back in a single meeting, from nought to one; one for clear glass.</returns>
    public static double GlassAlbedo(double cos, double alpha, double n1, double n2)
    {
        if (alpha <= 0)
            return 1;

        double place = Math.Log(n2 / n1) / RatioStep;
        int below = (int) Math.Floor(place);
        double fraction = place - below;
        double albedo = Read(GlassAlbedosAt(below), GlassSteps, cos, alpha);

        return fraction == 0
            ? albedo
            : albedo * (1 - fraction) + Read(GlassAlbedosAt(below + 1), GlassSteps, cos, alpha) * fraction;
    }

    /// <summary>
    /// This method returns what rough glass's light, met once by its facets, is to be multiplied by to give back what
    /// the facets lose to one another: one over its albedo, glass losing nothing of its own (Turquin, 2019).  It
    /// multiplies what the glass mirrors and what it lets through alike, so how it shares the light between the two
    /// stands as one meeting has it.
    /// </summary>
    /// <param name="cos">The cosine of the angle between the eye and the surface normal.</param>
    /// <param name="alpha">How far the facets lean.</param>
    /// <param name="n1">The index of refraction on the eye's side.</param>
    /// <param name="n2">The index of refraction on the far side.</param>
    /// <returns>What to multiply the glass's light by; one for clear glass.</returns>
    public static double GlassCompensation(double cos, double alpha, double n1, double n2) =>
        alpha <= 0 ? 1 : 1 / GlassAlbedo(cos, alpha, n1, n2);

    // A table's value at the given cosine and lean, read between its steps in the square root of the cosine and in
    // the roughness.
    private static double Read(double[] table, int steps, double cos, double alpha)
    {
        double across = Math.Sqrt(Math.Clamp(cos, 0, 1)) * steps;
        double up = Math.Min(Math.Sqrt(alpha), 1) * steps;
        int column = Math.Min((int) across, steps - 1);
        int row = Math.Min((int) up, steps - 1);
        double x = across - column;
        double y = up - row;
        int at = column * (steps + 1) + row;

        return (table[at] * (1 - x) + table[at + steps + 1] * x) * (1 - y) +
               (table[at + 1] * (1 - x) + table[at + steps + 2] * x) * y;
    }

    /// <summary>
    /// This method returns what a rough lobe's light, mirrored once by its facets, is to be multiplied by to give back
    /// what the facets lose among themselves: Turquin's compensation (2019), <c>1 + F (1 / E - 1)</c>.
    /// <para>
    /// **It keeps the lobe's shape and puts back its light.**  The light facets send at each other comes out
    /// spread wider than the lobe, and is about <c>1 - E</c> of what reaches the surface, all of it where they
    /// mirror all of it -- so a rough white metal in an even white light vanishes against it, as it should,
    /// where it came out not half as bright at roughness 0.9.  Where they mirror less, each further mirroring
    /// takes its share again, so less comes back, and what does is colored the more by the surface: a rough
    /// copper is a deeper copper than a smooth one.  Kulla and Conty's way (2017) adds a lobe of its own for
    /// it, spread as a matte surface's; this one only scales the lobe already there, so every way a lobe is
    /// looked at, and every way two of them share a light, stands as it was.
    /// </para>
    /// </summary>
    /// <param name="cos">The cosine of the angle between the eye and the surface normal.</param>
    /// <param name="alpha">How far the facets lean that the lobe was worked out with.</param>
    /// <param name="average">How much a facet mirrors, averaged over light from all round; see
    /// <see cref="ReflectanceFromAround"/>.</param>
    /// <returns>What to multiply the lobe by; one for a mirror.</returns>
    public static double Compensation(double cos, double alpha, double average) =>
        alpha <= 0 ? 1 : CompensationFor(Albedo(cos, alpha), average);

    /// <summary>
    /// This method returns what a rough lobe's light is to be multiplied by, for facets whose albedo is given -- a
    /// brushed surface's, which hangs on more than how far its facets lean; see <see cref="Facets.AlbedoFor"/>.
    /// </summary>
    /// <param name="albedo">How much the facets give back of light from all round in a single mirroring.</param>
    /// <param name="average">How much a facet mirrors, averaged over light from all round.</param>
    /// <returns>What to multiply the lobe by; one for a mirror.</returns>
    public static double CompensationFor(double albedo, double average) => 1 + average * (1 / albedo - 1);

    /// <summary>
    /// This method returns what a rough lobe's light is to be multiplied by, band by band, for facets whose albedo
    /// is given.  See <see cref="CompensationFor(double, double)"/>.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="albedo">How much the facets give back of light from all round in a single mirroring.</param>
    /// <param name="average">How much a facet mirrors of each band, averaged over light from all round.</param>
    /// <returns>What to multiply the lobe by.</returns>
    public static TS CompensationFor<TS>(double albedo, TS average)
        where TS : struct, ISpectrum<TS>
    {
        return TS.White + average * (1 / albedo - 1);
    }

    /// <summary>
    /// This method returns what a rough lobe's light is to be multiplied by, band by band, for facets that mirror each
    /// band by its own share, as a metal's do.  See <see cref="Compensation(double, double, double)"/>.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="cos">The cosine of the angle between the eye and the surface normal.</param>
    /// <param name="alpha">How far the facets lean that the lobe was worked out with.</param>
    /// <param name="average">How much a facet mirrors of each band, averaged over light from all round.</param>
    /// <returns>What to multiply the lobe by.</returns>
    public static TS Compensation<TS>(double cos, double alpha, TS average)
        where TS : struct, ISpectrum<TS>
    {
        return alpha <= 0 ? TS.White : TS.White + average * (1 / Albedo(cos, alpha) - 1);
    }

    /// <summary>
    /// This method returns how much a surface mirrors of light from all round, given how much it mirrors at each
    /// angle: twice the integral of the share at each cosine times that cosine, light from all round reaching a
    /// surface in proportion to the cosine.
    /// </summary>
    /// <param name="reflectance">How much the surface mirrors, given the cosine between the light and its normal.
    /// </param>
    /// <returns>The share mirrored of light from all round.</returns>
    public static double ReflectanceFromAround(Func<double, double> reflectance)
    {
        const int steps = 256;
        double sum = 0;

        for (int step = 0; step < steps; step++)
        {
            double cos = (step + 0.5) / steps;

            sum += reflectance(cos) * cos;
        }

        return 2 * sum / steps;
    }

    /// <summary>
    /// This method returns how much a facet of glass mirrors of light from all round meeting it from a substance of
    /// one index of refraction toward another; see <see cref="GlassReflectance"/>.  Worked out once for each pair.
    /// </summary>
    /// <param name="n1">The index of refraction on the light's side.</param>
    /// <param name="n2">The index of refraction on the far side.</param>
    /// <returns>The share mirrored of light from all round.</returns>
    public static double GlassReflectanceFromAround(double n1, double n2) =>
        GlassFromAround.GetOrAdd((n1, n2), pair =>
            ReflectanceFromAround(cos => GlassReflectance(cos, pair.Item1, pair.Item2)));

    private static readonly ConcurrentDictionary<(double, double), double> GlassFromAround = new ();

    // How many steps the glass's tables take across each of their two ways, and the step between them in the
    // logarithm of the ratio of the indices.
    private const int GlassSteps = 32;
    private const double RatioStep = 0.05;

    // The glass's albedo, for each step of the ratio of the indices needed so far.
    private static readonly ConcurrentDictionary<int, double[]> GlassAlbedos = new ();

    private static double[] GlassAlbedosAt(int step) => GlassAlbedos.GetOrAdd(step, MeasureGlassAlbedos);

    // How many steps the albedo table takes across each of its two ways, and how many places each step is worked out
    // from each way.
    private const int AlbedoSteps = 64;
    private const int AlbedoPlaces = 64;

    private static readonly Lazy<double[]> Albedos = new (MeasureAlbedos);

    // The share given back in a single mirroring at every step of the table: the facets the eye sees, picked as
    // SampleVisibleNormal picks them, each counted by how much of the surface its mirror image can see past the
    // facets in front.  The places are crowded toward the edge of the disc the facets are picked from, where the
    // facets lean furthest and the ones that mirror below the surface's horizon lie: spread evenly, a narrow lobe's
    // answer wobbled by half a percent as the number of places changed, where crowded it settles to within 0.03%.
    // Each step is worked out alone, so the table is the same however the rows are shared among threads.
    private static double[] MeasureAlbedos()
    {
        double[] albedos = new double[(AlbedoSteps + 1) * (AlbedoSteps + 1)];

        Parallel.For(0, AlbedoSteps + 1, column =>
        {
            double root = (double) column / AlbedoSteps;
            double cos = root * root;
            Vector eye = new (Math.Sqrt(1 - cos * cos), 0, cos);

            for (int row = 0; row <= AlbedoSteps; row++)
            {
                double roughness = (double) row / AlbedoSteps;
                double alpha = roughness * roughness;
                double sum = 0;

                if (alpha == 0)
                {
                    albedos[column * (AlbedoSteps + 1) + row] = 1;

                    continue;
                }

                for (int outward = 0; outward < AlbedoPlaces; outward++)
                {
                    double t = (outward + 0.5) / AlbedoPlaces;
                    double first = 1 - (1 - t) * (1 - t) * (1 - t);
                    double weight = 3 * (1 - t) * (1 - t);

                    for (int round = 0; round < AlbedoPlaces; round++)
                    {
                        Vector facet = SampleVisibleNormal(eye, alpha, first, (round + 0.5) / AlbedoPlaces);
                        double mirroredZ = 2 * eye.Dot(facet) * facet.Z - eye.Z;

                        sum += weight * Masking(mirroredZ, alpha);
                    }
                }

                albedos[column * (AlbedoSteps + 1) + row] = sum / (AlbedoPlaces * AlbedoPlaces);
            }
        });

        return albedos;
    }

    // The share glass gives back in a single meeting at every step of its table, as MeasureAlbedos works the mirror's
    // out: each facet the eye sees mirrors its share, counted by how much of the surface its mirror image can see,
    // and lets the rest through, bent, counted by how much of the surface the bent way can see from beneath -- as
    // GetRoughReflection's and Bend's rays are counted.
    private static double[] MeasureGlassAlbedos(int step)
    {
        const double n1 = 1;
        double n2 = Math.Exp(step * RatioStep);
        double[] albedos = new double[(GlassSteps + 1) * (GlassSteps + 1)];

        Parallel.For(0, GlassSteps + 1, column =>
        {
            double root = (double) column / GlassSteps;
            double cos = root * root;
            Vector eye = new (Math.Sqrt(1 - cos * cos), 0, cos);

            for (int row = 0; row <= GlassSteps; row++)
            {
                double roughness = (double) row / GlassSteps;
                double alpha = roughness * roughness;
                double sum = 0;

                if (alpha == 0)
                {
                    albedos[column * (GlassSteps + 1) + row] = 1;

                    continue;
                }

                for (int outward = 0; outward < AlbedoPlaces; outward++)
                {
                    double t = (outward + 0.5) / AlbedoPlaces;
                    double first = 1 - (1 - t) * (1 - t) * (1 - t);
                    double weight = 3 * (1 - t) * (1 - t);

                    for (int round = 0; round < AlbedoPlaces; round++)
                    {
                        Vector facet = SampleVisibleNormal(eye, alpha, first, (round + 0.5) / AlbedoPlaces);
                        double cosFacet = eye.Dot(facet);
                        double mirrored = GlassReflectance(cosFacet, n1, n2);
                        double given = mirrored * Masking(2 * cosFacet * facet.Z - eye.Z, alpha);

                        if (mirrored < 1 && Refract(eye, facet, n1 / n2) is { Z: < 0 } bent)
                            given += (1 - mirrored) * Masking(-bent.Z, alpha);

                        sum += weight * given;
                    }
                }

                albedos[column * (GlassSteps + 1) + row] = sum / (AlbedoPlaces * AlbedoPlaces);
            }
        });

        return albedos;
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
    /// This method returns how far the facets lean that a highlight of one sample of a lamp is worked out
    /// from, where the sample stands for a patch of the lamp's face the given angle across.
    /// <para>
    /// **An area light is looked at from a fixed few places across its face**, and the highlight of each in a
    /// surface of little roughness is a small fraction of a degree across, where the places may be degrees
    /// apart: a window showed in a satin floor, or in lacquer, as a grid of separate dots.  A place stands for
    /// its share of the face, though, not for a point, so its highlight is spread over that share -- the facets
    /// widened by half the share's angle, since a facet leaning by an angle turns what it mirrors by twice
    /// that, the two added as spreads add, in squares.  What the facets give back in all is the same however
    /// far they lean, so the window gives the same light in all, as one soft patch; on a surface rough enough
    /// for its highlights to overlap already, the widening is next to nothing.
    /// </para>
    /// <para>
    /// **Soft, not crisp**: GGX's long tail spreads half a near-mirror's highlight beyond the window's own
    /// image, so its middle is about half what a true mirror shows there.  Narrower, the dots came back -- a
    /// third of the share rippled by a twentieth, a quarter by a fifth, with an even grid of places and before
    /// the jitter opens gaps of up to two shares.  A crisp window in a near-mirror wants something there to
    /// mirror: a panel glowing by its `emission`.
    /// </para>
    /// </summary>
    /// <param name="alpha">How far the facets lean.</param>
    /// <param name="spread">The angle across the patch of the lamp the sample stands for, in radians; nought
    /// for a lamp that is a point or a direction.</param>
    /// <returns>How far the facets lean for that sample's highlight.</returns>
    public static double WidenedBy(double alpha, double spread) =>
        spread <= 0 ? alpha : Math.Sqrt(alpha * alpha + spread * spread / 4);

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
