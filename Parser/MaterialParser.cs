using System.Reflection;
using RayTracer.Core;

namespace RayTracer.Parser;

/// <summary>
/// This class handles parsing materials.
/// </summary>
internal class MaterialParser : BoundedContentParser
{
    private Material _material;

    internal MaterialParser(FileContent fileContent) : base(fileContent, '{', '}')
    {
        _material = null;
    }

    /// <summary>
    /// This method handles parsing the next material from the content.
    /// </summary>
    /// <returns>The parsed material.</returns>
    internal Material ParseMaterial()
    {
        _material = new Material();

        Parse();

        return _material;
    }

    /// <summary>
    /// This method parses the actual content.
    /// </summary>
    protected override void ParseContent()
    {
        while (true)
        {
            if (IsAtEnd())
                break;

            string word = FileContent.GetNextWord(true);

            switch (word)
            {
                case "colorSource":
                    _material.ColorSource = new ColorSourceParser(FileContent).ParseColorSource();
                    break;
                case "ambient":
                    _material.Ambient = FileContent.GetNextDouble();
                    break;
                case "diffuse":
                    _material.Diffuse = FileContent.GetNextDouble();
                    break;
                case "specular":
                    _material.Specular = FileContent.GetNextDouble();
                    break;
                case "shininess":
                    _material.Shininess = FileContent.GetNextDouble();
                    break;
                case "reflective":
                    _material.Reflective = FileContent.GetNextDouble();
                    break;
                case "transparency":
                    _material.Transparency = FileContent.GetNextDouble();
                    break;
                case "indexOfRefraction":
                case "ior":
                    _material.IndexOfRefraction = ParseIndexOfRefraction();
                    break;
                default:
                    FileParser.ErrorOut($"{word} is not an attribute of a point light.");
                    break;
            }
        }
    }

    /// <summary>
    /// This method parses an index of refraction value.
    /// </summary>
    /// <returns>The parsed index of refraction.</returns>
    private double ParseIndexOfRefraction()
    {
        if (char.IsLetter(FileContent.Peek()))
        {
            string word = FileContent.GetNextWord(true);
            FieldInfo field = typeof(IndicesOfRefraction).GetField(
                word, BindingFlags.Public | BindingFlags.Static);

            if (field == null)
                FileParser.ErrorOut($"{word} is not a valid index of refraction");

            return (double) (field?.GetValue(null) ?? IndicesOfRefraction.Vacuum);
        }

        return FileContent.GetNextDouble();
    }
}
