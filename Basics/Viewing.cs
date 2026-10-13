namespace RayTracer.Basics;

/// <summary>
/// This struct holds the directions a surface is being shaded between: the way to the eye, the way the
/// surface faces, and the way to the light, all in the world's own space.
/// <para>
/// **Most colors do not care**, and are worked out from where on the surface they are and nothing else.
/// A paint whose color shifts with the angle it is seen at does care -- deep blue from one side, dark
/// green from the other -- and only it is handed these.  See <see cref="Patterns.FlopPattern"/>.
/// </para>
/// </summary>
public readonly struct Viewing
{
    /// <summary>
    /// This property holds the unit direction from the point toward the eye, or <c>null</c> for no
    /// viewing at all.
    /// </summary>
    public Vector Eye { get; }

    /// <summary>
    /// This property holds the unit direction the surface faces, on the eye's side.
    /// </summary>
    public Vector Normal { get; }

    /// <summary>
    /// This property holds the unit direction from the point toward the light being shaded for, or
    /// <c>null</c> when no particular light is -- as for what a surface lets through, or tints a
    /// reflection with.
    /// </summary>
    public Vector Light { get; }

    /// <summary>
    /// This constructor records the directions.
    /// </summary>
    /// <param name="eye">The unit direction toward the eye.</param>
    /// <param name="normal">The unit direction the surface faces, on the eye's side.</param>
    /// <param name="light">The unit direction toward the light, or <c>null</c> for none in particular.</param>
    public Viewing(Vector eye, Vector normal, Vector light = null)
    {
        Eye = eye;
        Normal = normal;
        Light = light;
    }

    /// <summary>
    /// This property notes that there is no viewing to speak of.
    /// </summary>
    public bool IsEmpty => Eye is null || Normal is null;

    /// <summary>
    /// This property holds the angle, in radians from nought to π, between the way to the eye and the way
    /// the light mirrors off the surface: nought looking straight along the reflection of the light, π
    /// looking straight back the way the light came.
    /// <para>
    /// **With no light in particular, the light is taken to be at the eye**, which makes the angle twice
    /// the eye's own angle from the surface: nought seen square-on, π seen edge-on.  So a color worked
    /// out without a light agrees with the one a lamp right behind the eye would give.
    /// </para>
    /// </summary>
    public double Aspecular
    {
        get
        {
            Vector light = Light ?? Eye;
            Vector mirror = Normal * (2 * Normal.Dot(light)) - light;

            return Math.Acos(Math.Clamp(mirror.Dot(Eye) / mirror.Magnitude, -1, 1));
        }
    }
}
