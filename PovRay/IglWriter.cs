using System.Globalization;
using System.Text;

namespace RayTracer.PovRay;

/// <summary>
/// This class builds up the text of a generated library.  It keeps the indenting straight and
/// writes numbers the way the ray tracer's own language reads them, which is all the shaping a
/// generated file needs.
/// </summary>
public class IglWriter
{
    private readonly StringBuilder _builder = new();

    private int _depth;

    /// <summary>
    /// This method writes a line at the current depth.  An empty line is written flush, since
    /// trailing spaces on a blank line help no one.
    /// </summary>
    /// <param name="text">The text of the line.</param>
    /// <returns>This writer, so that calls may be chained.</returns>
    public IglWriter Line(string text = "")
    {
        if (text.Length > 0)
            _builder.Append(new string(' ', _depth * 4));

        _builder.AppendLine(text);

        return this;
    }

    /// <summary>
    /// This method opens a block, writing the text that introduces it and indenting what follows.
    /// </summary>
    /// <param name="text">The text introducing the block, without its brace.</param>
    /// <returns>This writer, so that calls may be chained.</returns>
    public IglWriter Open(string text)
    {
        Line($"{text} {{");

        _depth++;

        return this;
    }

    /// <summary>
    /// This method closes a block.
    /// </summary>
    /// <param name="suffix">Anything that should follow the closing brace, such as the comma
    /// between one entry of a list and the next.</param>
    /// <returns>This writer, so that calls may be chained.</returns>
    public IglWriter Close(string suffix = "")
    {
        _depth--;

        return Line($"}}{suffix}");
    }

    /// <summary>
    /// This method writes a number the way the ray tracer reads one.
    /// <para>
    /// The round trip format is used rather than a fixed number of places, so that a value is
    /// written back exactly as POV-Ray meant it.  Rounding here would be a quiet way of changing
    /// what a texture looks like.
    /// </para>
    /// </summary>
    /// <param name="value">The number to write.</param>
    /// <returns>The number, written.</returns>
    public static string Number(double value) =>
        double.IsPositiveInfinity(value)
            ? "infinity"
            : value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// This method writes a color, giving its alpha only when it has one worth giving, so that the
    /// common case reads as the three numbers it is.
    /// </summary>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <param name="alpha">How opaque the color is, where 1 is fully so.</param>
    /// <returns>The color, written.</returns>
    public static string Color(double red, double green, double blue, double alpha = 1)
    {
        string channels = $"{Number(red)}, {Number(green)}, {Number(blue)}";

        return alpha >= 1
            ? $"[{channels}]"
            : $"[{channels}, {Number(alpha)}]";
    }

    /// <summary>
    /// This method adds everything another writer has written.
    /// <para>
    /// It is what lets a declaration be written somewhere of its own and added here only once it
    /// is known to have worked.  Writing straight into the library would leave half a block behind
    /// when a declaration turned out partway through to be one we cannot express, and half a block
    /// is worse than no block: the library would not read at all.
    /// </para>
    /// </summary>
    /// <param name="other">The writer whose text should be added.</param>
    /// <returns>This writer, so that calls may be chained.</returns>
    public IglWriter Add(IglWriter other)
    {
        _builder.Append(other._builder);

        return this;
    }

    public override string ToString() => _builder.ToString();
}
