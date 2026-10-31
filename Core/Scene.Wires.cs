using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene shades wires finer than the ray that meets them; see <see cref="ShadeWire{TS}"/>.
/// </summary>
public partial class Scene
{
    // Gauss-Legendre's sixteen places on [-1, 1] and their weights, for a polished wire's highlight
    // averaged round it.
    private static readonly (double Place, double Weight)[] Legendre = GaussLegendre(16);

    /// <summary>
    /// This method works out the light a wire finer than the ray that met it sends toward the eye: the
    /// average of what its whole round sends, which is what the ray, as wide as it is, sees.
    /// <para>
    /// **Shading the place the ray happened to meet would make a polished wire glitter.**  Its highlight is
    /// a narrow line along it, at the one place round it that mirrors a lamp toward the eye, and a ray met
    /// anywhere across it found that place only by chance: a scatter of bright single pixels where a
    /// string should be.  So each lamp's highlight is worked out over the whole round at once.  Met evenly
    /// across its width, every place round the wire counts alike in angle -- its being seen side on is just
    /// made up for by its mirroring at a graze -- so the highlight averaged round it is the facets'
    /// spread taken along a line through their middle, which GGX gives in closed form, cut where the round
    /// turns away.  It depends on how far the halfway between the eye and the lamp leans along the wire, and
    /// not on where round it the lamp stands: a wire glints as brightly lit from beside it or behind it as
    /// from in front, as a real one does.  A polished wire shown by Phong's stand-in for a highlight is
    /// averaged by quadrature, there being no closed form.
    /// </para>
    /// <para>
    /// **The rest is averaged round it too.**  Its diffuse light is the lit share of its round, in closed
    /// form; each lamp's shadow ray sets off from the side of the round facing that lamp; and what it
    /// mirrors is looked for at places spread evenly across its width, each mirroring as the round does
    /// there -- so its edges, seen at a graze, mirror what lies behind it.  A lamp a ray can meet, the sky
    /// or a glowing volume, shows only in what it mirrors, as on any rough surface.  A wire wide enough
    /// to be seen place by place is shaded as any surface is, and since what is here is that shading's
    /// exact average, a string seen at a slant does not change in brightness where it narrows below a
    /// pixel.
    /// </para>
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="hit">Where the ray met the wire, already prepared.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The light leaving it toward the eye.</returns>
    private TS ShadeWire<TS>(HairIntersection hit, int remaining)
        where TS : struct, ISpectrum<TS>
    {
        // For the patterns that look about the place being shaded; see Surroundings.
        ShadeHere(hit.TimeIndex);

        Material material = RegularizedFor(hit);
        StrandFrame frame = FrameOf(hit);
        Vector eye = hit.Eye;
        double cosEye = Math.Max(1e-9, eye.Dot(frame.Face));
        TS pigment = TS.FromReflectance(material.GetColorFor(hit));
        double ownShare = material.FresnelApplies ? 1 - material.ReflectanceAt(cosEye) : 1;
        BareMetal? metal = material.Film is null ? material.BareMetalAt(hit) : null;

        // Bare metal shows nothing of its own, as at any surface.
        if (metal is { } bare)
            ownShare *= 1 - bare.Share;

        double ambient = material.EffectiveAmbient * ownShare;
        TS color = TS.Black;

        foreach (Light listed in Lights)
        {
            Light light = listed.At(hit.TimeIndex);
            int count = light.SampleCount;
            // Along a path of bounced light, one sample, as at any surface there; see Illuminate.
            int one = count > 1 && _bounced > 0
                ? Math.Min(count - 1, (int) (ShiftFor(hit.Point, 111) * count))
                : -1;
            TS sum = TS.Black;

            for (int index = one < 0 ? 0 : one; index < (one < 0 ? count : one + 1); index++)
            {
                LightSample sample = light.SampleAround(frame.Center, index);
                TS emitted = light.EmittedToward<TS>(sample);

                sum += pigment * emitted * ambient;

                double intensity = sample.Cone * light.FadingOver(sample.Distance);
                Vector toward = sample.Direction;
                Vector round = toward - frame.Tangent * toward.Dot(frame.Tangent);
                double cosAxial = round.Magnitude;

                if (intensity <= 0 || cosAxial < 1e-9)
                    continue;

                Vector lit = round * (1 / cosAxial);
                TS reaching = GetLightReaching<TS>(
                    frame.Center + lit * frame.Radius, toward, sample.Distance - frame.Radius, hit.TimeIndex,
                    null, false, hit.FootprintRadius, light.Housing);

                if (reaching.IsBlack)
                    continue;

                reaching *= intensity;

                // The lit share of the round: cos(axial) (sin psi + (pi - psi) cos psi) / 4, psi the angle round
                // the wire from its middle to the side facing the lamp.
                if (material.Diffuse > 0)
                {
                    double psi = Math.Acos(Math.Clamp(lit.Dot(frame.Face), -1, 1));
                    double share = cosAxial * (Math.Sin(psi) + (Math.PI - psi) * Math.Cos(psi)) / 4;

                    sum += pigment * emitted * reaching * (share * material.Diffuse * ownShare);
                }

                if (_bounced == 0 && !light.CanBeSeen)
                    sum += GlintOf(material, frame, eye, cosEye, toward, light, sample, pigment, metal) * reaching;
            }

            color += one >= 0 || count == 1 ? sum : sum * (1.0 / count);
        }

        if ((material.Reflective > 0 || metal is not null) && remaining >= 1)
            color += MirroredRound<TS>(hit, material, frame, eye, pigment, remaining, metal);

        // The light it gives off of its own, which its color tints, as any surface's -- and, as any surface's,
        // only the share its skin does not mirror.
        if (material.Emission is { } glow)
            color += pigment * TS.FromIlluminant(glow) * ownShare;

        // Bounced light, taken at its middle as any matte surface's is.
        color += BouncedOnto<TS>(hit, material, remaining) * ownShare;

        return color;
    }

    /// <summary>
    /// This method returns a lamp's highlight on a wire, averaged round it, for the lamp's sample.
    /// </summary>
    private static TS GlintOf<TS>(
        Material material, StrandFrame frame, Vector eye, double cosEye, Vector toward, Light light,
        LightSample sample, TS pigment, BareMetal? metal)
        where TS : struct, ISpectrum<TS>
    {
        Vector halfway = (eye + toward).Unit;
        double sinAxial = Math.Clamp(halfway.Dot(frame.Tangent), -1, 1);
        Vector round = halfway - frame.Tangent * sinAxial;
        double cosAxial = round.Magnitude;

        if (cosAxial < 1e-9)
            return TS.Black;

        // Where round the wire the halfway faces: the place that mirrors the lamp to the eye.
        Vector peak = round * (1 / cosAxial);
        double at = Math.Atan2(peak.Dot(frame.Side), peak.Dot(frame.Face));

        if (Math.Abs(at) >= Math.PI / 2)
            return TS.Black;

        double factor;
        double albedo = 1;

        if (material.IsRough)
        {
            double spread = light.SpreadOf(sample);
            (double lengthwise, double around) = AlphasOf(material);

            // What the facets mirror among themselves, made up for as at the place that mirrors the lamp to the eye,
            // where the lobe stands; see Microfacets.Compensation.  Brushed, by facets of its own leans there.
            Facets brushed = material.IsAnisotropic ? new Facets(peak, frame.Tangent, lengthwise, around) : null;

            albedo = brushed?.AlbedoFor(brushed.ToLocal(eye), spread) ??
                     Microfacets.Albedo(eye.Dot(peak), Microfacets.WidenedBy(lengthwise, spread));
            lengthwise = Microfacets.WidenedBy(lengthwise, spread);
            around = Microfacets.WidenedBy(around, spread);

            factor = Math.PI * RoundTheLobe(frame, eye, toward, lengthwise, around, sinAxial, cosAxial, at) *
                     (metal is null ? material.ReflectanceAt(eye.Dot(halfway)) : 1) / (8 * cosEye);

            if (metal is null)
                factor *= Microfacets.CompensationFor(albedo, material.ReflectanceFromAround);
        }
        else
        {
            if (material.Specular <= 0)
                return TS.Black;

            factor = material.Specular * PhongRound(frame, eye, toward, material.Shininess, at);
        }

        if (factor <= 0)
            return TS.Black;

        TS glint = (material.IsRough || light.HighlightsBySample ? light.EmittedToward<TS>(sample) : light.Emitted<TS>()) *
                   factor;

        // Bare metal's glint is its own share of each band: at the angle the eye meets the facets for a rough wire,
        // as the facets' share was taken, and at the light's against the place that mirrors it for Phong's.
        if (metal is { } bare)
        {
            double cos = material.IsRough ? eye.Dot(halfway) : peak.Dot(toward);

            return material.IsRough
                ? glint * bare.Mirrors<TS>(cos, material.ReflectanceAt(cos)) *
                  Microfacets.CompensationFor(albedo, bare.MirrorsFromAround<TS>(material.ReflectanceFromAround))
                : glint * bare.Mirrors<TS>(cos, 1);
        }

        return material.Metallic != 0 && material.Metal is null
            ? glint * material.GetMetallicTint(pigment, peak.Dot(toward))
            : glint;
    }

    /// <summary>
    /// This method returns how far a wire's facets lean along its length and round it: its roughness's both
    /// ways, or for one brushed, across its grooves and along them -- a wound string's grooves running round
    /// it, so that it is rougher along its length, and drawn wire's along it.
    /// </summary>
    private static (double Lengthwise, double Round) AlphasOf(Material material)
    {
        double alpha = material.Roughness * material.Roughness;

        if (!material.IsAnisotropic)
            return (alpha, alpha);

        (double alongGrooves, double acrossGrooves) = Facets.AlphasFor(material.Roughness, material.Anisotropy);

        return material.BrushedAlong is null && material.BrushedAround is null
            ? (acrossGrooves, alongGrooves)
            : (alongGrooves, acrossGrooves);
    }

    /// <summary>
    /// This method returns Smith's masking for a direction at a place on a wire, its facets leaning as far as
    /// given along it and round it.
    /// </summary>
    private static double MaskingOf(Vector normal, Vector tangent, Vector direction, double lengthwise, double round)
    {
        double cos = direction.Dot(normal);

        if (cos <= 0)
            return 0;

        double along = direction.Dot(tangent);
        double aroundSquared = Math.Max(0, 1 - cos * cos - along * along);
        double sideways = along * along + aroundSquared;
        double alphaSquared = sideways > 0
            ? (lengthwise * lengthwise * along * along + round * round * aroundSquared) / sideways
            : lengthwise * round;

        return 2 * cos / (cos + Math.Sqrt(alphaSquared + (1 - alphaSquared) * cos * cos));
    }

    /// <summary>
    /// This method returns GGX's distribution, stretched as far as given along a wire and round it, times
    /// Smith's masking of the eye and the lamp, taken round the wire along the line its facings make: the
    /// integral over the angle t round the wire, from the place facing the halfway, of D G at the place it
    /// stands, over the places the eye sees, that face the halfway and that the lamp lights.
    /// <para>
    /// With the halfway at (sin d, cos d sin t, cos d cos t) in the frame of the place, D is
    /// 1 / (pi ax ay (P + Q sin^2 t)^2), P = sin^2 d / ax^2 + cos^2 d, Q = cos^2 d / ay^2 - cos^2 d.  However narrow
    /// it is, D dt becomes smooth in v, tan t = sqrt(P / R) tan v with R = P + Q: it is
    /// (cos^2 v + (P / R) sin^2 v) sqrt(P / R) / P^2 dv over pi ax ay.  So the integral is taken in v by
    /// Gauss-Legendre's sixteen places, the masking asked at each, exactly enough for a mirror's narrow lobe and
    /// for a rough one cut short where the round turns from the lamp.
    /// </para>
    /// </summary>
    private static double RoundTheLobe(
        StrandFrame frame, Vector eye, Vector toward, double lengthwise, double round, double sinAxial,
        double cosAxial, double at)
    {
        double cosSquared = cosAxial * cosAxial;
        double p = sinAxial * sinAxial / (lengthwise * lengthwise) + cosSquared;
        double r = p + cosSquared / (round * round) - cosSquared;
        double ratio = Math.Sqrt(p / r);

        // The places round the wire the eye sees, that the lamp lights, as angles from its middle; and of those,
        // the ones facing the halfway, as angles from the place facing it.
        double lamp = Math.Atan2(toward.Dot(frame.Side), toward.Dot(frame.Face));
        double low = Math.Max(Math.Max(-Math.PI / 2, lamp - Math.PI / 2) - at, -Math.PI / 2);
        double high = Math.Min(Math.Min(Math.PI / 2, lamp + Math.PI / 2) - at, Math.PI / 2);

        if (high <= low)
            return 0;

        double from = Smoothed(low, ratio);
        double to = Smoothed(high, ratio);
        double middle = (from + to) / 2;
        double half = (to - from) / 2;
        double sum = 0;

        foreach ((double place, double weight) in Legendre)
        {
            double v = middle + half * place;
            double cos = Math.Cos(v);
            double sin = Math.Sin(v);
            double t = Math.Atan(ratio * Math.Tan(v));
            double angle = at + t;
            Vector normal = frame.Face * Math.Cos(angle) + frame.Side * Math.Sin(angle);
            double masking = MaskingOf(normal, frame.Tangent, eye, lengthwise, round) *
                             MaskingOf(normal, frame.Tangent, toward, lengthwise, round);

            sum += weight * (cos * cos + p / r * sin * sin) * ratio / (p * p) * masking;
        }

        return sum * half / (Math.PI * lengthwise * round);
    }

    // The angle v for an angle t round the wire, tan v = tan t sqrt(R / P), which at a quarter turn is a quarter turn.
    private static double Smoothed(double t, double ratio) =>
        Math.Abs(t) >= Math.PI / 2 - 1e-12 ? Math.Sign(t) * Math.PI / 2 : Math.Atan(Math.Tan(t) / ratio);

    /// <summary>
    /// This method returns Phong's highlight averaged across a wire: half the integral over the angle round it
    /// of the highlight at each place, weighted by how much of its width the place takes up, over the places
    /// near the peak that the lamp lights.
    /// </summary>
    private static double PhongRound(StrandFrame frame, Vector eye, Vector toward, double shininess, double at)
    {
        double width = Math.Min(Math.PI / 2, 3 / Math.Sqrt(Math.Max(shininess, 1)));
        double low = Math.Max(-Math.PI / 2, at - width);
        double high = Math.Min(Math.PI / 2, at + width);
        double middle = (low + high) / 2;
        double half = (high - low) / 2;
        double sum = 0;

        foreach ((double place, double weight) in Legendre)
        {
            double angle = middle + half * place;
            Vector normal = frame.Face * Math.Cos(angle) + frame.Side * Math.Sin(angle);
            double cosLight = normal.Dot(toward);

            if (cosLight <= 0)
                continue;

            double mirrored = (-toward).Reflect(normal).Dot(eye);

            if (mirrored > 0)
                sum += weight * Math.Pow(mirrored, shininess) * Math.Cos(angle);
        }

        return sum * half / 2;
    }

    /// <summary>
    /// This method works out what a wire mirrors, averaged across it: places spread evenly across its width,
    /// each mirroring the eye's ray as the round does there.
    /// </summary>
    private TS MirroredRound<TS>(
        HairIntersection hit, Material material, StrandFrame frame, Vector eye, TS pigment, int remaining,
        BareMetal? metal)
        where TS : struct, ISpectrum<TS>
    {
        (int count, double weight) = RaysOf(hit, _spreading || TS.Count == 1 ? 1 : material.ReflectionSamples, 133);

        if (count == 0)
            return TS.Black;

        double alpha = material.Roughness * material.Roughness;
        double shift = ShiftFor(hit.Point, 131);
        double turn = ShiftFor(hit.Point, 132);
        TS sum = TS.Black;
        bool was = _spreading;
        double wasRoughness = _spreadRoughness;
        LookingRun? wasLooking = _looking;

        _spreading = true;
        _spreadRoughness = Math.Max(wasRoughness, material.Roughness);
        _looking = null;

        try
        {
            for (int index = 0; index < count; index++)
            {
                double across = (index + 0.5) / count + shift;
                double angle = Math.Asin(2 * (across - Math.Floor(across)) - 1);
                Vector normal = frame.Face * Math.Cos(angle) + frame.Side * Math.Sin(angle);
                Vector facet = normal;
                double masking = 1;
                double albedo = 1;

                if (material.IsRough)
                {
                    Facets facets = Facets.At(
                        material, hit.Surface, hit.Point, normal, hit.TimeIndex, hit.Portals, hit);
                    (Vector x, Vector y) = facets is null ? Microfacets.FrameAround(normal) : (facets.Along, facets.Across);
                    Vector local = new (eye.Dot(x), eye.Dot(y), eye.Dot(normal));

                    if (local.Z <= 0)
                        continue;

                    double second = index * 0.6180339887498949 + turn;

                    second -= Math.Floor(second);

                    Vector picked = facets?.SampleVisibleNormal(local, (index + 0.5) / count, second) ??
                                    Microfacets.SampleVisibleNormal(local, alpha, (index + 0.5) / count, second);

                    facet = (x * picked.X + y * picked.Y + normal * picked.Z).Unit;
                    albedo = facets?.AlbedoFor(local) ?? Microfacets.Albedo(local.Z, alpha);
                }

                double cosFacet = eye.Dot(facet);
                Vector direction = facet * (2 * cosFacet) - eye;

                if (cosFacet <= 0 || direction.Dot(normal) <= 0)
                    continue;

                direction = direction.Unit;

                if (material.IsRough)
                {
                    Facets facets = Facets.At(
                        material, hit.Surface, hit.Point, normal, hit.TimeIndex, hit.Portals, hit);

                    masking = facets?.Masking(facets.ToLocal(direction)) ??
                              Microfacets.Masking(direction.Dot(normal), alpha);
                }

                Ray ray = new (
                    frame.Center + normal * (frame.Radius * (1 + 1e-6) + 1e-9), direction, hit.TimeIndex,
                    hit.ConeSpread, hit.ConeTravelled);
                if (metal is { } bare)
                {
                    TS mirrors = bare.Mirrors<TS>(cosFacet, material.ReflectanceAt(cosFacet));

                    // What the facets mirror among themselves, made up for at this place; see
                    // Microfacets.Compensation.
                    if (material.IsRough)
                    {
                        mirrors *= Microfacets.CompensationFor(
                            albedo, bare.MirrorsFromAround<TS>(material.ReflectanceFromAround));
                    }

                    sum += GetColorFor<TS>(ray, remaining - 1) * mirrors * masking;

                    continue;
                }

                if (material.IsRough)
                    masking *= Microfacets.CompensationFor(albedo, material.ReflectanceFromAround);

                TS seen = GetColorFor<TS>(ray, remaining - 1) * (material.ReflectanceAt(cosFacet) * masking);

                sum += material.Metallic != 0 && material.Metal is null
                    ? seen * material.GetMetallicTint(pigment, eye.Dot(normal))
                    : seen;
            }
        }
        finally
        {
            _spreading = was;
            _spreadRoughness = wasRoughness;
            _looking = wasLooking;
        }

        return sum * (weight / count);
    }

    /// <summary>
    /// This method returns how many of a look's `count` rays a wire finer than the ray that met it sends, and
    /// how much each counts for.
    /// <para>
    /// **In proportion to its share of that ray, by chance below one ray**, as it takes a lamp's samples.  A
    /// wire seen from far off is widened to the ray and meets it in its share, and a ray from that far off
    /// takes in every string of a piano at once -- each of which, sending a mirror's sixteen rays and a
    /// bounce's sixteen more, met the same couple of hundred strings again along every one of them: the
    /// piano's documented example took 52 minutes at 40 x 30.  So a wire whose share times `count` comes to
    /// less than one sends a single ray as often as that says, counted for as much more as it is sent less,
    /// which is the same on average; and the rays every wire along a ray sends then add up to no more than
    /// `count` however many there are.  A wire whose share times `count` is one or more sends them all, as
    /// it always did, so a string seen close enough to cover a sixteenth of its pixel is shaded as before.
    /// </para>
    /// </summary>
    /// <param name="hit">The wire, met.</param>
    /// <param name="count">How many rays the look would send.</param>
    /// <param name="salt">What the chance is drawn from, at the place.</param>
    /// <returns>How many rays to send, and the weight their average counts for.</returns>
    private static (int Count, double Weight) RaysOf(HairIntersection hit, int count, int salt)
    {
        double wanted = count * hit.Share;

        if (wanted >= 1 || hit.Coverage >= 1)
            return (count, 1);

        return ShiftFor(hit.Point, salt) < wanted ? (1, 1 / wanted) : (0, 0);
    }

    // The places and weights of Gauss-Legendre quadrature of the given order on [-1, 1], by Newton's method on
    // the Legendre polynomial from Chebyshev's guesses.
    private static (double Place, double Weight)[] GaussLegendre(int order)
    {
        (double, double)[] result = new (double, double)[order];

        for (int index = 0; index < order; index++)
        {
            double x = Math.Cos(Math.PI * (index + 0.75) / (order + 0.5));
            double derivative = 0;

            for (int step = 0; step < 100; step++)
            {
                double p0 = 1, p1 = x;

                for (int n = 2; n <= order; n++)
                    (p0, p1) = (p1, ((2 * n - 1) * x * p1 - (n - 1) * p0) / n);

                derivative = order * (x * p1 - p0) / (x * x - 1);

                double next = x - p1 / derivative;

                if (Math.Abs(next - x) < 1e-15)
                {
                    x = next;
                    break;
                }

                x = next;
            }

            result[index] = (x, 2 / ((1 - x * x) * derivative * derivative));
        }

        return result;
    }
}
