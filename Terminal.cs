namespace RayTracer;

/// <summary>
/// This class provides support for interacting with the terminal.
/// </summary>
public static class Terminal
{
    /// <summary>
    /// This method is used to write to the terminal.  It is sensitive to the quiet and
    /// verbose switches in the program's options.
    /// </summary>
    /// <param name="text">The text to write out.</param>
    /// <param name="isVerbose">Whether the text is considered verbose output.</param>
    /// <param name="newLine">Whether a newline should be emitted as well.</param>
    public static void Out(string text, bool isVerbose = false, bool newLine = true)
    {
        if (!ProgramOptions.Instance.Quiet &&
            (ProgramOptions.Instance.Verbose || !isVerbose))
        {
            Console.Write(text);

            if (newLine)
                Console.WriteLine();
        }
    }
}
