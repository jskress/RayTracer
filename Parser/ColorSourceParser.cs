using RayTracer.ColorSources;

namespace RayTracer.Parser;

/// <summary>
/// This class handles parsing a color source.
/// </summary>
internal class ColorSourceParser : BoundedContentParser
{
    private static readonly string[] ValidTypes =
    {
        "color", "checker", "linearGradient", "ring", "stripe"
    };
    private readonly TransformParser _transformParser;

    private string _type;
    private ColorSource _colorSource;

    internal ColorSourceParser(FileContent fileContent, string type = "") : base(fileContent, '{', '}')
    {
        _transformParser = new TransformParser(fileContent);
        _type = type;
        _colorSource = null;
    }

    /// <summary>
    /// This method handles parsing the next color source from the content.
    /// </summary>
    /// <returns>The parsed color source.</returns>
    internal ColorSource ParseColorSource()
    {
        if (_type.Length == 0)
        {
            _type = FileContent.GetNextWord(true);

            if (!ValidTypes.Contains(_type))
                FileParser.ErrorOut($"{_type} is not a valid type of color source");
        }

        if ("color" == _type)
            return new SolidColorSource(FileContent.GetNextColor());

        Parse();

        return _colorSource;
    }

    /// <summary>
    /// This method parses the actual content.
    /// </summary>
    protected override void ParseContent()
    {
        List<ColorSource> nestedSources = new ();

        while (true)
        {
            if (IsAtEnd())
                break;

            string word = FileContent.GetNextWord(true);

            if (!_transformParser.TryAddTransform(word))
            {
                ColorSourceParser nested = new (FileContent, word);

                nestedSources.Add(nested.ParseColorSource());
            }
        }

        CreateProperColorSource(nestedSources);

        _colorSource.Transform = _transformParser.GetFinalTransform();
    }

    /// <summary>
    /// This method handles creating our final color source.
    /// </summary>
    /// <param name="sources"></param>
    private void CreateProperColorSource(List<ColorSource> sources)
    {
        if (sources.Count < 2)
            FileParser.ErrorOut("Not enough color sources specified");

        if (sources.Count > 2)
            FileParser.ErrorOut("Too many color sources specified");

        _colorSource = _type switch
        {
            "checker" => new CheckerColorSource(sources[0], sources[1]),
            "linearGradient" => new LinearGradientColorSource(sources[0], sources[1]),
            "ring" => new RingColorSource(sources[0], sources[1]),
            "stripe" => new StripeColorSource(sources[0], sources[1]),
            _ => null // We'll never get here.
        };
    }
}
