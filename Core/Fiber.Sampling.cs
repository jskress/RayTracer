using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This part of a fiber picks directions for light to have arrived from in proportion to how much of it
/// the strand would send toward the eye, PBRT's way.  It is how the light scattered among the strands of
/// a head of hair is followed from strand to strand when it is gathered; see <see cref="HairLight"/>.
/// </summary>
public sealed partial class Fiber
{
    /// <summary>
    /// This method picks a direction for light to have arrived from, as the strand's scattering toward the
    /// eye favors it: a lobe in proportion to how much light it carries, a place along the strand from that
    /// lobe's spread along it, and a place round it from its spread round it.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="seen">What the scattering owes to the eye's direction.</param>
    /// <param name="pick">A number from nought to one, for which lobe.</param>
    /// <param name="along">Another, for how far out of the plane square to the strand.</param>
    /// <param name="about">Another, for which way about the lobe's center.</param>
    /// <param name="around">Another, for how far round the strand.</param>
    /// <returns>The sine of the direction's angle out of the plane square to the strand, its angle round
    /// it, and how likely that direction was to be picked, per unit of solid angle.</returns>
    internal (double SinThetaI, double PhiI, double Odds) Sample<TS>(
        in Seen<TS> seen, double pick, double along, double about, double around)
        where TS : struct, ISpectrum<TS>
    {
        Span<double> weights = stackalloc double[Lobes + 1];

        Weights(seen, weights);

        int lobe = 0;

        while (lobe < Lobes && pick >= weights[lobe])
            pick -= weights[lobe++];

        (double sinThetaOp, double cosThetaOp) = lobe < Lobes ? Tilted(seen, lobe) : (seen.SinThetaO, seen.CosThetaO);
        double variance = _variance[lobe];

        along = Math.Max(along, 1e-5);

        double cosTheta = 1 + variance * Math.Log(along + (1 - along) * Math.Exp(-2 / variance));
        double sinTheta = Math.Sqrt(Math.Max(0, 1 - cosTheta * cosTheta));
        double sinThetaI = Math.Clamp(
            -cosTheta * sinThetaOp + sinTheta * Math.Cos(2 * Math.PI * about) * cosThetaOp, -1, 1);
        double turn = lobe < Lobes
            ? 2 * lobe * seen.GammaT - 2 * seen.GammaO + lobe * Math.PI + TrimmedLogisticSample(around)
            : 2 * Math.PI * around;

        return (sinThetaI, seen.PhiO + turn, OddsOf(seen, sinThetaI, turn, weights));
    }

    /// <summary>
    /// This method returns how likely <see cref="Sample{TS}"/> is to pick a direction, per unit of solid
    /// angle.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="seen">What the scattering owes to the eye's direction.</param>
    /// <param name="sinThetaI">The sine of the direction's angle out of the plane square to the strand.</param>
    /// <param name="phiI">The direction's angle round the strand.</param>
    /// <returns>How likely it is to be picked.</returns>
    internal double OddsOf<TS>(in Seen<TS> seen, double sinThetaI, double phiI)
        where TS : struct, ISpectrum<TS>
    {
        Span<double> weights = stackalloc double[Lobes + 1];

        Weights(seen, weights);

        return OddsOf(seen, sinThetaI, phiI - seen.PhiO, weights);
    }

    private double OddsOf<TS>(in Seen<TS> seen, double sinThetaI, double turn, ReadOnlySpan<double> weights)
        where TS : struct, ISpectrum<TS>
    {
        double cosThetaI = Math.Sqrt(Math.Max(0, 1 - sinThetaI * sinThetaI));
        double odds = 0;

        for (int lobe = 0; lobe < Lobes; lobe++)
        {
            (double sinThetaOp, double cosThetaOp) = Tilted(seen, lobe);

            odds += weights[lobe] *
                    Longitudinal(cosThetaI, Math.Abs(cosThetaOp), sinThetaI, sinThetaOp, _variance[lobe]) *
                    Azimuthal(turn, lobe, seen.GammaO, seen.GammaT);
        }

        return odds + weights[Lobes] *
            Longitudinal(cosThetaI, seen.CosThetaO, sinThetaI, seen.SinThetaO, _variance[Lobes]) / (2 * Math.PI);
    }

    /// <summary>
    /// This method works out how likely each lobe is to be picked: in proportion to the light it carries,
    /// averaged across the bands.
    /// </summary>
    private static void Weights<TS>(in Seen<TS> seen, Span<double> weights)
        where TS : struct, ISpectrum<TS>
    {
        double total = 0;

        for (int lobe = 0; lobe <= Lobes; lobe++)
        {
            TS carried = lobe switch
            {
                0 => seen.Mirrored, 1 => seen.Through, 2 => seen.MirroredInside, _ => seen.Rest
            };
            double mean = 0;

            for (int band = 0; band < TS.Count; band++)
                mean += carried[band];

            weights[lobe] = mean / TS.Count;
            total += weights[lobe];
        }

        for (int lobe = 0; lobe <= Lobes; lobe++)
            weights[lobe] = total > 0 ? weights[lobe] / total : lobe == 0 ? 1 : 0;
    }

    /// <summary>
    /// This method picks an angle from the logistic round the strand, cut off at half a turn either way.
    /// </summary>
    private double TrimmedLogisticSample(double u)
    {
        double low = LogisticCdf(-Math.PI, _spread);
        double span = LogisticCdf(Math.PI, _spread) - low;
        double x = -_spread * Math.Log(1 / (u * span + low) - 1);

        return Math.Clamp(x, -Math.PI, Math.PI);
    }
}
