using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Parser;

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

        Vertices = new List<Point>();
        Normals = new List<Vector>();
        Triangles = new List<Triangle>();
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
        Point[] points = words[1..]
            .Select(word => Vertices[int.Parse(word) - 1])
            .Reverse()
            .ToArray();

        for (int index = 1; index < points.Length - 1; index++)
        {
            Triangle triangle = new (points[0], points[index], points[index + 1]);

            Triangles.Add(triangle);

            _topGroup ??= _currentGroup = new Group();

            _currentGroup.Add(triangle);
        }
    }

    /// <summary>
    /// This method is used to start a new group to add things to.
    /// </summary>
    private void StoreGroup()
    {
        _topGroup ??= _currentGroup = new Group();

        _currentGroup = new Group();
        _topGroup.Add(_currentGroup);
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
