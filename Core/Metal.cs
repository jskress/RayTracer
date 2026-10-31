using System.Collections.Concurrent;
using System.Numerics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a metal: what a surface that is bare metal gives back of the light meeting it, worked
/// out from its complex index of refraction, band by band.
/// <para>
/// **A metal is a conductor, and its color is what it mirrors.**  Light that gets into one is swallowed within a
/// few dozen nanometers, so a metal shows no color of its own the way paint does: everything it gives back it
/// mirrors, and it mirrors each wavelength by its own share.  How much is fixed by the metal's index, which is
/// complex -- how far it bends light, and how fast it swallows it -- and Fresnel's equations, which for a metal
/// keep the share near what it is square-on most of the way to a graze, dip a little, and climb to all of it at
/// the very edge, its color washing out to white as they climb.  So gold mirrors much of the red and green that
/// reaches it and a third of the blue, and copper's salmon turns to orange toward its edge.
/// </para>
/// <para>
/// **A metal is had by name or by color.**  A named one (see <see cref="Metals"/>) takes its index from
/// measurements.  One given as a color -- for an alloy or a finish no one has measured, bronze or gunmetal --
/// takes an index made to mirror that color square-on, by Gulbrandsen's mapping (JCGT 3(4), 2014): with an edge
/// color said too, how each color fares at a slant -- climbing early where the edge is bright, dipping first
/// where it is dark; without, the edge pbrt gives a color, an index whose bending part is one.
/// </para>
/// <para>
/// What it mirrors at each angle is worked out once, against the index of whatever lies outside it -- air, water,
/// a coat -- for a thousand and twenty-four steps of the cosine, as each band and as red, green and blue, and
/// read between the steps in a straight line.
/// </para>
/// </summary>
public sealed class Metal
{
    // Steps across the cosine of the angle light meets the metal at.  A metal's share changes slowly but at the
    // very edge, where it climbs to all of it in a straight line in the cosine.
    private const int Steps = 1024;

    private static readonly ConcurrentDictionary<string, Metal> NamedMetals = new (StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<(double, double, double, double, double, double, bool), Metal>
        ColoredMetals = new ();

    private readonly Complex[] _indices;
    private readonly ConcurrentDictionary<double, Table> _tables = new ();
    private readonly (Metal From, Metal To, double Share)? _mixture;

    /// <summary>
    /// This property holds the metal's name, for one taken from <see cref="Metals"/>, or <c>null</c>.
    /// </summary>
    public string Name { get; }

    private Metal(string name, Complex[] indices)
    {
        Name = name;
        _indices = indices;
    }

    private Metal(Metal from, Metal to, double share)
    {
        _mixture = (from, to, share);
        _indices = share < 0.5 ? from._indices : to._indices;
    }

    /// <summary>
    /// This method returns the metal of the given name, as <see cref="Metals"/> knows it.
    /// </summary>
    /// <param name="name">The metal's name.</param>
    /// <returns>The metal.</returns>
    public static Metal Named(string name)
    {
        return NamedMetals.GetOrAdd(name, known => new Metal(
            known.ToLowerInvariant(),
            Enumerable.Range(0, SpectralColor.Bands)
                .Select(band => Metals.IndexAt(known, SpectralColor.WavelengthOf(band)))
                .ToArray()));
    }

    /// <summary>
    /// This method returns a metal that mirrors the given color square-on: its index, band by band, is
    /// Gulbrandsen's for the color's share of each band and the edge's.
    /// </summary>
    /// <param name="face">The color the metal mirrors square-on.</param>
    /// <param name="edge">How each color fares at a slant, before it whitens at the very edge -- climbing early
    /// where the edge is bright, dipping first where it is dark -- or <c>null</c> for pbrt's, an index whose
    /// bending part is one.</param>
    /// <returns>The metal.</returns>
    public static Metal FromColor(Color face, Color edge = null)
    {
        Color toward = edge ?? Colors.Black;

        return ColoredMetals.GetOrAdd(
            (face.Red, face.Green, face.Blue, toward.Red, toward.Green, toward.Blue, edge is null),
            _ =>
            {
                BandSpectrum faces = BandSpectrum.FromReflectance(face);
                BandSpectrum edges = BandSpectrum.FromReflectance(toward);
                Complex[] indices = new Complex[SpectralColor.Bands];

                for (int band = 0; band < SpectralColor.Bands; band++)
                {
                    indices[band] = edge is null
                        ? IndexFor(faces[band])
                        : IndexFor(faces[band], edges[band]);
                }

                return new Metal(null, indices);
            });
    }

    /// <summary>
    /// This method returns a metal part way between two, as a material blend has one: what it mirrors is each
    /// one's share of what that one mirrors.  Its index, for a film to lie on, is the nearer's.
    /// </summary>
    /// <param name="from">The metal at nought.</param>
    /// <param name="to">The metal at one.</param>
    /// <param name="share">How far between them, from nought to one.</param>
    /// <returns>The metal between.</returns>
    public static Metal Mixed(Metal from, Metal to, double share)
    {
        if (ReferenceEquals(from, to) || share <= 0)
            return from;

        return share >= 1 ? to : new Metal(from, to, share);
    }

    /// <summary>
    /// This method returns the index that mirrors the given share of a band square-on, with the bending part of
    /// it one: pbrt's way with a metal given by its color.
    /// </summary>
    /// <param name="face">The share mirrored square-on.</param>
    /// <returns>The complex index.</returns>
    internal static Complex IndexFor(double face)
    {
        double r = Math.Clamp(face, 0, 0.9999);

        return new Complex(1, 2 * Math.Sqrt(r / (1 - r)));
    }

    /// <summary>
    /// This method returns the index that mirrors the given share of a band square-on and turns toward the edge's
    /// share at a slant: Gulbrandsen's mapping.  An edge of nought gives the most an index can bend with nothing
    /// swallowed, which is glass; an edge of one the least.
    /// </summary>
    /// <param name="face">The share mirrored square-on.</param>
    /// <param name="edge">How the band fares at a slant, from nought, dipping first, to one, climbing early.</param>
    /// <returns>The complex index.</returns>
    internal static Complex IndexFor(double face, double edge)
    {
        double r = Math.Clamp(face, 0, 0.9999);
        double g = Math.Clamp(edge, 0, 1);
        double root = Math.Sqrt(r);
        double n = g * (1 - r) / (1 + r) + (1 - g) * (1 + root) / (1 - root);
        double k = Math.Sqrt(Math.Max(0, (r * (n + 1) * (n + 1) - (n - 1) * (n - 1)) / (1 - r)));

        return new Complex(n, k);
    }

    /// <summary>
    /// This method returns the metal's complex index of refraction in one band.
    /// </summary>
    /// <param name="band">Which band, from nought up to one less than <see cref="SpectralColor.Bands"/>.</param>
    /// <returns>The index there.</returns>
    public Complex IndexIn(int band) => _indices[band];

    /// <summary>
    /// This method returns how much of the light meeting a conductor at a given angle it mirrors: Fresnel's
    /// equations with a complex index, for light polarized each way, averaged.
    /// </summary>
    /// <param name="cos">The cosine of the angle between the light and the normal.</param>
    /// <param name="index">The conductor's index over the index of what lies outside it.</param>
    /// <returns>The share mirrored.</returns>
    public static double Reflectance(double cos, Complex index)
    {
        cos = Math.Clamp(cos, 0, 1);

        Complex bent = Complex.Sqrt(1 - (1 - cos * cos) / (index * index));
        Complex across = (cos - index * bent) / (cos + index * bent);
        Complex along = (index * cos - bent) / (index * cos + bent);

        return (Norm(across) + Norm(along)) / 2;
    }

    private static double Norm(Complex value) => value.Real * value.Real + value.Imaginary * value.Imaginary;

    /// <summary>
    /// This method returns how much of each band the metal mirrors of light meeting it at a given angle, in
    /// whatever bands the render carries light in.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="cos">The cosine of the angle between the light, or the eye, and the normal.</param>
    /// <param name="outside">The index of refraction of what lies outside the metal.</param>
    /// <returns>The share of each band mirrored.</returns>
    public TS ShareAt<TS>(double cos, double outside)
        where TS : struct, ISpectrum<TS>
    {
        if (_mixture is ({ } from, { } to, var share))
            return from.ShareAt<TS>(cos, outside) * (1 - share) + to.ShareAt<TS>(cos, outside) * share;

        Table table = TableFor(outside);
        double place = Math.Clamp(cos, 0, 1) * Steps;
        int step = Math.Min((int) place, Steps - 1);
        double fraction = place - step;
        TS result = TS.Black;

        if (TS.Count == 3)
        {
            for (int channel = 0; channel < 3; channel++)
                result[channel] = Between(table.Colors, step * 3 + channel, 3, fraction);
        }
        else if (TS.Count == 1)
        {
            int band = (int) Math.Clamp(
                (TS.Wavelength - SpectralColor.ShortestWavelength) / BandWidth, 0, SpectralColor.Bands - 1);

            result[0] = Between(table.Bands, step * SpectralColor.Bands + band, SpectralColor.Bands, fraction);
        }
        else
        {
            for (int band = 0; band < SpectralColor.Bands; band++)
            {
                result[band] = Between(
                    table.Bands, step * SpectralColor.Bands + band, SpectralColor.Bands, fraction);
            }
        }

        return result;
    }

    /// <summary>
    /// This method returns how much the metal mirrors of light meeting it at a given angle, on average over the
    /// bands: enough to say whether what it mirrors is worth following, or to weigh one way a photon may go
    /// against another.
    /// </summary>
    /// <param name="cos">The cosine of the angle between the light, or the eye, and the normal.</param>
    /// <param name="outside">The index of refraction of what lies outside the metal.</param>
    /// <returns>The share mirrored, on average.</returns>
    public double AverageAt(double cos, double outside)
    {
        if (_mixture is ({ } from, { } to, var share))
            return from.AverageAt(cos, outside) * (1 - share) + to.AverageAt(cos, outside) * share;

        Table table = TableFor(outside);
        double place = Math.Clamp(cos, 0, 1) * Steps;
        int step = Math.Min((int) place, Steps - 1);

        return Between(table.Averages, step, 1, place - step);
    }

    /// <summary>
    /// This method returns how much of each band the metal mirrors of light from all round, in whatever bands the
    /// render carries light in: what its facets mirror one another's light by.  See
    /// <see cref="Microfacets.Compensation{TS}"/>.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="outside">The index of refraction of what lies outside the metal.</param>
    /// <returns>The share of each band mirrored of light from all round.</returns>
    public TS ShareFromAround<TS>(double outside)
        where TS : struct, ISpectrum<TS>
    {
        if (_mixture is ({ } from, { } to, var share))
            return from.ShareFromAround<TS>(outside) * (1 - share) + to.ShareFromAround<TS>(outside) * share;

        Table table = TableFor(outside);
        TS result = TS.Black;

        if (TS.Count == 3)
        {
            for (int channel = 0; channel < 3; channel++)
                result[channel] = table.ColorsFromAround[channel];
        }
        else if (TS.Count == 1)
        {
            int band = (int) Math.Clamp(
                (TS.Wavelength - SpectralColor.ShortestWavelength) / BandWidth, 0, SpectralColor.Bands - 1);

            result[0] = table.BandsFromAround[band];
        }
        else
        {
            for (int band = 0; band < SpectralColor.Bands; band++)
                result[band] = table.BandsFromAround[band];
        }

        return result;
    }

    private const double BandWidth =
        (SpectralColor.LongestWavelength - SpectralColor.ShortestWavelength) / SpectralColor.Bands;

    private static double Between(double[] values, int at, int stride, double fraction)
    {
        return values[at] + (values[at + stride] - values[at]) * fraction;
    }

    /// <summary>
    /// This method returns what the metal mirrors at every step of the cosine against the given index outside it,
    /// working it out the first time it is asked for.
    /// </summary>
    private Table TableFor(double outside) => _tables.GetOrAdd(outside, MakeTable);

    private Table MakeTable(double outside)
    {
        double[] bands = new double[(Steps + 1) * SpectralColor.Bands];
        double[] colors = new double[(Steps + 1) * 3];
        double[] averages = new double[Steps + 1];
        Complex[] indices = _indices.Select(index => index / outside).ToArray();

        for (int step = 0; step <= Steps; step++)
        {
            double cos = (double) step / Steps;
            Span<double> perBand = bands.AsSpan(step * SpectralColor.Bands, SpectralColor.Bands);
            double sum = 0;

            for (int band = 0; band < SpectralColor.Bands; band++)
            {
                perBand[band] = Reflectance(cos, indices[band]);
                sum += perBand[band];
            }

            // A color as deep as gold's lies a little outside what red, green and blue can show, and would come
            // out mirroring a hair more than all of one: held to what a surface can give back.
            Color color = SpectralColor.ToColor(perBand);

            colors[step * 3] = Math.Clamp(color.Red, 0, 1);
            colors[step * 3 + 1] = Math.Clamp(color.Green, 0, 1);
            colors[step * 3 + 2] = Math.Clamp(color.Blue, 0, 1);
            averages[step] = sum / SpectralColor.Bands;
        }

        // Of light from all round: twice the integral of the share at each cosine times that cosine, taken by the
        // trapezoid rule over the same steps, and in red, green and blue from the colors as they are read above.
        double[] bandsFromAround = new double[SpectralColor.Bands];
        double[] colorsFromAround = new double[3];

        for (int step = 0; step <= Steps; step++)
        {
            double cos = (double) step / Steps;
            double weight = 2 * cos * (step == 0 || step == Steps ? 0.5 : 1) / Steps;

            for (int band = 0; band < SpectralColor.Bands; band++)
                bandsFromAround[band] += bands[step * SpectralColor.Bands + band] * weight;

            for (int channel = 0; channel < 3; channel++)
                colorsFromAround[channel] += colors[step * 3 + channel] * weight;
        }

        return new Table(bands, colors, averages, bandsFromAround, colorsFromAround);
    }

    /// <summary>
    /// This class holds what a metal mirrors at every step of the cosine against one index outside it: each band,
    /// in turn for each step; red, green and blue; and the bands' average.  And of light from all round, each band,
    /// and red, green and blue.
    /// </summary>
    private sealed record Table(
        double[] Bands, double[] Colors, double[] Averages, double[] BandsFromAround, double[] ColorsFromAround);
}

/// <summary>
/// This struct holds what is bare metal where a ray met a surface: which metal, how much of the surface is bare
/// metal there, the rest being whatever the material is otherwise -- a weathered metal's grime, a decal's paint --
/// and what lies outside it.
/// </summary>
/// <param name="Metal">The metal.</param>
/// <param name="Share">How much of the surface is bare metal there, from nought to one.</param>
/// <param name="Outside">The index of refraction of what lies outside the surface, or outside its coat.</param>
/// <param name="Coat">The clear coat over the metal, or <c>null</c>.  Under one the metal mirrors against the
/// coat's own index, at the angle the light is bent to inside it.</param>
public readonly record struct BareMetal(Metal Metal, double Share, double Outside, Coat Coat = null)
{
    /// <summary>
    /// This method returns how much of each band the surface mirrors of light meeting it at a given angle: the
    /// metal's share where it is bare, and the rest of the material's where it is not.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="cos">The cosine of the angle between the light, or the eye, and the normal.</param>
    /// <param name="other">What the rest of the material mirrors at that angle.</param>
    /// <returns>The share of each band mirrored.</returns>
    public TS Mirrors<TS>(double cos, double other)
        where TS : struct, ISpectrum<TS>
    {
        TS metal = Coat is null
            ? Metal.ShareAt<TS>(cos, Outside)
            : Metal.ShareAt<TS>(Coat.Within(cos, Outside), Coat.IndexOfRefraction);

        return Share >= 1 ? metal : metal * Share + TS.White * ((1 - Share) * other);
    }

    /// <summary>
    /// This method returns how much the surface mirrors of light meeting it at a given angle, on average over the
    /// bands.  See <see cref="Mirrors{TS}"/>.
    /// </summary>
    /// <param name="cos">The cosine of the angle between the light, or the eye, and the normal.</param>
    /// <param name="other">What the rest of the material mirrors at that angle.</param>
    /// <returns>The share mirrored, on average.</returns>
    public double Average(double cos, double other)
    {
        double metal = Coat is null
            ? Metal.AverageAt(cos, Outside)
            : Metal.AverageAt(Coat.Within(cos, Outside), Coat.IndexOfRefraction);

        return Share >= 1 ? metal : metal * Share + (1 - Share) * other;
    }

    /// <summary>
    /// This method returns how much of each band the surface mirrors of light from all round: the metal's share where
    /// it is bare, and the rest of the material's where it is not.  Under a coat the metal's facets mirror one
    /// another's light inside the coat, so against the coat's index.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="other">What the rest of the material mirrors of light from all round.</param>
    /// <returns>The share of each band mirrored of light from all round.</returns>
    public TS MirrorsFromAround<TS>(double other)
        where TS : struct, ISpectrum<TS>
    {
        TS metal = Metal.ShareFromAround<TS>(Coat?.IndexOfRefraction ?? Outside);

        return Share >= 1 ? metal : metal * Share + TS.White * ((1 - Share) * other);
    }
}
