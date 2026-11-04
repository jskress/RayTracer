using RayTracer.Graphics;
using RayTracer.Scanners;

namespace RayTracer.Core.Denoising;

/// <summary>
/// This class is the renderer's own denoiser: the edge-avoiding à-trous wavelet filter of Schied and others'
/// spatiotemporal variance-guided filtering (2017), as a first frame with nothing gathered before it has it.
/// <para>
/// **What is smoothed is the light on the surfaces, not their colors.**  Each pixel's light is divided by its
/// color (<see cref="Guides.Albedo"/>), which leaves what fell on it; that is smoothed, and multiplied by the
/// color again.  So a pattern, a picture or a grain in the wood stays as sharp as it was: only the grain the
/// sampling left in the light is smoothed out of it.
/// </para>
/// <para>
/// **The smoothing is five passes of a five by five kernel whose taps spread twice as far each pass** -- one, two,
/// four, eight and sixteen pixels apart -- so it reaches thirty-two pixels either way for the cost of 125 taps.
/// Each tap is weighed by how alike the two pixels are: their surfaces' facings (a power of 128 of the cosine
/// between them); how far the tap's place stands off the pixel's surface, against how far apart they lie across it
/// (so a near thing never mixes with a far one behind it, and a floor seen at a slant is still one floor); how far
/// apart their colors are, as a share of them (light over color is not all light that fell: what a surface mirrors
/// is not its color's, so a slab and its mortar are kept apart); and how alike their light is, against how much
/// grain there is to smooth there.  The last is what keeps a shadow's edge: two pixels a few grains apart are
/// mixed, two much further apart are not.
/// </para>
/// <para>
/// **How much grain there is, is measured from the picture**, since a render here is drawn once.  Each pixel's light
/// is set against the middle of the nine about it that show the same surface, taken in pairs either side of it --
/// which a grain stands out from, and neither an edge nor light rising evenly does -- and the squares of those
/// differences averaged over the seven by seven about it that show the same surface.  (SVGF's own first frame
/// averages the light's spread over the seven by seven instead, which takes a shadow's edge for grain and blurs
/// it.)  On a surface that mirrors smoothly the largest few are left out, so that the lamps it mirrors are not
/// taken for grain; on anything else a lone bright point can only be grain, and counts.  Each pass shrinks the
/// grain as it smooths, and the measure is carried along with it.
/// </para>
/// </summary>
internal static class WaveletFilter
{
    // The kernel's weights, middle out: one sixteenth, a quarter, three eighths.
    private static readonly double[] Kernel = [3.0 / 8, 1.0 / 4, 1.0 / 16];

    private const int Passes = 5;
    private const int FacingPower = 128;
    private const double LightSpread = 4;
    private const double PlaceSpread = 1;

    // A color darker than this is divided by this instead: what little light a near-black surface shows is mostly
    // what it mirrors, and divided by its color it would be grain made huge.
    private const float LeastAlbedo = 0.05f;

    // A pixel more than half sky is left as it is: the sky holds no grain to smooth.
    private const float LeastMet = 0.5f;

    // On a surface that mirrors smoothly, the share of the squared differences, the largest, left out of the measure
    // of grain, and for grain spread evenly what the average of the rest is of its spread.  A few bright points there
    // -- the lamps a wet road mirrors -- are not grain, and averaged in, they were taken for it and smoothed away;
    // but the middle of them all took grain that is a sprinkling of specks, one pixel in five, for no grain at all
    // and left every speck.  On anything else a lone bright point can only be grain -- a bounce that happened on a
    // patch of sun -- and every difference is averaged.
    private const double Brightest = 0.06;
    private const double TrimmedShare = 0.665;
    private const double LeastMirroring = 0.01;

    // For grain spread evenly, the squared difference from the middle of nine is this share of its spread.
    private const double MiddleOfNineShare = 0.945;

    // How alike two pixels' surfaces must be to be measured together.
    private const double SameSurface = 0.25;

    // How far apart two pixels' colors may be, as a share of their own, before they are not smoothed together.
    private const double AlbedoSpread = 0.25;

    /// <summary>
    /// This method smooths the grain out of a picture's light, guided by what it shows.
    /// </summary>
    /// <param name="canvas">The picture, holding light, which is smoothed in place; its coverage is left alone.</param>
    /// <param name="guides">What each of its pixels shows.</param>
    /// <param name="scanner">What to go over its pixels with.</param>
    internal static void Apply(Canvas canvas, Guides guides, IScanner scanner)
    {
        int width = canvas.Width;
        int height = canvas.Height;
        int count = width * height;
        float[] light = new float[count * 3];
        float[] albedo = new float[count * 3];
        float[] facing = new float[count * 3];
        float[] brightness = new float[count];
        float[] grain = new float[count];

        // Light over color, and the unit facing of each pixel's surfaces.
        for (int at = 0; at < count; at++)
        {
            Color color = canvas.GetPixel(at % width, at / width);
            double length = Math.Sqrt(
                guides.Normal[at * 3] * guides.Normal[at * 3] + guides.Normal[at * 3 + 1] * guides.Normal[at * 3 + 1] +
                guides.Normal[at * 3 + 2] * guides.Normal[at * 3 + 2]);

            for (int channel = 0; channel < 3; channel++)
            {
                albedo[at * 3 + channel] = Math.Max(guides.Albedo[at * 3 + channel], LeastAlbedo);
                double value = channel == 0 ? color.Red : channel == 1 ? color.Green : color.Blue;

                light[at * 3 + channel] = (float) (value / albedo[at * 3 + channel]);
                facing[at * 3 + channel] = length > 1e-6 ? (float) (guides.Normal[at * 3 + channel] / length) : 0;
            }

            brightness[at] = Brightness(light, at);
        }

        Neighbors neighbors = new (width, height, guides, facing);

        MeasureTheGrain(brightness, grain, neighbors, scanner);

        float[] smoothed = new float[count * 3];
        float[] carried = new float[count];

        for (int pass = 0; pass < Passes; pass++)
        {
            int step = 1 << pass;
            float[] from = light;
            float[] into = smoothed;
            float[] grainFrom = grain;
            float[] grainInto = carried;

            scanner.Scan(width, height, (x, y) =>
                Smooth(x, y, step, from, into, grainFrom, grainInto, neighbors));

            (light, smoothed) = (smoothed, light);
            (grain, carried) = (carried, grain);
        }

        for (int at = 0; at < count; at++)
        {
            if (guides.Met[at] < LeastMet)
                continue;

            int x = at % width;
            int y = at / width;
            Color color = canvas.GetPixel(x, y);

            canvas.SetColor(new Color(
                light[at * 3] * albedo[at * 3], light[at * 3 + 1] * albedo[at * 3 + 1],
                light[at * 3 + 2] * albedo[at * 3 + 2], color.Alpha), x, y);
        }
    }

    /// <summary>
    /// This method measures how much grain each pixel's light holds: its difference from the middle of the nine about
    /// it, squared, and those squares averaged over the seven by seven about it that show the same surface, the largest
    /// few left out.
    /// </summary>
    private static void MeasureTheGrain(float[] brightness, float[] grain, Neighbors neighbors, IScanner scanner)
    {
        int width = neighbors.Width;
        int height = neighbors.Height;
        float[] squared = new float[width * height];

        // Only the nine that show the same surface count -- an edge between two things is not grain -- and only in
        // pairs either side of the middle, so that light rising evenly across a surface has its middle at the middle
        // even at the picture's edge, or the sky's.
        scanner.Scan(width, height, (x, y) =>
        {
            int at = y * width + x;
            Span<float> nine = stackalloc float[9];
            int found = 0;

            nine[found++] = brightness[at];

            for (int dy = -1; dy <= 0; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dy == 0 && dx >= 0)
                        break;

                    int one = Within(x + dx, y + dy);
                    int other = Within(x - dx, y - dy);

                    if (one >= 0 && other >= 0 && neighbors.Alike(at, one, 1) >= SameSurface &&
                        neighbors.Alike(at, other, 1) >= SameSurface)
                    {
                        nine[found++] = brightness[one];
                        nine[found++] = brightness[other];
                    }
                }
            }

            nine = nine[..found];
            nine.Sort();

            float off = brightness[at] - nine[found / 2];

            squared[at] = off * off;
        });

        int Within(int x, int y) => x >= 0 && y >= 0 && x < width && y < height ? y * width + x : -1;

        scanner.Scan(width, height, (x, y) =>
        {
            int at = y * width + x;
            Span<float> about = stackalloc float[49];
            int found = 0;

            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx >= 0 && ny >= 0 && nx < width && ny < height &&
                        neighbors.Alike(at, ny * width + nx, 1) >= SameSurface)
                    {
                        about[found++] = squared[ny * width + nx];
                    }
                }
            }

            about = about[..found];
            about.Sort();

            int kept = neighbors.Guides.Mirrors[at] >= LeastMirroring
                ? found - (int) Math.Ceiling(found * Brightest)
                : found;
            double sum = 0;

            for (int index = 0; index < kept; index++)
                sum += about[index];

            grain[at] = kept <= 0 ? 0 : (float) (sum / kept / (kept < found ? TrimmedShare : MiddleOfNineShare));
        });
    }

    /// <summary>
    /// This method smooths one pixel's light by one pass of the kernel, its taps the given step apart, and carries
    /// the grain left in it along.
    /// </summary>
    private static void Smooth(
        int x, int y, int step, float[] from, float[] into, float[] grainFrom, float[] grainInto, Neighbors neighbors)
    {
        int width = neighbors.Width;
        int height = neighbors.Height;
        int at = y * width + x;

        if (neighbors.Guides.Met[at] < LeastMet)
        {
            into[at * 3] = from[at * 3];
            into[at * 3 + 1] = from[at * 3 + 1];
            into[at * 3 + 2] = from[at * 3 + 2];
            grainInto[at] = grainFrom[at];

            return;
        }

        double brightness = Brightness(from, at);
        double spread = LightSpread * Math.Sqrt(NearbyGrain(x, y, grainFrom, width, height)) + 1e-10;
        double red = 0, green = 0, blue = 0, weights = 0, grain = 0;

        for (int dy = -2; dy <= 2; dy++)
        {
            int ny = y + dy * step;

            if (ny < 0 || ny >= height)
                continue;

            for (int dx = -2; dx <= 2; dx++)
            {
                int nx = x + dx * step;

                if (nx < 0 || nx >= width)
                    continue;

                int near = ny * width + nx;
                double weight = Kernel[Math.Abs(dx)] * Kernel[Math.Abs(dy)];

                if (near != at)
                {
                    if (neighbors.Guides.Met[near] < LeastMet)
                        continue;

                    weight *= neighbors.Alike(at, near, step * Math.Sqrt(dx * dx + dy * dy)) *
                              Math.Exp(-Math.Abs(brightness - Brightness(from, near)) / spread);
                }

                red += from[near * 3] * weight;
                green += from[near * 3 + 1] * weight;
                blue += from[near * 3 + 2] * weight;
                grain += grainFrom[near] * weight * weight;
                weights += weight;
            }
        }

        into[at * 3] = (float) (red / weights);
        into[at * 3 + 1] = (float) (green / weights);
        into[at * 3 + 2] = (float) (blue / weights);
        grainInto[at] = (float) (grain / (weights * weights));
    }

    /// <summary>
    /// This method returns the grain about a pixel, blurred over the three by three about it, as SVGF reads it.
    /// </summary>
    private static double NearbyGrain(int x, int y, float[] grain, int width, int height)
    {
        double sum = 0;
        double weights = 0;

        for (int dy = -1; dy <= 1; dy++)
        {
            int ny = y + dy;

            if (ny < 0 || ny >= height)
                continue;

            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;

                if (nx < 0 || nx >= width)
                    continue;

                double weight = (dx == 0 ? 0.5 : 0.25) * (dy == 0 ? 0.5 : 0.25);

                sum += grain[ny * width + nx] * weight;
                weights += weight;
            }
        }

        return sum / weights;
    }

    /// <summary>
    /// This method returns how bright a pixel's light is.
    /// </summary>
    private static float Brightness(float[] light, int at)
    {
        return 0.2126f * light[at * 3] + 0.7152f * light[at * 3 + 1] + 0.0722f * light[at * 3 + 2];
    }

    /// <summary>
    /// This class says how alike two pixels' surfaces are by their guides: by their facing and by how far one's place
    /// stands off the other's surface.
    /// </summary>
    private sealed class Neighbors(int width, int height, Guides guides, float[] facing)
    {
        internal int Width => width;
        internal int Height => height;
        internal Guides Guides => guides;

        /// <summary>
        /// This method returns how alike two pixels' surfaces are, from nought to one.
        /// </summary>
        /// <param name="at">The pixel being smoothed.</param>
        /// <param name="near">The pixel it may take light from.</param>
        /// <param name="apart">How many pixels apart they lie.</param>
        internal double Alike(int at, int near, double apart)
        {
            if (at == near)
                return 1;

            double cos = facing[at * 3] * facing[near * 3] + facing[at * 3 + 1] * facing[near * 3 + 1] +
                         facing[at * 3 + 2] * facing[near * 3 + 2];

            if (cos <= 0)
                return 0;

            double facingWeight = cos;

            for (int square = 1; square < FacingPower; square *= 2)
                facingWeight *= facingWeight;

            double[] place = guides.Place;
            double off = Math.Abs(
                facing[at * 3] * (place[near * 3] - place[at * 3]) +
                facing[at * 3 + 1] * (place[near * 3 + 1] - place[at * 3 + 1]) +
                facing[at * 3 + 2] * (place[near * 3 + 2] - place[at * 3 + 2]));
            double allowed = PlaceSpread * 2 * guides.Footprint[at] * apart + 1e-12;
            float[] albedo = guides.Albedo;
            double apartInColor = 0;

            // Light divided by a color is not all light that fell on it: what a surface mirrors is not its color's.
            // So two pixels of colors far apart -- a slab and its mortar -- are not smoothed together, or what the
            // paving mirrors is shared out between them wrongly.
            for (int channel = 0; channel < 3; channel++)
            {
                double mine = albedo[at * 3 + channel];
                double theirs = albedo[near * 3 + channel];

                apartInColor = Math.Max(apartInColor, Math.Abs(mine - theirs) / ((mine + theirs) / 2 + 0.02));
            }

            return facingWeight * Math.Exp(-off / allowed - apartInColor / AlbedoSpread);
        }
    }
}
