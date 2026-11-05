using System.Globalization;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class handles parsing Wavefront OBJ files: vertices, texture coordinates and normals, faces of any number of
/// corners as fans of triangles, groups and objects, and the materials a material library gives them.
/// <para>
/// **A model is mirrored into this engine's space as it is read.**  A model file is right-handed and this engine is
/// left-handed, so a model read as it stands comes out its own mirror image -- a word in its texture backward.  So
/// every Z is negated, a vertex's and a normal's, which turns the model the right way about and leaves its faces
/// wound the way this engine's triangles face out; a model's front, toward +Z where it was made, faces -Z here,
/// toward the usual camera, as this engine's buildings and vehicles do.
/// </para>
/// <para>
/// `mtllib` names a material library (see <see cref="MaterialLibrary"/>), found beside the model, and `usemtl` gives
/// the faces after it one of its materials; a face given none, or one the library does not hold, wears whatever the
/// model as a whole is given.  A face's corners are `v`, `v/vt`, `v//vn` or `v/vt/vn`, counted from one, or from the
/// end when negative; a texture or normal index that names nothing is taken as none.  `g` and `o` start a group,
/// named for what follows them.  A line ending in a backslash runs on, and `#` starts a remark.
/// </para>
/// </summary>
public class ObjectFileParser
{
    public List<Point> Vertices { get; }
    public List<Vector> Normals { get; }
    public List<TwoDPoint> TextureCoordinates { get; }
    public List<Triangle> Triangles { get; }

    /// <summary>
    /// This property holds the materials the model's libraries give, by name.
    /// </summary>
    public Dictionary<string, Material> Materials { get; } = [];

    /// <summary>
    /// This property holds what the model and its libraries said that was not read, for a note.
    /// </summary>
    public List<string> Unread { get; } = [];

    /// <summary>
    /// This property holds the materials a scene puts in place of the library's, by name.
    /// </summary>
    public Dictionary<string, Material> Replacements { get; init; } = [];

    private readonly IEnumerable<string> _lines;
    private readonly string _directory;

    private Group _topGroup;
    private Group _currentGroup;
    private Material _currentMaterial;

    public ObjectFileParser(string fileName = null, string content = null)
    {
        _lines = fileName != null
            ? File.ReadAllLines(fileName)
            : content?.Split('\n') ?? [];
        _directory = fileName is null ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(Path.GetFullPath(fileName));
        _topGroup = null;
        _currentGroup = null;

        Vertices = [];
        Normals = [];
        TextureCoordinates = [];
        Triangles = [];
    }

    /// <summary>
    /// This method is used to parse the object file's content.
    /// </summary>
    /// <returns>The resulting group.</returns>
    public Group Parse()
    {
        foreach (string line in Joined(_lines))
        {
            string[] words = WordsOf(line);

            if (words.Length == 0)
                continue;

            switch (words[0])
            {
                case "v":
                    StoreVertex(words);
                    break;
                case "vt":
                    StoreTextureCoordinate(words);
                    break;
                case "vn":
                    StoreVertexNormal(words);
                    break;
                case "f":
                    StoreFace(words);
                    break;
                case "g" or "o":
                    StoreGroup(words);
                    break;
                case "mtllib":
                    ReadLibraries(words);
                    break;
                case "usemtl":
                    UseMaterial(words);
                    break;
                case "s" or "l" or "p" or "vp":
                    break;
                default:
                    if (char.IsLetter(words[0][0]) && words[0].All(character => char.IsLetterOrDigit(character) || character == '_'))
                        NoteUnread(words[0]);
                    break;
            }
        }

        return _topGroup;
    }

    /// <summary>
    /// This method returns a file's lines with those ending in a backslash run on into the next, and remarks dropped.
    /// </summary>
    internal static IEnumerable<string> Joined(IEnumerable<string> lines)
    {
        string carried = null;

        foreach (string raw in lines)
        {
            string line = raw.TrimEnd('\r');
            int remark = line.IndexOf('#');

            if (remark >= 0)
                line = line[..remark];

            if (line.TrimEnd().EndsWith('\\'))
            {
                carried = (carried ?? "") + line.TrimEnd()[..^1] + " ";
                continue;
            }

            yield return (carried ?? "") + line;

            carried = null;
        }

        if (carried is not null)
            yield return carried;
    }

    /// <summary>
    /// This method splits a line into its words.
    /// </summary>
    internal static string[] WordsOf(string line) =>
        line.Split([' ', '\t'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static double Number(string word) => double.Parse(word, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>
    /// This method handles storing a vertex, mirrored into this engine's space.
    /// </summary>
    /// <param name="words">The words from the "v" command line.</param>
    private void StoreVertex(IReadOnlyList<string> words)
    {
        Vertices.Add(new Point(Number(words[1]), Number(words[2]), -Number(words[3])));
    }

    /// <summary>
    /// This method handles storing a texture coordinate: across, and up, the picture.
    /// </summary>
    /// <param name="words">The words from the "vt" command line.</param>
    private void StoreTextureCoordinate(IReadOnlyList<string> words)
    {
        TextureCoordinates.Add(new TwoDPoint(Number(words[1]), words.Count > 2 ? Number(words[2]) : 0));
    }

    /// <summary>
    /// This method handles storing a vertex normal, mirrored into this engine's space.
    /// </summary>
    /// <param name="words">The words from the "vn" command line.</param>
    private void StoreVertexNormal(IReadOnlyList<string> words)
    {
        Normals.Add(new Vector(Number(words[1]), Number(words[2]), -Number(words[3])));
    }

    /// <summary>
    /// This method handles storing a face, as a fan of triangles from its first corner.
    /// </summary>
    /// <param name="words">The words from the "f" command line.</param>
    private void StoreFace(string[] words)
    {
        (Point Point, TwoDPoint Texture, Vector Normal)[] corners = words[1..].Select(ParseFaceTerm).ToArray();

        _topGroup ??= _currentGroup = new Group();

        for (int index = 1; index < corners.Length - 1; index++)
        {
            (Point point1, TwoDPoint texture1, Vector normal1) = corners[0];
            (Point point2, TwoDPoint texture2, Vector normal2) = corners[index];
            (Point point3, TwoDPoint texture3, Vector normal3) = corners[index + 1];

            Triangle triangle = normal1 == null || normal2 == null || normal3 == null
                ? new Triangle
                {
                    Point1 = point1,
                    Point2 = point2,
                    Point3 = point3
                }
                : new SmoothTriangle
                {
                    Point1 = point1,
                    Point2 = point2,
                    Point3 = point3,
                    Normal1 = normal1,
                    Normal2 = normal2,
                    Normal3 = normal3
                };

            if (texture1 is not null && texture2 is not null && texture3 is not null)
                triangle.TextureCoordinates = (texture1, texture2, texture3);

            // The material the library gives, or none, so the triangle wears what the model is given.
            triangle.Material = _currentMaterial;

            Triangles.Add(triangle);

            _currentGroup.Add(triangle);
        }
    }

    /// <summary>
    /// This method is used to parse a term from a face statement into the referenced point and its optional texture
    /// coordinate and normal.
    /// </summary>
    /// <param name="spec">The spec to parse.</param>
    /// <returns>The point and its optional texture coordinate and normal.</returns>
    private (Point, TwoDPoint, Vector) ParseFaceTerm(string spec)
    {
        string[] parts = spec.Split('/');
        Point point = Vertices[IndexOf(parts[0], Vertices.Count)];
        TwoDPoint texture = parts.Length > 1 ? Optional(parts[1], TextureCoordinates) : null;
        Vector normal = parts.Length > 2 ? Optional(parts[2], Normals) : null;

        return (point, texture, normal);
    }

    // An index, counted from one, or back from the end when negative.
    private static int IndexOf(string word, int count)
    {
        int index = int.Parse(word, CultureInfo.InvariantCulture);

        return index < 0 ? count + index : index - 1;
    }

    // What an index names, or none if it is empty or names nothing.
    private static T Optional<T>(string word, List<T> items) where T : class
    {
        if (word.Length == 0 || !int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            return null;

        int at = index < 0 ? items.Count + index : index - 1;

        return at >= 0 && at < items.Count ? items[at] : null;
    }

    /// <summary>
    /// This method is used to start a new group to add things to, named for what follows the word.
    /// </summary>
    private void StoreGroup(string[] words)
    {
        _topGroup ??= _currentGroup = new Group();

        _currentGroup = new Group();
        _topGroup.Add(_currentGroup);

        if (words.Length > 1)
            _currentGroup.Name = string.Join(' ', words[1..]);

        // Always inherit our material from the owning group.
        _currentGroup.Material = null;
    }

    /// <summary>
    /// This method reads the material libraries a model names, beside it.
    /// </summary>
    private void ReadLibraries(string[] words)
    {
        // A library's name may have spaces in it, so the whole of the rest is tried as one name first.
        List<string> names = File.Exists(Path.Combine(_directory, string.Join(' ', words[1..])))
            ? [string.Join(' ', words[1..])]
            : [..words[1..]];

        foreach (string name in names)
        {
            string path = Path.Combine(_directory, name);

            if (!File.Exists(path))
            {
                NoteUnread($"mtllib {name} (not found)");
                continue;
            }

            MaterialLibrary library = new (File.ReadAllLines(path), Path.GetDirectoryName(path));

            foreach ((string material, Material value) in library.Materials)
                Materials[material] = value;

            foreach (string unread in library.Unread)
                NoteUnread(unread);
        }
    }

    /// <summary>
    /// This method gives the faces that follow one of the libraries' materials, or one the scene puts in its place.
    /// </summary>
    private void UseMaterial(string[] words)
    {
        string name = string.Join(' ', words[1..]);

        _currentMaterial = Replacements.TryGetValue(name, out Material replacement)
            ? replacement
            : Materials.GetValueOrDefault(name);
    }

    private void NoteUnread(string what)
    {
        if (!Unread.Contains(what))
            Unread.Add(what);
    }
}
