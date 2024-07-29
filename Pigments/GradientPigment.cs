namespace RayTracer.Pigments;

/// <summary>
/// This class is the base class for all gradient pigments.
/// </summary>
public abstract class GradientPigment : Pigment
{
    public bool Bounces { get; set; }
}
