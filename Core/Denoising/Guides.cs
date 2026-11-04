using RayTracer.Basics;
using RayTracer.Scanners;

namespace RayTracer.Core.Denoising;

/// <summary>
/// This class holds what each pixel of a picture shows of the surfaces in it, apart from the light on them: their
/// color, which way they face, where they are and how wide a pixel is there.  It is what a denoiser is guided by
/// (see <see cref="Scene.GuideFor"/>).
/// <para>
/// **The guides are gathered as the picture is**, so their edges fall where its edges do, as Intel's guide to its
/// denoiser asks: a picture with no anti-aliasing looks through each pixel's middle, as its light was found, and
/// one with anti-aliasing through sixteen places spread evenly over the pixel, which its adaptive samples average
/// toward.  Through a lens or an open shutter each place takes the lens's and the shutter's samples in turn, every
/// one of them at least once.  Nothing is drawn by chance, so the guides hold no grain of their own.
/// </para>
/// </summary>
public sealed class Guides
{
    // How many places across a pixel an anti-aliased picture's guides are gathered from, each way.
    private const int Across = 4;

    /// <summary>
    /// This property holds how many pixels across the picture is.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// This property holds how many pixels down the picture is.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// This property holds each pixel's color, three numbers a pixel between nought and one, row by row.
    /// </summary>
    internal float[] Albedo { get; }

    /// <summary>
    /// This property holds which way each pixel's surfaces face, three numbers a pixel: the average of their unit
    /// normals, so shorter than one where a pixel straddles an edge, and nought where it shows only the sky.
    /// </summary>
    internal float[] Normal { get; }

    /// <summary>
    /// This property holds where in the world each pixel's surfaces are, three numbers a pixel: the average place
    /// over what the pixel met.
    /// </summary>
    internal double[] Place { get; }

    /// <summary>
    /// This property holds how wide the ray is where it meets each pixel's surfaces: half a pixel's width there.
    /// </summary>
    internal float[] Footprint { get; }

    /// <summary>
    /// This property holds how much of each pixel met a surface rather than the sky.
    /// </summary>
    internal float[] Met { get; }

    /// <summary>
    /// This property holds how much of what each pixel shows mirrors smoothly, which may show a lamp as one bright
    /// point; a bright point on anything else is grain.
    /// </summary>
    internal float[] Mirrors { get; }

    internal Guides(int width, int height)
    {
        Width = width;
        Height = height;
        Albedo = new float[width * height * 3];
        Normal = new float[width * height * 3];
        Place = new double[width * height * 3];
        Footprint = new float[width * height];
        Met = new float[width * height];
        Mirrors = new float[width * height];
    }

    /// <summary>
    /// This method gathers the guides for a picture.
    /// </summary>
    /// <param name="scene">The scene the picture is of.</param>
    /// <param name="converter">What turns its pixels into rays.</param>
    /// <param name="antialiased">Whether its pixels' light was gathered from across them, or only their
    /// middles.</param>
    /// <param name="scanner">What to go over its pixels with.</param>
    /// <param name="width">How many pixels across it is.</param>
    /// <param name="height">How many pixels down it is.</param>
    /// <returns>The guides.</returns>
    internal static Guides Gather(
        Scene scene, PixelToRayConverter converter, bool antialiased, IScanner scanner, int width, int height)
    {
        Guides guides = new (width, height);
        int lens = converter.Sampler.SampleCount;
        int places = antialiased ? Across * Across : 1;
        int count = Math.Max(places, lens);

        scanner.Scan(width, height, (x, y) =>
        {
            GuideSample sum = default;
            int passed = 0;

            for (int index = 0; index < count; index++)
            {
                int place = index % places;
                int sample = index % lens;
                double centerX = antialiased ? (place % Across + 0.5) / Across : 0.5;
                double centerY = antialiased ? (place / Across + 0.5) / Across : 0.5;

                if (lens > 1 && !converter.Passes(x, y, centerX, centerY, 0, 0, sample))
                    continue;

                Ray ray = converter.GetRayForPixel(x, y, centerX, centerY, 0, 0, sample);

                sum.Add(scene.GuideFor(ray), 1);
                passed++;
            }

            guides.Set(x, y, passed == 0 ? sum : sum.Scaled(1.0 / passed));
        });

        return guides;
    }

    /// <summary>
    /// This method sets one pixel's guides from what its samples found, averaged.
    /// </summary>
    internal void Set(int x, int y, GuideSample found)
    {
        int at = y * Width + x;
        double met = found.Met;

        Albedo[at * 3] = (float) found.Red;
        Albedo[at * 3 + 1] = (float) found.Green;
        Albedo[at * 3 + 2] = (float) found.Blue;
        Normal[at * 3] = (float) found.NormalX;
        Normal[at * 3 + 1] = (float) found.NormalY;
        Normal[at * 3 + 2] = (float) found.NormalZ;
        Met[at] = (float) met;
        Mirrors[at] = (float) found.Mirrors;

        if (met > 0)
        {
            Place[at * 3] = found.PlaceX / met;
            Place[at * 3 + 1] = found.PlaceY / met;
            Place[at * 3 + 2] = found.PlaceZ / met;
            Footprint[at] = (float) (found.Footprint / met);
        }
    }

    /// <summary>
    /// This method returns one pixel's color.
    /// </summary>
    internal (double Red, double Green, double Blue) AlbedoAt(int x, int y)
    {
        int at = (y * Width + x) * 3;

        return (Albedo[at], Albedo[at + 1], Albedo[at + 2]);
    }

    /// <summary>
    /// This method returns which way one pixel's surfaces face, on average.
    /// </summary>
    internal (double X, double Y, double Z) NormalAt(int x, int y)
    {
        int at = (y * Width + x) * 3;

        return (Normal[at], Normal[at + 1], Normal[at + 2]);
    }
}
