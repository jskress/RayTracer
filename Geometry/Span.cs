namespace RayTracer.Geometry;

/// <summary>
/// This record holds the two named places a surface is stretched between: it stands with its origin at the first and
/// its +X running toward the second, its +Y as near up -- its parent's +Y -- as that allows, and, if `stretched`, its
/// X scaled by the distance between them, so that a thing drawn from nought to one along X reaches exactly.
/// </summary>
/// <param name="From">The name of the place its origin stands at.</param>
/// <param name="To">The name of the place its +X runs toward.</param>
/// <param name="Stretched">Whether its X is scaled by the distance between them.</param>
public sealed record Span(string From, string To, bool Stretched);
