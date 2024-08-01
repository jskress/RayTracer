using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Instructions;

/// <summary>
/// This class handles parsing Wavefront OBJ files.
/// </summary>
public class ObjectFileParser
{
    public List<Point> Vertices { get; }
    public List<Vector> Normals { get; }
    public List<Triangle> Triangles { get; }

    private readonly string[] _lines;

    private Group _topGroup;
    private Group _currentGroup;

    public ObjectFileParser(string fileName = null, string content = null)
    {
        _lines = fileName != null
            ? File.ReadAllLines(fileName)
            : content?.Split('\n');
        _topGroup = null;
        _currentGroup = null;

        Vertices = [];
        Normals = [];
        Triangles = [];
    }

    /// <summary>
    /// This method is used to parse the object file's content.
    /// </summary>
    /// <returns>The resulting group.</returns>
    public Group Parse()
    {
        foreach (string line in _lines)
        {
            string[] words = line.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
                continue;

            switch (words[0])
            {
                case "v":
                    StoreVertex(words);
                    break;
                case "f":
                    StoreFace(words);
                    break;
                case "g":
                    StoreGroup();
                    break;
                case "vn":
                    StoreVertexNormal(words);
                    break;
            }
        }

        return _topGroup;
    }

    /// <summary>
    /// This method handles storing a vertex.
    /// </summary>
    /// <param name="words">The words from the "v" command line.</param>
    private void StoreVertex(IReadOnlyList<string> words)
    {
        double x = double.Parse(words[1]);
        double y = double.Parse(words[2]);
        double z = double.Parse(words[3]);

        Vertices.Add(new Point(x, y, z));
    }

    /// <summary>
    /// This method handles storing a face.
    /// </summary>
    /// <param name="words">The words from the "f" command line.</param>
    private void StoreFace(string[] words)
    {
        (Point, Vector)[] points = words[1..]
            .Select(ParseFaceTerm)
            .Reverse()
            .ToArray();

        _topGroup ??= _currentGroup = new Group();

        for (int index = 1; index < points.Length - 1; index++)
        {
            (Point point1, Vector normal1) = points[0];
            (Point point2, Vector normal2) = points[index];
            (Point point3, Vector normal3) = points[index + 1];

            Triangle triangle = normal1 == null || normal2 == null || normal3 == null
                ? new Triangle(point1, point2, point3)
                : new SmoothTriangle(point1, point2, point3, normal1, normal2, normal3);

            // Always inherit our material from the owning group.
            triangle.Material = null;

            Triangles.Add(triangle);

            _currentGroup.Add(triangle);
        }
    }

    /// <summary>
    /// This method is used to parse a term from a face statement into the referenced
    /// point and optional normal.
    /// </summary>
    /// <param name="spec">The spec to parse.</param>
    /// <returns>The point and its optional normal vector.</returns>
    private (Point, Vector) ParseFaceTerm(string spec)
    {
        if (spec.Contains('/'))
        {
            string[] parts = spec.Split('/', 3);

            if (parts.Length > 2)
            {
                return (Vertices[int.Parse(parts[0]) - 1],
                    Normals[int.Parse(parts[2]) - 1]);
            }
        }

        return (Vertices[int.Parse(spec) - 1], null);
    }

    /// <summary>
    /// This method is used to start a new group to add things to.
    /// </summary>
    private void StoreGroup()
    {
        _topGroup ??= _currentGroup = new Group();

        _currentGroup = new Group();
        _topGroup.Add(_currentGroup);

        // Always inherit our material from the owning group.
        _currentGroup.Material = null;
    }

    /// <summary>
    /// This method handles storing a vertex.
    /// </summary>
    /// <param name="words">The words from the "vn" command line.</param>
    private void StoreVertexNormal(IReadOnlyList<string> words)
    {
        double x = double.Parse(words[1]);
        double y = double.Parse(words[2]);
        double z = double.Parse(words[3]);

        Normals.Add(new Vector(x, y, z));
    }
}
