namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This enumerates the supported types of renderers for an L-system production.
/// </summary>
public enum LSystemRendererType
{
    Extrusion
}

/// <summary>
/// This class gives us some useful extensions for our renderer type enum.
/// </summary>
public static class LSystemRendererTypeExtensions
{
    public static LSystemShapeRenderer GetRenderer(
        this LSystemRendererType type, string production)
    {
        return type switch
        {
            LSystemRendererType.Extrusion => new LSystemExtrusionRenderer(production),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported renderer type.")
        };
    }
}
