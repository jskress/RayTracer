using Lex.Parser;
using RayTracer.General;

namespace RayTracer;

/// <summary>
/// This class provides support for interacting with the terminal.
/// </summary>
public static class Terminal
{
    /// <summary>
    /// This property is used to govern just how much we write out.
    /// </summary>
    public static OutputLevel OutputLevel { get; set; } = OutputLevel.Normal;

    /// <summary>
    /// This method is used to write to the terminal.  It is sensitive to the quiet and
    /// verbose switches in the program's options.
    /// </summary>
    /// <param name="text">The text to write out.</param>
    /// <param name="outputLevel">The level of output at which the text should be written.</param>
    /// <param name="newLine">Whether a newline should be emitted as well.</param>
    public static void Out(string text, OutputLevel outputLevel = OutputLevel.Normal, bool newLine = true)
    {
        if (OutputLevel >= outputLevel)
        {
            Console.Write(text);

            if (newLine)
                Console.WriteLine();
        }
    }

    /// <summary>
    /// This method is used to show details about the given exception.
    /// </summary>
    /// <param name="exception">The exception to show.</param>
    public static void ShowException(Exception exception)
    {
        Console.WriteLine($"Error: {exception.Message}");

        if (exception is TokenException tokenException)
        {
            int line = tokenException.Token.Line;
            int column = tokenException.Token.Column;

            if (line > 0)
            {
                Console.WriteLine($"[{line}:{column}] -> {tokenException.Token}");

                if (column > 0)
                    Console.WriteLine($"{new string('-', column - 1)}^");
            }
        }

        if (OutputLevel is OutputLevel.Verbose)
            Console.WriteLine(exception.StackTrace);
    }
}
