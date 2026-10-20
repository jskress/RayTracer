using System.Collections.Concurrent;
using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents the sheen of cloth: velvet, satin, felt, the nap of a rug -- a soft shine that comes
/// up toward a graze, where the fibers standing out of the weave catch the light edge-on.
/// <para>
/// **The fibers are facets that stand up rather than lie down.**  A rough surface's facets mostly face out,
/// and lean only a little; a cloth's fibers mostly stand across the surface, so the facets there face every
/// way along it, and the light they mirror is strongest where the surface is seen or lit edge-on.  That is
/// Estevez and Kulla's distribution (2017, "Charlie"), sin^(1/r) of the angle the facets lean, with Neubelt
/// and Pettineo's cheap shadowing (2013) -- the cloth model of Filament, Google's renderer.  A sheen has a
/// color of its own, what its fibers give back, which is not the cloth's: satin's sheen is pale over a deep
/// color, and velvet's often the cloth's own color, brightened.
/// </para>
/// <para>
/// **What the sheen gives back, the cloth beneath does not.**  The light the fibers turn back toward a graze
/// never reaches the weave, so what the material says lies beneath is shown less by as much -- by the
/// sheen's own share of light from the eye's direction or the lamp's, whichever is the greater, as glTF has
/// it.  That share is worked out once for each roughness by adding up the sheen over every way light comes.
/// </para>
/// </summary>
public sealed class Sheen
{
    /// <summary>
    /// This is how rough a sheen is when it says nothing: a soft velvet's.
    /// </summary>
    public const double DefaultRoughness = 0.5;

    // How finely the share the sheen gives back is known, across the cosine of the angle it is seen at.
    private const int ShareSteps = 32;

    /// <summary>
    /// This property holds the sheen's color: the share of each color its fibers give back.
    /// </summary>
    public Color Color { get; set; } = Colors.White;

    /// <summary>
    /// This property holds how rough the sheen is, from nearly nothing -- a satin's narrow shine right at
    /// the edge -- to one, a felt's broad glow.
    /// </summary>
    public double Roughness { get; set; } = DefaultRoughness;

    // Estevez and Kulla's r: the square of the roughness, held off nothing so that the power stays finite.
    private double Alpha => Math.Max(Roughness * Roughness, 1e-3);

    /// <summary>
    /// This method returns how densely the fibers' facets crowd at a given lean, per unit of solid angle: the
    /// Charlie distribution, which comes to one weighted by how squarely each faces out.
    /// </summary>
    /// <param name="cosLean">The cosine of the angle between a facet's normal and the surface's.</param>
    /// <returns>The density.</returns>
    public double Distribution(double cosLean)
    {
        if (cosLean <= 0)
            return 0;

        double inverse = 1 / Alpha;
        double sin2 = Math.Max(1 - cosLean * cosLean, 0.0078125);

        return (2 + inverse) * Math.Pow(sin2, inverse * 0.5) / (2 * Math.PI);
    }

    /// <summary>
    /// This method returns Neubelt and Pettineo's shadowing for cloth, with the BRDF's own 4 n.l n.v folded
    /// in: 1 / 4(n.l + n.v - n.l n.v).
    /// </summary>
    public static double Visibility(double cosEye, double cosLight) =>
        1 / (4 * (cosLight + cosEye - cosLight * cosEye));

    /// <summary>
    /// This method returns how brightly a lamp shows in the sheen, for a light of unit color and a sheen of
    /// unit color: the fibers' share of the lamp's light, times the cosine it arrives at, times pi for the
    /// reason <see cref="Microfacets.Highlight"/> gives.
    /// </summary>
    public double Highlight(Vector normal, Vector eye, Vector light)
    {
        double cosEye = normal.Dot(eye);
        double cosLight = normal.Dot(light);

        if (cosEye <= 0 || cosLight <= 0)
            return 0;

        return Math.PI * Distribution(normal.Dot((eye + light).Unit)) * Visibility(cosEye, cosLight) * cosLight;
    }

    /// <summary>
    /// This method returns how much of the light from all round a sheen of unit color gives back toward a
    /// direction at the given angle.
    /// </summary>
    public double ShareAt(double cos)
    {
        double[] shares = Shares.GetOrAdd(Alpha, MeasureShares);
        double place = Math.Clamp(cos, 0, 1) * (ShareSteps - 1);
        int below = Math.Min((int) place, ShareSteps - 2);

        return shares[below] + (shares[below + 1] - shares[below]) * (place - below);
    }

    /// <summary>
    /// This method returns how much of the light reaching the cloth from one direction is left for what lies
    /// beneath the sheen, seen from another: all of it but the sheen's share, from the eye's direction or the
    /// lamp's, whichever is the greater.
    /// </summary>
    public double Beneath(double cosEye, double cosLight) =>
        Math.Min(1 - Strongest * ShareAt(cosEye), 1 - Strongest * ShareAt(cosLight));

    /// <summary>
    /// This method returns how much of the light reaching the cloth from all round is left for what lies
    /// beneath the sheen, seen from the given angle.
    /// </summary>
    public double Beneath(double cosEye) => 1 - Strongest * ShareAt(cosEye);

    private double Strongest => Math.Max(Color.Red, Math.Max(Color.Green, Color.Blue));

    // The share a sheen of each roughness gives back, kept for every sheen of that roughness: a cloth's
    // material is copied for every surface that wears it.
    private static readonly ConcurrentDictionary<double, double[]> Shares = new ();

    // The share the sheen gives back at cosines from nought to one, added up over the light's every
    // direction: a grid in the light's angle from the normal and round it.
    private double[] MeasureShares(double alpha)
    {
        double[] shares = new double[ShareSteps];
        Vector normal = new (0, 1, 0);
        const int rings = 256;
        const int spokes = 256;

        for (int step = 0; step < ShareSteps; step++)
        {
            double cos = Math.Max(step / (ShareSteps - 1.0), 1e-3);
            Vector eye = new (Math.Sqrt(1 - cos * cos), cos, 0);
            double total = 0;

            for (int ring = 0; ring < rings; ring++)
            {
                double theta = (ring + 0.5) / rings * Math.PI / 2;

                for (int spoke = 0; spoke < spokes; spoke++)
                {
                    double phi = (spoke + 0.5) / spokes * 2 * Math.PI;
                    Vector light = new (
                        Math.Sin(theta) * Math.Cos(phi), Math.Cos(theta), Math.Sin(theta) * Math.Sin(phi));

                    // Highlight is pi f cos, so f cos is Highlight over pi.
                    total += Highlight(normal, eye, light) / Math.PI * Math.Sin(theta) *
                             (Math.PI / 2 / rings) * (2 * Math.PI / spokes);
                }
            }

            shares[step] = total;
        }

        return shares;
    }
}
