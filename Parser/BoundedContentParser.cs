namespace RayTracer.Parser;

/// <summary>
/// This class provides a base class for parsing content that is bounded by braces.
/// </summary>
internal abstract class BoundedContentParser
{
    protected readonly FileContent FileContent;

    private readonly char _start;
    private readonly char _end;

    protected BoundedContentParser(FileContent fileContent, char start, char end)
    {
        FileContent = fileContent;
        _start = start;
        _end = end;
    }

    /// <summary>
    /// This method is used to parse the content and its surrounding braces.
    /// </summary>
    internal void Parse()
    {
        if (!FileContent.IsNext(_start))
            FileParser.ErrorOut($"Expecting an opening brace ('{{') but found {FileContent.Peek()}");

        ParseContent();
    }

    /// <summary>
    /// This method should parse the actual content.
    /// </summary>
    protected abstract void ParseContent();

    /// <summary>
    /// This method returns whether we have reached the end of our block.
    /// </summary>
    /// <returns><c>true</c>, if we are at the end of our block, or <c>false</c>, if not.</returns>
    protected bool IsAtEnd()
    {
        return FileContent.IsNext(_end);
    }
}
