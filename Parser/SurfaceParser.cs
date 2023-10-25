using RayTracer.Core;
using RayTracer.Geometry;

namespace RayTracer.Parser;

/// <summary>
/// This method is used to parse surfaces.
/// </summary>
internal class SurfaceParser : AttributeParser
{
    private readonly Scene _scene;
    private readonly Group _group;

    internal SurfaceParser(FileContent fileContent, Scene scene = null, Group group = null) : base(fileContent)
    {
        _scene = scene;
        _group = group;
    }

    /// <summary>
    /// This method is used to try to parse a surface from the content.
    /// </summary>
    /// <param name="name">The type of surface to parse.</param>
    /// <returns><c>true</c>, if the name was a valid type of surface, or <c>false</c>, if
    /// not.</returns>
    internal override bool TryParseAttributes(string name)
    {
        switch (name)
        {
            case "sphere":
                ParseSurface(new Sphere());
                break;
            case "plane":
                ParseSurface(new Plane());
                break;
            case "cube":
                ParseSurface(new Cube());
                break;
            case "cylinder":
                Cylinder cylinder = new ();
                CircularSurfaceAttributesParser circularSurfaceAttributesParser = new (FileContent, cylinder);
                ParseSurface(cylinder, circularSurfaceAttributesParser);
                break;
            case "conic":
                Conic conic = new ();
                CircularSurfaceAttributesParser conicAttributesParser = new (FileContent, conic);
                ParseSurface(conic, conicAttributesParser);
                break;
            case "group":
                Group group = new ();
                SurfaceParser surfaceParser = new (FileContent, group: group);
                GroupAttributeParser groupAttributeParser = new (FileContent, group);
                ParseSurface(group, surfaceParser, groupAttributeParser);
                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>
    /// This method is used to parse the details about a surface.
    /// </summary>
    /// <param name="surface">The surface to fill in with details.</param>
    /// <param name="attributeParsers">Any optional parsers for extra attributes.</param>
    private void ParseSurface(Surface surface, params AttributeParser[] attributeParsers)
    {
        if (attributeParsers.Length == 0)
            attributeParsers = null;

        SurfaceAttributeParser surfaceAttributeParser = new (FileContent, surface, attributeParsers);

        surfaceAttributeParser.Parse();

        if (_scene != null)
            _scene.Surfaces.Add(surface);
        else if (_group != null)
            _group.Add(surface);
    }
}
