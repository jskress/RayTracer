using RayTracer.Geometry;

namespace RayTracer.Parser;

/// <summary>
/// This class handles parsing of surfaces.
/// </summary>
internal class SurfaceParser : BoundedContentParser
{
    private readonly Surface _surface;
    private readonly TransformParser _transformParser;
    private readonly AttributeParser _attributeParser;

    internal SurfaceParser(
        FileContent fileContent, Surface surface, AttributeParser attributeParser = null)
        : base(fileContent, '{', '}')
    {
        _surface = surface;
        _transformParser = new TransformParser(fileContent);
        _attributeParser = attributeParser;
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

            if (_attributeParser != null && _attributeParser.TryParseAttributes(word))
                continue;

            if (!_transformParser.TryParseAttributes(word))
            {
                if (word == "material")
                    _surface.Material = new MaterialParser(FileContent).ParseMaterial();
                else
                    FileParser.ErrorOut($"{word} is not an attribute of a surface.");
            }
        }

        _surface.Transform = _transformParser.GetFinalTransform();
    }
}
