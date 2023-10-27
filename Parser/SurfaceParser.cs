using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace RayTracer.Parser;

/// <summary>
/// This method is used to parse surfaces.
/// </summary>
internal class SurfaceParser : AttributeParser
{
    private readonly string _directory;
    private readonly Scene _scene;
    private readonly Group _group;

    internal SurfaceParser(FileContent fileContent, string directory, Scene scene = null, Group group = null) : base(fileContent)
    {
        _directory = directory;
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
                SurfaceParser surfaceParser = new (FileContent, _directory, group: group);
                GroupAttributeParser groupAttributeParser = new (FileContent, group);
                ParseSurface(group, surfaceParser, groupAttributeParser);
                break;
            case "triangle":
                ParseTriangle();
                break;
            case "objFile":
                ParseObjectFile();
                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>
    /// This method is used to parse a triangle.
    /// </summary>
    private void ParseTriangle()
    {
        Point point1 = FileContent.GetNextPoint();
        Point point2 = FileContent.GetNextPoint();
        Point point3 = FileContent.GetNextPoint();
        Triangle triangle= new (point1, point2, point3);

        if (FileContent.Peek() == '{')
            ParseSurface(triangle);
        else
            Store(triangle);
    }

    /// <summary>
    /// This method is used to parse an object file.  It comes out as a group.
    /// </summary>
    private void ParseObjectFile()
    {
        string path = Path.GetFullPath(Path.Combine(_directory, FileContent.GetNextQuotedString()));
        ObjectFileParser parser = new (fileName: path);
        Group group = parser.Parse();

        ParseSurface(group);

        // Now, let's post-process the triangle groups to generate bounding boxes and
        // push the material we just parsed down to all the triangles (which isn't perfect
        // for transform reasons).
        TraverseTree(group, group.Material);
    }

    /// <summary>
    /// This method traverses the given root, pushing the given material to all nested
    /// non-group surfaces.
    /// </summary>
    /// <param name="group">The group to traverse</param>
    /// <param name="material">The material to set.</param>
    /// <returns>An appropriate bounding box for the contents of the group.</returns>
    private static BoundingBox TraverseTree(Group group, Material material)
    {
        BoundingBox boundingBox = null;

        foreach (Surface surface in group.Surfaces)
        {
            switch (surface)
            {
                case Group childGroup:
                    BoundingBox childBox = TraverseTree(childGroup, material);
                    if (childBox != null)
                    {
                        if (boundingBox == null)
                            boundingBox = childBox;
                        else
                            boundingBox.Add(childBox);
                    }
                    break;
                case Triangle triangle:
                {
                    if (boundingBox == null)
                        boundingBox = new BoundingBox(triangle.Point1, triangle.Point2);
                    else
                    {
                        boundingBox.Add(triangle.Point1);
                        boundingBox.Add(triangle.Point2);
                    }

                    boundingBox.Add(triangle.Point3);
                    triangle.Material = material;
                    break;
                }
                default:
                    surface.Material = material;
                    break;
            }
        }

        boundingBox?.Adjust();

        group.BoundingBox = boundingBox;

        return boundingBox;
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

        Store(surface);
    }

    /// <summary>
    /// This is a helper method for storing the given surface.
    /// </summary>
    /// <param name="surface">The surface to store.</param>
    private void Store(Surface surface)
    {
        if (_scene != null)
            _scene.Surfaces.Add(surface);
        else if (_group != null)
            _group.Add(surface);
    }
}
