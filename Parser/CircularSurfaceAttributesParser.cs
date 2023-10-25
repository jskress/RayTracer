using RayTracer.Geometry;

namespace RayTracer.Parser;

/// <summary>
/// This class handles parsing the extra attributes for a circular surfaces (cylinders and
/// conics) at the surface level.
/// </summary>
internal class CircularSurfaceAttributesParser : AttributeParser
{
    private readonly CircularSurface _circularSurface;

    internal CircularSurfaceAttributesParser(FileContent fileContent, CircularSurface circularSurface)
        : base(fileContent)
    {
        _circularSurface = circularSurface;
    }

    /// <summary>
    /// This method adds the named attribute to our surface.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <returns><c>true</c>, if the name was a supported attribute, or <c>false</c>, if
    /// not.</returns>
    internal override bool TryParseAttributes(string name)
    {
        switch (name)
        {
            case "minY":
                _circularSurface.MinimumY = FileContent.GetNextDouble();
                break;
            case "maxY":
                _circularSurface.MaximumY = FileContent.GetNextDouble();
                break;
            case "closed":
                _circularSurface.Closed = true;
                break;
            case "open":
                _circularSurface.Closed = false;
                break;
            default:
                return false;
        }

        return true;
    }
}
