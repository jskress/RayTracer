namespace RayTracer.Basics;

/// <summary>
/// This class provides a generator for turbulence.
/// </summary>
public class Turbulence
{
    /// <summary>
    /// This property controls the depth of the turbulence we will apply.
    /// </summary>
    public int Depth { get; set; } = 1;

    /// <summary>
    /// This property controls whether we will apply phasing to the turbulence.
    /// </summary>
    public bool Phased { get; set; }

    /// <summary>
    /// This property controls the tightness of the turbulence we will apply.  It applies
    /// only if <see cref="Phased"/> is <c>true</c>.
    /// </summary>
    public int Tightness { get; set; } = 10;

    /// <summary>
    /// This property controls the scale of the turbulence we will apply.  It applies only
    /// if <see cref="Phased"/> is <c>true</c>.
    /// </summary>
    public double Scale { get; set; } = 1;

    /// <summary>
    /// This method produces turbulence by accumulating multiple calls to the Perlin noise
    /// generator we were constructed with.
    /// </summary>
    /// <param name="point">The point to determine some noise for.</param>
    /// <returns></returns>
    public double Generate(Point point)
    {
        double noise = 0.0;
        double weight = 1.0;

        for (int i = 0; i < Depth; i++)
        {
            noise += weight * PerlinNoise.Instance.Noise(point);
            weight *= 0.5;
            point = new Point(point.X * 2, point.Y * 2, point.Z * 2);
        }

        noise = Math.Abs(noise);

        if (Phased)
            noise = 1 + Math.Sin(Scale * point.Z + Tightness * noise);

        return noise;
    }
}
