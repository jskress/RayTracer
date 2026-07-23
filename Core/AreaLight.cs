using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class represents an area light: a light with width, a lit rectangle rather than a point,
/// which is what gives a shadow a soft edge instead of a hard one.
/// <para>
/// A point light is either seen from a place or hidden from it, so its shadow turns from lit to
/// dark in the span of a single pixel.  Nothing in the world does that -- every real light has a
/// face, and along the edge of a shadow a point sees part of that face past the blocker and the
/// rest of it hidden, so the shadow fades rather than snaps.  That fading, the penumbra, is the
/// whole of what this adds, and it is bought the plain way POV-Ray buys it: the face is looked at
/// from a grid of places across it and the results averaged, so a point that half the grid can see
/// comes out half lit.
/// </para>
/// <para>
/// It is a point light in all else -- it stands somewhere and its light falls off with the cone of
/// nothing -- so it inherits where it lies from.  The one care is that the grid, left plain, bands:
/// evenly spaced samples turn the penumbra into a staircase of a few grey steps.  Nudging each
/// sample off its square by a fixed, seeded jitter breaks the steps into a smooth fall while
/// keeping the render the same from one run to the next, which is the same bargain the noise and
/// the L-systems strike.
/// </para>
/// </summary>
public class AreaLight : PointLight
{
    /// <summary>
    /// This is the seed the jitter falls back on when a scene names none, so that a scene which
    /// says nothing about it still renders the same way every time.
    /// </summary>
    private const int DefaultSeed = 0;

    /// <summary>
    /// This property holds one full edge of the lit rectangle, as a vector from side to side.  The
    /// light reaches half of it either way of its location, so the location is the centre of the
    /// face rather than a corner.
    /// </summary>
    public Vector Axis1 { get; set; } = new (1, 0, 0);

    /// <summary>
    /// This property holds the other full edge of the lit rectangle.
    /// </summary>
    public Vector Axis2 { get; set; } = new (0, 0, 1);

    /// <summary>
    /// This property holds how many samples are taken across <see cref="Axis1"/>.
    /// </summary>
    public int USteps { get; set; } = 4;

    /// <summary>
    /// This property holds how many samples are taken across <see cref="Axis2"/>.
    /// </summary>
    public int VSteps { get; set; } = 4;

    /// <summary>
    /// This property notes whether the samples are nudged off their grid to smooth the penumbra.
    /// It is on by default, since a plain grid bands; turning it off gives the bare grid, which is
    /// what a scene wanting POV-Ray's un-jittered behaviour would ask for.
    /// </summary>
    public bool Jitter { get; set; } = true;

    /// <summary>
    /// This property holds the seed for the jitter, for a scene that wants a particular scatter or
    /// wants to vary it.  Left unset, <see cref="DefaultSeed"/> stands in.
    /// </summary>
    public int? Seed { get; set; }

    private readonly Lazy<(double[] U, double[] V)> _jitter;

    public AreaLight()
    {
        _jitter = new Lazy<(double[], double[])>(BuildJitter);
    }

    /// <summary>
    /// This property notes how many places the face is looked at from, which is the grid it is
    /// divided into.
    /// </summary>
    public override int SampleCount => USteps * VSteps;

    /// <summary>
    /// This method works out one of the places on the face the light is looked at from, following
    /// POV-Ray: the grid square is turned into an offset running from half the face one way to half
    /// the other, nudged off its square by the jitter, and added to the centre.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <param name="index">Which sample, counted across <see cref="Axis1"/> first.</param>
    /// <returns>The sample: which way it lies from the point, how far off, and -- being no
    /// spotlight -- all of the light aimed that way.</returns>
    public override LightSample SampleToward(Point point, int index)
    {
        int u = index % USteps;
        int v = index / USteps;
        (double[] jitterU, double[] jitterV) = _jitter.Value;

        // A single step along an axis has no spread to place a sample within, so it sits at the
        // centre; POV-Ray guards the division by the step count less one the same way.
        double alongU = USteps > 1 ? (u + jitterU[index]) / (USteps - 1) - 0.5 : 0;
        double alongV = VSteps > 1 ? (v + jitterV[index]) / (VSteps - 1) - 0.5 : 0;

        Point on = Location + Axis1 * alongU + Axis2 * alongV;
        Vector toward = on - point;

        return new LightSample(toward.Unit, toward.Magnitude, 1);
    }

    /// <summary>
    /// This method works out the jitter offsets once, so that they are fixed for the whole render
    /// and the same from one render to the next.  Each is a nudge between minus and plus a half a
    /// grid square, which is POV-Ray's own, and all zero when the jitter is turned off.
    /// </summary>
    /// <returns>The offsets along each axis, one per sample.</returns>
    private (double[], double[]) BuildJitter()
    {
        int count = SampleCount;
        double[] u = new double[count];
        double[] v = new double[count];

        if (Jitter)
        {
            Random random = new Random(Seed ?? DefaultSeed);

            for (int index = 0; index < count; index++)
            {
                u[index] = random.NextDouble() - 0.5;
                v[index] = random.NextDouble() - 0.5;
            }
        }

        return (u, v);
    }
}
