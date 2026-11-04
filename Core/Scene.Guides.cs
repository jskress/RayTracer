using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene says what a ray from the eye finds of the surface it shows, apart from the light on it: its
/// color, which way it faces, and where it is.  A denoiser is guided by them (see
/// <see cref="Denoising.Guides"/>): grain in the light is smoothed out across a surface, and stops where its color,
/// its facing or its place says the surface does.
/// <para>
/// **A mirror or clear glass shows what is beyond it**, so what it shows is followed on, as Intel's guide to its
/// denoiser asks: through a surface whose mirroring and passing are most of what it shows, unless it is rough (a
/// rough one blurs what is beyond it, and is guide enough itself), the color found beyond each way is weighed by
/// that way's share, and tinted as the way tints it.  Every share is the one the shading weighs it by
/// (<see cref="SharesOf{TS}"/>), at the same place, so a guide is never noisier than the shape of the scene: nothing
/// in it is drawn by chance.
/// </para>
/// </summary>
public partial class Scene
{
    // How many mirrors and panes a guide is followed through, and the least share a way must have to be followed
    // at all: a way worth less leaves its share to the rest.
    private const int GuideDepth = 4;
    private const double LeastGuideShare = 1.0 / 32;

    /// <summary>
    /// This method works out what a ray from the eye finds of the surface it shows.
    /// </summary>
    /// <param name="ray">The ray from the eye.</param>
    /// <returns>What it finds.</returns>
    internal GuideSample GuideFor(Ray ray)
    {
        return GuideAlong(ray, GuideDepth);
    }

    /// <summary>
    /// This method works out what a ray finds of the surface it shows, strands finer than it laid over what is
    /// behind them by how much of the ray each covers, as <see cref="GetColorFor{TS}(Ray, int)"/> lays their light.
    /// </summary>
    private GuideSample GuideAlong(Ray ray, int remaining)
    {
        GuideSample seen = default;

        if (ray.Outside)
        {
            seen.Add(SkyGuide(ray), 1);

            return seen;
        }

        List<Intersection> hits = Intersect(ray);
        double left = 1;

        hits.Sort();

        foreach (Intersection hit in hits)
        {
            if (hit.Distance < 0)
                continue;

            hit.PrepareUsing(ray, hits, Environment.IndexOfRefraction);

            GuideSample here = GuideAt(hit, remaining);

            if (hit is not HairIntersection { Coverage: < 1 } strand)
            {
                seen.Add(here, left);

                return seen;
            }

            seen.Add(here, left * strand.Coverage);
            left *= 1 - strand.Coverage;

            if (left < MostlyCovered)
                return seen;
        }

        seen.Add(SkyGuide(ray), left);

        return seen;
    }

    /// <summary>
    /// This method works out what a ray that met nothing finds: the background's color, held to white, facing
    /// nowhere.
    /// </summary>
    private GuideSample SkyGuide(Ray ray)
    {
        Color sky = Background.GetTransformedLightFor<RgbSpectrum>(HeadingOf(ray)).ToColor();

        return GuideSample.Of(Held(sky), null, null, 0, 0);
    }

    /// <summary>
    /// This method works out what a ray finds of the surface at a crossing: its own color, or, for a smooth mirror
    /// or clear glass, what it shows of beyond it as well.
    /// </summary>
    private GuideSample GuideAt(Intersection hit, int remaining)
    {
        ShadeHere(hit.TimeIndex);

        Material material = SettledFor(hit);
        Color pigment = material.GetColorFor(hit);
        double transparency = material.PigmentMayTransmit
            ? material.TransparencyFor(pigment)
            : material.Transparency;
        BareMetal? metal = material.BareMetalAt(hit);
        FilmShares<RgbSpectrum>? film = material.Film?.SharesAt<RgbSpectrum>(
            hit, material.MetalApplies ? material.Metal : null);
        double cos = hit.Eye.Dot(hit.Normal);
        Color own = Held(pigment);

        // Bare metal's own color is what it mirrors looked at square on; the rest, its paint's.
        if (metal is { } bare)
        {
            Color square = bare.Mirrors<RgbSpectrum>(1, 0).ToColor();

            own = Held(new Color(
                square.Red + (1 - bare.Share) * own.Red, square.Green + (1 - bare.Share) * own.Green,
                square.Blue + (1 - bare.Share) * own.Blue));
        }

        (double mirrors, double passes) = SharesOf(hit, material, transparency, film, metal);
        bool smooth = !material.IsRough && CoatOn(hit, material) is not { IsRough: true };
        GuideSample here = GuideSample.Of(
            own, hit.Normal, hit.Point, hit.FootprintRadius, smooth && hit is not HairIntersection ? mirrors : 0);

        if (remaining < 1 || !smooth || mirrors + passes < 0.5 || hit is HairIntersection)
            return here;

        // Light passing a face it cannot leave by is all mirrored.
        Vector bent = passes >= LeastGuideShare ? Bent(hit, hit.N1, hit.N2) : null;

        if (bent is null)
        {
            mirrors += passes;
            passes = 0;
        }

        GuideSample shown = default;
        double ownShare = Math.Max(0, 1 - mirrors - passes);

        shown.Add(here, ownShare);

        if (mirrors >= LeastGuideShare)
        {
            Ray mirrored = new (hit.MirrorPoint, hit.Reflect, hit.TimeIndex, hit.ConeSpread, hit.ConeTravelled);
            Color tint = metal is { } shining ? HueOf(shining.Mirrors<RgbSpectrum>(cos, material.ReflectanceAt(cos)))
                : null;

            shown.Add(GuideAlong(mirrored, remaining - 1).Tinted(tint), mirrors);
        }

        if (passes >= LeastGuideShare)
        {
            Color tint = material.Interior.Filter > 0 ? material.Interior.GetFilterTint(pigment) : null;

            shown.Add(GuideAlong(RefractedRay(hit, bent), remaining - 1).Tinted(Held(tint)), passes);
        }

        return shown.Weight > 1 ? shown.Scaled(1 / shown.Weight) : shown;
    }

    /// <summary>
    /// This method returns a color held between black and white, as a guide's color must be.
    /// </summary>
    private static Color Held(Color color)
    {
        return color is null
            ? null
            : new Color(Math.Clamp(color.Red, 0, 1), Math.Clamp(color.Green, 0, 1), Math.Clamp(color.Blue, 0, 1));
    }

    /// <summary>
    /// This method returns the hue of the light a mirror gives back -- its color with its brightest channel made
    /// one -- for tinting what is seen in it, the share it gives back being weighed apart.
    /// </summary>
    private static Color HueOf(RgbSpectrum light)
    {
        Color color = light.ToColor();
        double most = Math.Max(color.Red, Math.Max(color.Green, color.Blue));

        return most <= 0 ? null : new Color(color.Red / most, color.Green / most, color.Blue / most);
    }
}

/// <summary>
/// This struct holds what a ray from the eye finds of the surface it shows (see <see cref="Scene.GuideFor"/>), or of
/// several, each weighed by its share of the ray: color, facing and place summed by weight, and the weight of
/// what was met at all -- a ray that met only the sky has a color and nothing else.
/// </summary>
internal struct GuideSample
{
    internal double Red, Green, Blue;
    internal double NormalX, NormalY, NormalZ;
    internal double PlaceX, PlaceY, PlaceZ;
    internal double Footprint;

    /// <summary>
    /// This field holds how much of what the ray met mirrors smoothly: what may show a lamp as one bright point that
    /// no chance put there, where a bright point on anything else is grain.
    /// </summary>
    internal double Mirrors;

    /// <summary>
    /// This field holds the share of the ray that met something: the weight its place and footprint were summed
    /// with, by which they are divided once a pixel's samples are all in.
    /// </summary>
    internal double Met;

    /// <summary>
    /// This field holds the share of the ray the whole is weighed by.
    /// </summary>
    internal double Weight;

    /// <summary>
    /// This method makes what a ray finds of one surface, or of the sky.
    /// </summary>
    /// <param name="color">Its color, between black and white.</param>
    /// <param name="normal">Which way it faces, or <c>null</c> for the sky.</param>
    /// <param name="place">Where it is, or <c>null</c> for the sky.</param>
    /// <param name="footprint">How wide the ray is there.</param>
    /// <param name="mirrors">How much of it mirrors smoothly.</param>
    internal static GuideSample Of(Color color, Vector normal, Point place, double footprint, double mirrors)
    {
        GuideSample sample = new ()
        {
            Red = color.Red, Green = color.Green, Blue = color.Blue, Mirrors = mirrors, Weight = 1
        };

        if (normal is not null)
        {
            sample.NormalX = normal.X;
            sample.NormalY = normal.Y;
            sample.NormalZ = normal.Z;
        }

        if (place is not null)
        {
            sample.PlaceX = place.X;
            sample.PlaceY = place.Y;
            sample.PlaceZ = place.Z;
            sample.Footprint = footprint;
            sample.Met = 1;
        }

        return sample;
    }

    /// <summary>
    /// This method adds another sample's findings, weighed by the given share.
    /// </summary>
    internal void Add(GuideSample other, double share)
    {
        if (share <= 0)
            return;

        Red += other.Red * share;
        Green += other.Green * share;
        Blue += other.Blue * share;
        NormalX += other.NormalX * share;
        NormalY += other.NormalY * share;
        NormalZ += other.NormalZ * share;
        PlaceX += other.PlaceX * share;
        PlaceY += other.PlaceY * share;
        PlaceZ += other.PlaceZ * share;
        Footprint += other.Footprint * share;
        Mirrors += other.Mirrors * share;
        Met += other.Met * share;
        Weight += other.Weight * share;
    }

    /// <summary>
    /// This method returns these findings with every sum scaled alike.
    /// </summary>
    internal GuideSample Scaled(double factor)
    {
        GuideSample scaled = default;

        scaled.Add(this, factor);

        return scaled;
    }

    /// <summary>
    /// This method returns these findings with their color tinted, or as they are for no tint.
    /// </summary>
    internal GuideSample Tinted(Color tint)
    {
        if (tint is null)
            return this;

        GuideSample tinted = this;

        tinted.Red *= tint.Red;
        tinted.Green *= tint.Green;
        tinted.Blue *= tint.Blue;

        return tinted;
    }
}
