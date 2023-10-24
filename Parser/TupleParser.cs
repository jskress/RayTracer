using System.Reflection;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Parser;

/// <summary>
/// This class handles parsing a three-member tuple as points or vectors.
/// </summary>
internal class TupleParser
{
    private readonly FileContent _fileContent;

    internal TupleParser(FileContent fileContent)
    {
        _fileContent = fileContent;
    }

    /// <summary>
    /// This method parses the next point from the content.
    /// </summary>
    /// <returns>The parsed point.</returns>
    internal Point ParsePoint()
    {
        double[] tuple = ParseTuple();

        return new Point(tuple[0], tuple[1], tuple[2]);
    }

    /// <summary>
    /// This method parses the next vector from the content.
    /// </summary>
    /// <param name="canBeDirection">Whether the vector can be a named direction.</param>
    /// <returns>The parsed vector.</returns>
    internal Vector ParseVector(bool canBeDirection)
    {
        if (canBeDirection && _fileContent.Peek() != '<')
        {
            string word = _fileContent.GetNextWord(true);
            FieldInfo field = typeof(Directions).GetField(
                word, BindingFlags.Public | BindingFlags.Static);

            if (field == null)
                FileParser.ErrorOut($"{word} is not a valid direction");

            return (Vector) field?.GetValue(null);
        }

        double[] tuple = ParseTuple();

        return new Vector(tuple[0], tuple[1], tuple[2]);
    }

    /// <summary>
    /// This method parses the next color from the content.
    /// </summary>
    /// <returns>The parsed color.</returns>
    internal Color ParseColor()
    {
        if (_fileContent.Peek() != '<')
        {
            string word = _fileContent.GetNextWord(true);
            FieldInfo field = typeof(Colors).GetField(
                word, BindingFlags.Public | BindingFlags.Static);

            if (field == null)
                FileParser.ErrorOut($"{word} is not a valid color");

            return (Color) field?.GetValue(null);
        }

        double[] tuple = ParseTuple();

        return new Color(tuple[0], tuple[1], tuple[2]);
    }

    /// <summary>
    /// This method parses the next tuple of numbers from the content.
    /// </summary>
    /// <returns>The parsed tuple of 3 doubles.</returns>
    internal double[] ParseTuple(int extent = 3)
    {
        if (!_fileContent.IsNext('<'))
            FileParser.ErrorOut($"Expecting an opening angle bracket ('<') but found {_fileContent.Peek()}");

        double[] result = new double[extent];

        for (int index = 0; index < extent; index++)
            result[index] = _fileContent.GetNextDouble();

        if (!_fileContent.IsNext('>'))
            FileParser.ErrorOut($"Expecting a closing angle bracket ('>') but found {_fileContent.Peek()}");

        return result;
    }
}
