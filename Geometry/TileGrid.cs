namespace RayTracer.Geometry;

/// <summary>
/// This class represents a grid of cells laid with tiles by wave function collapse (see
/// <see cref="WaveFunctionCollapse"/>): each tile a thing built once and stood, turned as it was laid, in every cell
/// it was laid in.  It is laid when it is resolved, and is a group of what was laid from then on.
/// </summary>
public class TileGrid : Group;
