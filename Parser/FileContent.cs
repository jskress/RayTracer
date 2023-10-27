using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Parser;

/// <summary>
/// This class wraps the text content of a file to parse and handles parsing low-level
/// primitives.
/// </summary>
internal class FileContent
{
    private readonly string _content;
    private readonly int _length;
    private readonly TupleParser _tupleParser;

    private int _cp;

    internal FileContent(string content)
    {
        _content = content;
        _length = content.Length;
        _tupleParser = new TupleParser(this);
        _cp = 0;
    }

    /// <summary>
    /// This method returns the next non-whitespace character in the content without
    /// moving consuming it.
    /// </summary>
    /// <returns>The next non-whitespace character.</returns>
    internal char Peek()
    {
        EatWhiteSpace();

        return _cp < _length ? _content[_cp] : ' ';
    }

    /// <summary>
    /// This method checks to see if the next non-whitespace character matches the one
    /// given.  If so, it is consumed.
    /// </summary>
    /// <param name="ch">The character to check for.</param>
    /// <returns><c>true</c>, if the next content character matches the one specified, or
    /// <c>false</c>, if not.</returns>
    internal bool IsNext(char ch)
    {
        char next = Peek();

        if (next == ch)
        {
            _cp++;

            return true;
        }

        return false;
    }

    /// <summary>
    /// This method reads the next word from the content.  Words are composed of letters
    /// only, of either case.  If we've hit the end of the content, then we will return
    /// <c>null</c>.  If what's next in the content is not a word, that's an error and
    /// parsing stops.
    /// </summary>
    /// <param name="required">Whether the word is required or not.</param>
    /// <returns>The next word, or <c>null</c>.</returns>
    internal string GetNextWord(bool required = false)
    {
        return GetNextThing("a word", char.IsLetter, required);
    }

    /// <summary>
    /// This method parses an <c>int</c> from the content.  It is assumed the int is
    /// required.
    /// </summary>
    /// <param name="min">An optional minimum allowed value.</param>
    /// <param name="max">An optional maximum allowed value.</param>
    /// <returns>The parsed <c>int</c>.</returns>
    internal int GetNextInt(int min = int.MinValue, int max = int.MaxValue)
    {
        string text = GetNextThing(
            "an integer",
            ch => char.IsDigit(ch) || "+-".Contains(ch),
            true);

        if (!int.TryParse(text, out int value))
            FileParser.ErrorOut($"The text, '{text}', is not an int");

        if (value < min)
            FileParser.ErrorOut($"The value, {value}, cannot be smaller than {min}");

        if (value > max)
            FileParser.ErrorOut($"The value, {value}, cannot be larger than {max}");

        return value;
    }

    /// <summary>
    /// This method parses a <c>double</c> from the content.  It is assumed the double is
    /// required.
    /// </summary>
    /// <returns>The parsed <c>int</c>.</returns>
    internal double GetNextDouble()
    {
        string text = GetNextThing(
            "a number",
            ch => char.IsDigit(ch) || "+-eE.".Contains(ch),
            true);

        if (!double.TryParse(text, out double value))
            FileParser.ErrorOut($"The text, '{text}', is not a number");

        return value;
    }

    /// <summary>
    /// This method returns the next point from the content.
    /// </summary>
    /// <returns>The next point from the content.</returns>
    internal Point GetNextPoint()
    {
        return _tupleParser.ParsePoint();
    }

    /// <summary>
    /// This method returns the next vector from the content.
    /// </summary>
    /// <param name="canBeDirection">Whether the vector can be a named direction.</param>
    /// <returns>The next vector from the content.</returns>
    internal Vector GetNextVector(bool canBeDirection)
    {
        return _tupleParser.ParseVector(canBeDirection);
    }

    /// <summary>
    /// This method returns the next color from the content.
    /// </summary>
    /// <returns>The next color from the content.</returns>
    internal Color GetNextColor()
    {
        return _tupleParser.ParseColor();
    }

    /// <summary>
    /// This method returns the next quoted string from the content.  It is assumed that
    /// the string is required.
    /// </summary>
    /// <returns>The next string from the content.</returns>
    internal string GetNextQuotedString()
    {
        if (!IsNext('\''))
            FileParser.ErrorOut($"Expecting a single quote here but found {Peek()}");

        int start = _cp;

        while (_cp < _length)
        {
            char ch = _content[_cp];

            if (ch == '\'')
            {
                _cp++;

                if (_cp >= _length || _content[_cp] != '\'')
                    break;
            }

            _cp++;
        }

        return _content[start..(_cp - 1)].Replace("''", "'");
    }

    /// <summary>
    /// This method reads the next word from the content.  Words are composed of letters
    /// only, of either case.  If we've hit the end of the content, then we will return
    /// <c>null</c>.  If what's next in the content is not a word, that's an error and
    /// parsing stops.
    /// </summary>
    /// <param name="noun">A text description of what we are expecting; used in errors.</param>
    /// <param name="matcher">The matcher function to use.</param>
    /// <param name="required">Whether the thing is required or not.</param>
    /// <returns>The next word, or <c>null</c>.</returns>
    private string GetNextThing(string noun, Func<char, bool> matcher, bool required)
    {
        EatWhiteSpace();

        if (_cp >= _length)
        {
            if (required)
                FileParser.ErrorOut($"Unexpected end of input");

            return null;
        }

        int start = _cp;
        int end = FindEnd(matcher);

        if (start == end)
            FileParser.ErrorOut($"Expecting {noun} but found {_content[start]}");

        _cp = end;

        return _content[start..end];
    }

    /// <summary>
    /// This is a helper method for eating whitespace.
    /// </summary>
    private void EatWhiteSpace()
    {
        _cp = FindEnd(char.IsWhiteSpace);
    }

    /// <summary>
    /// This method returns the index of the first character that the given matching
    /// function returns <c>false</c> for.
    /// </summary>
    /// <param name="matcher">The matching function to use.</param>
    /// <returns>The index of the first character that the matcher doesn't like.</returns>
    private int FindEnd(Func<char, bool> matcher)
    {
        int index = _cp;

        while (index < _length && matcher.Invoke(_content[index]))
            index++;

        return index;
    }
}
