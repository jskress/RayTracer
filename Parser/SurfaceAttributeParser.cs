using RayTracer.Geometry;

namespace RayTracer.Parser;

/// <summary>
/// This class handles parsing of surfaces.
/// </summary>
internal class SurfaceAttributeParser : BoundedContentParser
{
    private readonly Surface _surface;
    private readonly TransformParser _transformParser;
    private readonly AttributeParser[] _attributeParsers;

    internal SurfaceAttributeParser(
        FileContent fileContent, Surface surface, AttributeParser[] attributeParsers = null)
        : base(fileContent, '{', '}')
    {
        _surface = surface;
        _transformParser = new TransformParser(fileContent);
        _attributeParsers = attributeParsers;
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

            if (_attributeParsers != null && _attributeParsers
                    .Any(parser => parser.TryParseAttributes(word)))
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
