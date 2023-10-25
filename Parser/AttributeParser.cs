namespace RayTracer.Parser;

/// <summary>
/// This is the base class for parsers that need to support subsets of attributes.
/// </summary>
internal abstract class AttributeParser
{
    protected readonly FileContent FileContent;

    internal AttributeParser(FileContent fileContent)
    {
        FileContent = fileContent;
    }

    /// <summary>
    /// This method is used to parse additional attributes for a containing block.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <returns><c>true</c>, if the name was a supported attribute, or <c>false</c>, if
    /// not.</returns>
    internal abstract bool TryParseAttributes(string name);
}
