using Lex.Parser;
using RayTracer.Fonts;
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
    /// This method is used to format a grid, or table, of data and print it out.
    /// </summary>
    /// <param name="lines">The table data to output.</param>
    /// <param name="prefix">An optional prefix to print before each line.</param>
    /// <param name="spacing">An optional amount of space to put between the columns; the
    /// default is two spaces.</param>
    /// <param name="hasHeadings">A flag noting whether the first entry in the data represents
    /// headings for the columns.</param>
    /// <param name="alignments">The alignment to apply to each column.</param>
    /// <param name="outputLevel">The output level to respect.</param>
    public static void Out(
        List<List<string>> lines, string prefix = "  ", string spacing = "  ", bool hasHeadings = false,
        List<TextAlignment> alignments = null, OutputLevel outputLevel = OutputLevel.Normal)
    {
        int[] widths = GetColumnWidths(lines);
        string sp = prefix;
        bool first = true;

        alignments ??= [];

        while (alignments.Count > lines[0].Count)
            alignments.Add(TextAlignment.Left);

        foreach (List<string> line in lines)
        {
            for (int index = 0; index < line.Count; index++)
            {
                TextAlignment alignment = hasHeadings && index == 0
                    ? TextAlignment.Center
                    : alignments[index];
                string text = alignment switch
                {
                    TextAlignment.Left => line[index].PadRight(widths[index]),
                    TextAlignment.Center => Center(line[index], widths[index]),
                    TextAlignment.Right => line[index].PadLeft(widths[index]),
                    _ => throw new NotSupportedException($"Unsupported text alignment {alignments[index]}")
                };

                Out(sp, outputLevel, false);
                Out(text, outputLevel, false);
            }

            Out("", outputLevel);

            if (hasHeadings && first)
            {
                string dashes = prefix + string.Join(
                    spacing, widths.Select(width => new string('-', width)));

                Out(dashes, outputLevel);

                first = false;
            }

            sp = spacing;
        }
    }

    /// <summary>
    /// This method is used to determine the column widths for the give columnar output.
    /// </summary>
    /// <param name="lines">The columnar output to get the column widths for.</param>
    /// <returns>The determined column widths.</returns>
    private static int[] GetColumnWidths(List<List<string>> lines)
    {
        int[] widths = new int[lines[0].Count];

        foreach (List<string> line in lines)
        {
            for (int index = 0; index < line.Count; index++)
                widths[index] = Math.Max(widths[index], line[index].Length);
        }

        return widths;
    }

    /// <summary>
    /// This is a helper method for centering the given text within the specified width.
    /// </summary>
    /// <param name="text">The text to center.</param>
    /// <param name="width">The width to center it in.</param>
    /// <returns>The centered text.</returns>
    private static string Center(string text, int width)
    {
        width -= text.Length;

        if (width < 1)
            return text;

        int left = width / 2;
        int right = width - left;
        string leftSpace = new string(' ', left);
        string rightSpace = new string(' ', right);

        return $"{leftSpace}{text}{rightSpace}";
    }

    /// <summary>
    /// This method may be used to write the given text in the specified color.
    /// </summary>
    /// <param name="text">The text to write.</param>
    /// <param name="color">The color to write it in.</param>
    public static void OutInColor(string text, ConsoleColor color)
    {
        ConsoleColor hold = Console.ForegroundColor;
        
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = hold;
    }

    /// <summary>
    /// This method is used to emit an error message and, perhaps, halt the program.
    /// </summary>
    /// <param name="message">The error message to write out.</param>
    /// <param name="halt">A flag noting whether the program should be halted.</param>
    public static void ShowError(string message, bool halt = true)
    {
        OutInColor($"Error: {message}", ConsoleColor.Red);

        if (halt)
            Environment.Exit(1);
    }

    /// <summary>
    /// This method is used to show details about the given exception.
    /// </summary>
    /// <param name="exception">The exception to show.</param>
    public static void ShowException(Exception exception)
    {
        ShowError(exception.Message, false);

        if (exception is TokenException tokenException)
        {
            int line = tokenException.Token.Line;
            int column = tokenException.Token.Column;

            if (line > 0)
                Console.WriteLine($"[{line}:{column}] -> {tokenException.Token}");
        }

        if (OutputLevel is OutputLevel.Verbose)
            Console.WriteLine(exception.StackTrace);
    }
}
