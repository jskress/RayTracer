namespace RayTracer.Basics;

/// <summary>
/// This interface marks an object that makes use of Perlin noise
/// </summary>
public interface INoiseConsumer
{
    /// <summary>
    /// This property holds the seed for the noise generator to use.
    /// If it is not specified, one with a default noise generator will be used.
    /// </summary>
    public int? Seed { get; set; }
}
