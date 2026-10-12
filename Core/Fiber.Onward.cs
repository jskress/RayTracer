using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This part of a fiber says how much of the light reaching a strand from one direction it sends onward
/// -- into the half of the circle round it beyond the strand -- and how much back, averaged across the
/// strand: what a shadow ray through hair keeps of the light, for a surface beneath it.
/// </summary>
public sealed partial class Fiber
{
    // The light's angle out of the plane square to the strand, and how clear the strand is, at which the
    // shares are worked out ahead of time; and how many places across the strand each is averaged over.
    private const int ShareAngles = 17;
    private const int ShareClarities = 33;
    private const int Offsets = 64;

    private readonly Lazy<ShareTable> _shares;

    /// <summary>
    /// This method returns how much of the light reaching a strand from one direction it sends onward and
    /// how much back, per band, averaged across the strand.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="sinTheta">The sine of the light's angle out of the plane square to the strand.</param>
    /// <param name="absorption">How much the strand's inside absorbs, per band, over its own radius.</param>
    /// <returns>The shares sent onward and back.</returns>
    public (TS Onward, TS Back) SharesOf<TS>(double sinTheta, TS absorption)
        where TS : struct, ISpectrum<TS>
    {
        ShareTable table = _shares.Value;
        TS onward = TS.Black;
        TS back = TS.Black;

        for (int band = 0; band < TS.Count; band++)
            (onward[band], back[band]) = table.Look(sinTheta, absorption[band]);

        return (onward, back);
    }

    /// <summary>
    /// This method returns how much of a lobe round the strand lies below the given angle, counting every
    /// whole turn below it as one: the running total of <see cref="Azimuthal"/>, so that the share of it
    /// between two angles is the difference of this at each, however far round they lie.
    /// </summary>
    private double Wound(double phi, int lobe, double gammaO, double gammaT)
    {
        double away = phi - (2 * lobe * gammaT - 2 * gammaO + lobe * Math.PI);
        double turns = Math.Floor((away + Math.PI) / (2 * Math.PI));
        double within = away - turns * 2 * Math.PI;
        double low = LogisticCdf(-Math.PI, _spread);

        return turns + (LogisticCdf(within, _spread) - low) / (LogisticCdf(Math.PI, _spread) - low);
    }

    /// <summary>
    /// This class holds how much a fiber of one roughness sends onward and back, worked out ahead of time
    /// over the light's angle out of the plane square to the strand and over how clear the strand is.
    /// </summary>
    private sealed class ShareTable
    {
        private readonly double[] _entries = new double[ShareAngles * ShareClarities * 2];

        public ShareTable(Fiber fiber)
        {
            Span<double> onward = stackalloc double[Lobes];

            for (int angle = 0; angle < ShareAngles; angle++)
            {
                double theta = Math.PI / 2 * angle / (ShareAngles - 1);
                double sinTheta = Math.Min(Math.Sin(theta), 0.9999);
                double cosTheta = Math.Sqrt(1 - sinTheta * sinTheta);

                for (int offset = 0; offset < Offsets; offset++)
                {
                    double h = -1 + 2 * (offset + 0.5) / Offsets;
                    (double gammaT, RgbSpectrum _) = Inside(sinTheta, cosTheta, h, RgbSpectrum.Black);
                    double gammaO = Math.Asin(h);
                    double sinThetaT = sinTheta / Index;
                    double length = 2 * Math.Cos(gammaT) / Math.Sqrt(1 - sinThetaT * sinThetaT);
                    double fresnel = Reflectance(cosTheta * Math.Sqrt(1 - h * h));

                    // How much of each lobe goes onward: the part of it more than a quarter turn round the
                    // strand from where the light came in.
                    for (int lobe = 0; lobe < Lobes; lobe++)
                    {
                        onward[lobe] = 1 - (fiber.Wound(Math.PI / 2, lobe, gammaO, gammaT) -
                                            fiber.Wound(-Math.PI / 2, lobe, gammaO, gammaT));
                    }

                    for (int clarity = 0; clarity < ShareClarities; clarity++)
                    {
                        double through = Math.Pow((double) clarity / (ShareClarities - 1), length);
                        double passed = (1 - fresnel) * (1 - fresnel) * through;
                        double inside = passed * through * fresnel;
                        double each = through * fresnel;
                        double rest = each < 1 ? inside * each / (1 - each) : 0;
                        ReadOnlySpan<double> lobes = [fresnel, passed, inside];
                        int at = (angle * ShareClarities + clarity) * 2;

                        for (int lobe = 0; lobe < Lobes; lobe++)
                        {
                            _entries[at] += lobes[lobe] * onward[lobe] / Offsets;
                            _entries[at + 1] += lobes[lobe] * (1 - onward[lobe]) / Offsets;
                        }

                        // What is left after the mirror inside is spread evenly round the strand.
                        _entries[at] += rest / 2 / Offsets;
                        _entries[at + 1] += rest / 2 / Offsets;
                    }
                }
            }
        }

        /// <summary>
        /// This method reads the table for light at the given angle reaching a strand that absorbs as given
        /// in one band, between the entries around it.
        /// </summary>
        public (double Onward, double Back) Look(double sinTheta, double absorption)
        {
            double angle = Math.Asin(Math.Min(Math.Abs(sinTheta), 1)) / (Math.PI / 2) * (ShareAngles - 1);
            double clarity = Math.Exp(-Math.Max(absorption, 0)) * (ShareClarities - 1);
            int a = Math.Min((int) angle, ShareAngles - 2);
            int c = Math.Min((int) clarity, ShareClarities - 2);
            double da = angle - a;
            double dc = clarity - c;

            return (Read(0), Read(1));

            double Read(int which)
            {
                double Entry(int i, int j) => _entries[((a + i) * ShareClarities + c + j) * 2 + which];

                return (1 - da) * ((1 - dc) * Entry(0, 0) + dc * Entry(0, 1)) +
                       da * ((1 - dc) * Entry(1, 0) + dc * Entry(1, 1));
            }
        }
    }
}
