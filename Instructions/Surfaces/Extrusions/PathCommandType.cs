namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This enumeration specifies the type of general path commands we support.
/// </summary>
public enum PathCommandType
{
    MoveTo,
    LineTo,
    QuadTo,
    CurveTo,
    Close,
    Svg
}
