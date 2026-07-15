namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This enumeration specifies the type of spline commands we support.
/// </summary>
public enum SplineCommandType
{
    MoveTo,
    LineTo,
    QuadTo,
    CurveTo
}
