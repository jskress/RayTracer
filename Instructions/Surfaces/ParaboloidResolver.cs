using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a paraboloid value.  It takes nothing a cylinder does not: the unit
/// bowl plus a transform is every paraboloid there is.
/// </summary>
public class ParaboloidResolver : ExtrudedSurfaceResolver<Paraboloid>;
