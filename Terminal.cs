using RayTracer.General;

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
    /// <param name="outputLevel">The level of output at which the text should be written.</param>
    /// <param name="newLine">Whether a newline should be emitted as well.</param>
    public static void Out(string text, OutputLevel outputLevel = OutputLevel.Normal, bool newLine = true)
    {
        if (ProgramOptions.Instance.OutputLevel >= outputLevel)
        {
            Console.Write(text);

            if (newLine)
                Console.WriteLine();
        }
    }
}
