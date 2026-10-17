namespace RayTracer.Graphics;

/// <summary>
/// This enumeration lists what a texture is taken to hold beyond its own edges.
/// </summary>
public enum TextureWrap
{
    /// <summary>
    /// The image tiles, so what lies past one edge is what lies inside the other.
    /// </summary>
    Repeat,

    /// <summary>
    /// The edge goes on forever, as a world map's poles do.
    /// </summary>
    Clamp,

    /// <summary>
    /// There is nothing past the edge: an image drawn once, with clear space round it.
    /// </summary>
    Border
}

/// <summary>
/// This class represents an image made ready to be looked at from a surface: its colors turned back
/// into light, and a pyramid of ever smaller copies of it, each half the size of the one before, so that
/// a patch of it can be averaged at any size for the cost of a few texels.
/// <para>
/// **A pixel sees a patch of an image, not a point of it.**  Looked up at the one point a ray strikes, a
/// checkered floor running off to the horizon beats into moiré, because neighboring pixels pick out
/// squares that have nothing to do with the squares between them.  What a pixel owes is the image
/// averaged over the patch its footprint covers, and the footprint of a floor seen at a glance is a long
/// thin ellipse rather than a disc.  So each lookup is the elliptically weighted average PBRT uses
/// (Heckbert's EWA): a Gaussian over the ellipse, read from the copy in which the ellipse's short axis is
/// a texel or two across, and blended with the next copy down so that nothing jumps as the ellipse
/// grows.  Asked about a point with no footprint at all, the image is read between its four nearest
/// texels rather than at the nearest one, so that one seen close up is smooth rather than blocky.
/// </para>
/// <para>
/// **The averaging is of light, and of light weighted by how opaque it is.**  Averaging the values a file
/// holds would average sRGB numbers, which darkens every edge between a light color and a dark one; and
/// averaging colors without their alpha lets the black of a clear texel bleed into the edge of a decal.
/// Each texel therefore holds its light already multiplied by its alpha, and the alpha is divided back
/// out once the average is taken.
/// </para>
/// </summary>
public sealed class Texture
{
    /// <summary>
    /// The most an ellipse may be longer than it is wide.  A floor at a glance asks for more, and is
    /// blurred a little across where it should not be, rather than averaged over a needle a few
    /// thousand texels long.
    /// </summary>
    private const double MaxAnisotropy = 16;

    /// <summary>
    /// How sharply the Gaussian falls away over the ellipse: PBRT's two.
    /// </summary>
    private const double Falloff = 2;

    private const int WeightTableSize = 128;

    private static readonly double[] Weights = BuildWeights();

    /// <summary>
    /// How much of a footprint's edge each of the ellipse's half-axes is.
    /// <para>
    /// A footprint's edges are a pixel's sides, laid on the surface, and what a pixel owes is the image
    /// averaged evenly over that patch: a box, whose spread along each side is a twelfth of the side
    /// squared.  The Gaussian, cut off at its ellipse's edge, spreads over a smaller share of its
    /// half-axis squared, worked out below; the half-axes are scaled so the two spreads agree.  PBRT
    /// takes each half-axis as a whole side, which is a third wider than the box, and measured against a
    /// floor supersampled sixteen times each way that blurred the checks into one another -- an error of
    /// 22.9 levels to this 18.5, and 3.7 to 2.5 at the horizon, where the footprint is all that counts.
    /// </para>
    /// </summary>
    private static readonly double AxisScale = Math.Sqrt(1.0 / 12 / SpreadOfTheWeights());

    /// <summary>
    /// This property holds the width of the image, in texels.
    /// </summary>
    public int Width => _levels[0].Width;

    /// <summary>
    /// This property holds the height of the image, in texels.
    /// </summary>
    public int Height => _levels[0].Height;

    /// <summary>
    /// This property holds how many copies of the image the pyramid holds, the image itself included.
    /// </summary>
    public int LevelCount => _levels.Length;

    /// <summary>
    /// This property reports whether every texel of the image is opaque, so that nothing read from it
    /// can be anything else.
    /// </summary>
    public bool IsOpaque { get; }

    private readonly Level[] _levels;

    private Texture(Level[] levels, bool isOpaque)
    {
        _levels = levels;
        IsOpaque = isOpaque;
    }

    /// <summary>
    /// This method makes a texture from a canvas, turning its colors into light by the canvas's own
    /// encoding, or taking them as they stand when asked to.
    /// </summary>
    /// <param name="canvas">The canvas to make the texture from.</param>
    /// <param name="asLight">Whether to turn the canvas's colors into light.  A scene that writes no
    /// gamma takes its colors as the values that reach the file, and an image in it is taken the same
    /// way.</param>
    /// <returns>The texture.</returns>
    public static Texture From(Canvas canvas, bool asLight = true)
    {
        TransferFunction encoding = asLight ? canvas.Encoding : TransferFunction.Linear;
        float[] texels = new float[canvas.Width * canvas.Height * 4];
        bool isOpaque = true;
        int index = 0;

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                Color color = canvas.GetPixel(x, y);
                double alpha = Math.Clamp(color.Alpha, 0, 1);

                texels[index++] = (float) (encoding.ToLinear(color.Red) * alpha);
                texels[index++] = (float) (encoding.ToLinear(color.Green) * alpha);
                texels[index++] = (float) (encoding.ToLinear(color.Blue) * alpha);
                texels[index++] = (float) alpha;

                if (alpha < 1)
                    isOpaque = false;
            }
        }

        List<Level> levels = [new Level(canvas.Width, canvas.Height, texels)];

        while (levels[^1].Width > 1 || levels[^1].Height > 1)
            levels.Add(levels[^1].Reduced());

        return new Texture(levels.ToArray(), isOpaque);
    }

    /// <summary>
    /// This method reads the image at a point, between the four texels nearest it.
    /// </summary>
    /// <param name="u">How far across the image, in texels; a texel's middle is half a texel in.</param>
    /// <param name="v">How far down the image, in texels.</param>
    /// <param name="wrapU">What lies beyond the image's sides.</param>
    /// <param name="wrapV">What lies beyond its top and bottom.</param>
    /// <returns>The color there.</returns>
    public Color Bilinear(double u, double v, TextureWrap wrapU, TextureWrap wrapV)
    {
        Level level = _levels[0];
        Sum sum = default;

        u = Settle(u, level.Width, wrapU);
        v = Settle(v, level.Height, wrapV);

        double x = u - 0.5, y = v - 0.5;
        int x0 = (int) Math.Floor(x), y0 = (int) Math.Floor(y);
        double fx = x - x0, fy = y - y0;

        level.Add(ref sum, x0, y0, (1 - fx) * (1 - fy), wrapU, wrapV);
        level.Add(ref sum, x0 + 1, y0, fx * (1 - fy), wrapU, wrapV);
        level.Add(ref sum, x0, y0 + 1, (1 - fx) * fy, wrapU, wrapV);
        level.Add(ref sum, x0 + 1, y0 + 1, fx * fy, wrapU, wrapV);

        return Resolve(sum, wrapU, wrapV);
    }

    /// <summary>
    /// This method averages the image over an ellipse round a point.
    /// </summary>
    /// <param name="u">How far across the image the ellipse's middle lies, in texels.</param>
    /// <param name="v">How far down the image it lies, in texels.</param>
    /// <param name="du0">How far across the image one of the ellipse's axes reaches, in texels.</param>
    /// <param name="dv0">How far down the image the same axis reaches.</param>
    /// <param name="du1">How far across the image the other axis reaches.</param>
    /// <param name="dv1">How far down the image the other axis reaches.</param>
    /// <param name="wrapU">What lies beyond the image's sides.</param>
    /// <param name="wrapV">What lies beyond its top and bottom.</param>
    /// <returns>The average color over the ellipse.</returns>
    public Color Filtered(
        double u, double v, double du0, double dv0, double du1, double dv1,
        TextureWrap wrapU, TextureWrap wrapV)
    {
        du0 *= AxisScale;
        dv0 *= AxisScale;
        du1 *= AxisScale;
        dv1 *= AxisScale;

        double major2 = du0 * du0 + dv0 * dv0;
        double minor2 = du1 * du1 + dv1 * dv1;

        if (major2 < minor2)
        {
            (du0, dv0, du1, dv1) = (du1, dv1, du0, dv0);
            (major2, minor2) = (minor2, major2);
        }

        if (!(major2 > 0) || double.IsInfinity(major2))
            return Bilinear(u, v, wrapU, wrapV);

        double major = Math.Sqrt(major2);
        double minor = Math.Sqrt(minor2);

        // An ellipse too thin to be worth reading along is widened.  One with no width at all -- the
        // patch seen edge-on to the mapping, as a planar map is on an upright wall -- is given a width
        // square to its length, since a line has no direction across it to widen along.
        if (minor * MaxAnisotropy < major)
        {
            double wanted = major / MaxAnisotropy;

            if (minor > 0)
            {
                du1 *= wanted / minor;
                dv1 *= wanted / minor;
            }
            else
            {
                du1 = -dv0 * (wanted / major);
                dv1 = du0 * (wanted / major);
            }

            minor = wanted;
        }

        u = Settle(u, Width, wrapU);
        v = Settle(v, Height, wrapV);

        // The copy to read is the one in which the short axis is a texel across, so that the Gaussian
        // has a texel or two to work with across the ellipse and as many as it needs along it.
        double lod = Math.Max(0, Math.Log2(minor));

        if (lod >= _levels.Length - 1)
            return Resolve(_levels[^1].Average(wrapU, wrapV), wrapU, wrapV);

        int fine = (int) Math.Floor(lod);
        double share = lod - fine;
        Sum sum = Ewa(_levels[fine], u, v, du0, dv0, du1, dv1, wrapU, wrapV);

        if (share > 0)
            sum = sum.Blend(Ewa(_levels[fine + 1], u, v, du0, dv0, du1, dv1, wrapU, wrapV), share);

        return Resolve(sum, wrapU, wrapV);
    }

    /// <summary>
    /// This method averages one copy of the image over an ellipse, by a Gaussian over it.
    /// </summary>
    private Sum Ewa(
        Level level, double u, double v, double du0, double dv0, double du1, double dv1,
        TextureWrap wrapU, TextureWrap wrapV)
    {
        double scaleU = (double) level.Width / Width;
        double scaleV = (double) level.Height / Height;
        double s = u * scaleU - 0.5;
        double t = v * scaleV - 0.5;

        du0 *= scaleU;
        du1 *= scaleU;
        dv0 *= scaleV;
        dv1 *= scaleV;

        // The ellipse as a quadratic, r² = A s² + B s t + C t², which is one at its edge.  The ones
        // added to A and C widen it by a texel all round, so that however small it is it never falls
        // between texels.
        double a = dv0 * dv0 + dv1 * dv1 + 1;
        double b = -2 * (du0 * dv0 + du1 * dv1);
        double c = du0 * du0 + du1 * du1 + 1;
        double scale = 1 / (a * c - b * b * 0.25);

        a *= scale;
        b *= scale;
        c *= scale;

        double det = 4 * a * c - b * b;
        double reachT = 2 * Math.Sqrt(a * det) / det;
        int t0 = (int) Math.Ceiling(t - reachT);
        int t1 = (int) Math.Floor(t + reachT);
        Sum sum = default;
        double total = 0;

        for (int it = t0; it <= t1; it++)
        {
            double tt = it - t;

            // Only the texels inside the ellipse along this row are visited: where the quadratic in s
            // falls below one.
            double half = b * tt;
            double room = half * half - 4 * a * (c * tt * tt - 1);

            if (room < 0)
                continue;

            double root = Math.Sqrt(room);
            int s0 = (int) Math.Ceiling(s + (-half - root) / (2 * a));
            int s1 = (int) Math.Floor(s + (-half + root) / (2 * a));

            for (int @is = s0; @is <= s1; @is++)
            {
                double ss = @is - s;
                double r2 = a * ss * ss + b * ss * tt + c * tt * tt;

                if (r2 >= 1)
                    continue;

                double weight = Weights[Math.Min((int) (r2 * WeightTableSize), WeightTableSize - 1)];

                level.Add(ref sum, @is, it, weight, wrapU, wrapV);
                total += weight;
            }
        }

        // The ellipse is never so small as to hold no texel's middle, since it is at least a texel
        // across; but if rounding ever leaves it empty, the nearest texels stand in.
        return total > 0 ? sum.Scaled(1 / total) : level.Bilinear(s + 0.5, t + 0.5, wrapU, wrapV);
    }

    /// <summary>
    /// This method brings a coordinate that may lie far outside the image back near it, where whole
    /// numbers of texels can count it without overflowing: a tiled image is wrapped into its first
    /// tile, and anything else is held within reach of its edges.
    /// </summary>
    private static double Settle(double value, int size, TextureWrap wrap)
    {
        if (wrap == TextureWrap.Repeat)
            return value - size * Math.Floor(value / size);

        return Math.Clamp(value, -2.0 * size - 2, 3.0 * size + 2);
    }

    /// <summary>
    /// This method turns an average of light weighted by opacity back into a color.  An opaque image
    /// read where no border can reach is opaque, exactly, whatever rounding the average carries.
    /// </summary>
    private Color Resolve(Sum sum, TextureWrap wrapU, TextureWrap wrapV)
    {
        if (IsOpaque && wrapU != TextureWrap.Border && wrapV != TextureWrap.Border)
            return new Color(sum.Red / sum.Alpha, sum.Green / sum.Alpha, sum.Blue / sum.Alpha);

        if (sum.Alpha <= 1e-9)
            return Colors.Transparent;

        double alpha = sum.Alpha >= 1 - 1e-6 ? 1 : sum.Alpha;

        return new Color(sum.Red / sum.Alpha, sum.Green / sum.Alpha, sum.Blue / sum.Alpha, alpha);
    }

    /// <summary>
    /// This method works out how far the weights spread along one axis of an ellipse with half-axes of
    /// one: the variance of exp(-a r²) - exp(-a) over the unit disc, taken along a line through it.
    /// </summary>
    private static double SpreadOfTheWeights()
    {
        double floor = Math.Exp(-Falloff);

        // The integrals over r of r w(r) and r³ w(r), from nought to one, in closed form.
        double first = (1 - floor) / (2 * Falloff) - floor / 2;
        double third = (1 - (1 + Falloff) * floor) / (2 * Falloff * Falloff) - floor / 4;

        return third / first / 2;
    }

    private static double[] BuildWeights()
    {
        double[] weights = new double[WeightTableSize];
        double floor = Math.Exp(-Falloff);

        for (int index = 0; index < WeightTableSize; index++)
        {
            double r2 = (double) index / (WeightTableSize - 1);

            weights[index] = Math.Exp(-Falloff * r2) - floor;
        }

        return weights;
    }

    /// <summary>
    /// This structure holds a running weighted sum of texels.
    /// </summary>
    private struct Sum
    {
        public double Red;
        public double Green;
        public double Blue;
        public double Alpha;

        public readonly Sum Scaled(double factor)
        {
            return new Sum
            {
                Red = Red * factor, Green = Green * factor, Blue = Blue * factor, Alpha = Alpha * factor
            };
        }

        public readonly Sum Blend(Sum other, double share)
        {
            return new Sum
            {
                Red = Red + (other.Red - Red) * share,
                Green = Green + (other.Green - Green) * share,
                Blue = Blue + (other.Blue - Blue) * share,
                Alpha = Alpha + (other.Alpha - Alpha) * share
            };
        }
    }

    /// <summary>
    /// This class holds one copy of the image in the pyramid: its texels as light multiplied by alpha,
    /// four floats apiece.
    /// </summary>
    private sealed class Level
    {
        public readonly int Width;
        public readonly int Height;

        private readonly float[] _texels;

        public Level(int width, int height, float[] texels)
        {
            Width = width;
            Height = height;

            _texels = texels;
        }

        /// <summary>
        /// This method adds one texel to a running sum, by the given weight.  A texel past a border is
        /// nothing, and adds nothing but its weight.
        /// </summary>
        public void Add(ref Sum sum, int x, int y, double weight, TextureWrap wrapU, TextureWrap wrapV)
        {
            if (!Place(ref x, Width, wrapU) || !Place(ref y, Height, wrapV))
                return;

            int index = (y * Width + x) * 4;

            sum.Red += _texels[index] * weight;
            sum.Green += _texels[index + 1] * weight;
            sum.Blue += _texels[index + 2] * weight;
            sum.Alpha += _texels[index + 3] * weight;
        }

        /// <summary>
        /// This method reads this copy between the four texels nearest a point, for the rare ellipse
        /// that rounding leaves holding none.
        /// </summary>
        public Sum Bilinear(double u, double v, TextureWrap wrapU, TextureWrap wrapV)
        {
            double x = u - 0.5, y = v - 0.5;
            int x0 = (int) Math.Floor(x), y0 = (int) Math.Floor(y);
            double fx = x - x0, fy = y - y0;
            Sum sum = default;

            Add(ref sum, x0, y0, (1 - fx) * (1 - fy), wrapU, wrapV);
            Add(ref sum, x0 + 1, y0, fx * (1 - fy), wrapU, wrapV);
            Add(ref sum, x0, y0 + 1, (1 - fx) * fy, wrapU, wrapV);
            Add(ref sum, x0 + 1, y0 + 1, fx * fy, wrapU, wrapV);

            return sum;
        }

        /// <summary>
        /// This method returns the average of the whole of this copy.
        /// </summary>
        public Sum Average(TextureWrap wrapU, TextureWrap wrapV)
        {
            Sum sum = default;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                    Add(ref sum, x, y, 1, wrapU, wrapV);
            }

            return sum.Scaled(1.0 / (Width * Height));
        }

        /// <summary>
        /// This method makes the next copy down the pyramid, half this one's size each way, rounded up.
        /// Each of its texels is the exact average of the patch of this one it covers, which for an odd
        /// size takes in parts of texels.
        /// </summary>
        public Level Reduced()
        {
            int width = Math.Max(1, (Width + 1) / 2);
            int height = Math.Max(1, (Height + 1) / 2);
            float[] across = new float[width * Height * 4];
            float[] down = new float[width * height * 4];

            for (int y = 0; y < Height; y++)
                Resample(_texels, y * Width * 4, 4, Width, across, y * width * 4, 4, width);

            for (int x = 0; x < width; x++)
                Resample(across, x * 4, width * 4, Height, down, x * 4, width * 4, height);

            return new Level(width, height, down);
        }

        /// <summary>
        /// This method resamples one line of texels to a shorter length, each new texel the average of
        /// the stretch of old ones it covers.
        /// </summary>
        private static void Resample(
            float[] from, int fromStart, int fromStep, int fromCount,
            float[] to, int toStart, int toStep, int toCount)
        {
            double span = (double) fromCount / toCount;

            for (int j = 0; j < toCount; j++)
            {
                double start = j * span, end = (j + 1) * span;
                int first = (int) Math.Floor(start);
                int last = Math.Min(fromCount - 1, (int) Math.Ceiling(end) - 1);
                double red = 0, green = 0, blue = 0, alpha = 0;

                for (int i = first; i <= last; i++)
                {
                    double overlap = Math.Min(end, i + 1) - Math.Max(start, i);
                    int index = fromStart + i * fromStep;

                    red += from[index] * overlap;
                    green += from[index + 1] * overlap;
                    blue += from[index + 2] * overlap;
                    alpha += from[index + 3] * overlap;
                }

                int target = toStart + j * toStep;

                to[target] = (float) (red / span);
                to[target + 1] = (float) (green / span);
                to[target + 2] = (float) (blue / span);
                to[target + 3] = (float) (alpha / span);
            }
        }

        /// <summary>
        /// This method brings a texel's place inside this copy by the wrap asked for, reporting false
        /// for one past a border.
        /// </summary>
        private static bool Place(ref int value, int size, TextureWrap wrap)
        {
            if (value >= 0 && value < size)
                return true;

            switch (wrap)
            {
                case TextureWrap.Repeat:
                    value %= size;

                    if (value < 0)
                        value += size;

                    return true;
                case TextureWrap.Clamp:
                    value = Math.Clamp(value, 0, size - 1);

                    return true;
                default:
                    return false;
            }
        }
    }
}
