using RayTracer.Scanners;

namespace RayTracer.Parser;

/// <summary>
/// This class handles parsing of what scanner to use.
/// </summary>
internal class ScannerParser
{
    private readonly FileContent _fileContent;

    internal ScannerParser(FileContent fileContent)
    {
        _fileContent = fileContent;
    }

    /// <summary>
    /// This method returns an appropriate scanner based on the next word from the content.
    /// </summary>
    /// <returns></returns>
    internal IScanner Parse()
    {
        string word = _fileContent.GetNextWord(true);
        IScanner scanner = word switch
        {
            "single" => new SingleThreadScanner(),
            "line" => new LineParallelScanner(),
            "pixel" => new PixelParallelScanner(),
            _ => null
        };

        if (scanner == null)
            FileParser.ErrorOut($"Scanner must be 'single', 'line' or 'pixel', not {word}");

        return scanner;
    }
}
