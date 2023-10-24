using RayTracer.Core;

namespace RayTracer.Parser;

/// <summary>
/// This class is used to parse light information.
/// </summary>
internal class LightParser : BoundedContentParser
{
    private PointLight _light;

    internal LightParser(FileContent fileContent) : base(fileContent, '{', '}')
    {
        _light = null;
    }

    /// <summary>
    /// This method is used to parse the next light from the content.
    /// </summary>
    /// <returns>The light we parsed.</returns>
    internal PointLight ParseLight()
    {
        _light = new PointLight();

        Parse();

        return _light;
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
                case "location":
                    _light.Location = FileContent.GetNextPoint();
                    break;
                case "color":
                    _light.Color = FileContent.GetNextColor();
                    break;
                default:
                    FileParser.ErrorOut($"{word} is not an attribute of a point light.");
                    break;
            }
        }
    }
}
