namespace RayTracer.Graphics;

/// <summary>
/// This class provides the work of parsing a standard SVG path string and produces a
/// <see cref="GeneralPath"/> from it.
/// </summary>
public class SvgPathFactory
{
    private readonly string _pathSpec;

    private GeneralPath _path;
    private int _cp;

    public SvgPathFactory(string pathSpec)
    {
        _pathSpec = pathSpec;
    }

    /// <summary>
    /// This method is used to parse the SVC path we were constructed with into a general
    /// path.
    /// </summary>
    /// <returns>The parsed general path.</returns>
    public GeneralPath Parse()
    {
        ParseInto(new GeneralPath());

        return _path;
    }

    /// <summary>
    /// This method is used to parse the SVC path we were constructed with into the given
    /// general path.
    /// </summary>
    /// <param name="path">The path to parse the SVG path information into.</param>
    public void ParseInto(GeneralPath path)
    {
        char lastCommand = ' ';

        _path = path;
        _cp = 0;

        while (MoreToParse())
            lastCommand = ParseCommand(lastCommand);
    }

    /// <summary>
    /// This method is used to parse the next command from the string.
    /// </summary>
    /// <returns>The command we just parsed.</returns>
    private char ParseCommand(char lastCommand)
    {
        // We check for the close path command since a badly formed spec could cause an
        // infinite parsing loop.
        char command = char.IsLetter(Char()) || lastCommand is 'Z' or 'z'
            ? _pathSpec[_cp++]
            : lastCommand;

        switch (command)
        {
            case 'M':
                ParseMoveTo();
                command = 'L'; // any following coordinates imply lines.
                break;
            case 'm':
                ParseRelativeMoveTo();
                command = 'l'; // any following coordinates imply lines.
                break;
            case 'L':
                ParseLineTo();
                break;
            case 'l':
                ParseRelativeLineTo();
                break;
            case 'H':
                ParseHorizontalLineTo();
                break;
            case 'h':
                ParseRelativeHorizontalLineTo();
                break;
            case 'V':
                ParseVerticalLineTo();
                break;
            case 'v':
                ParseRelativeVerticalLineTo();
                break;
            case 'Q':
                ParseQuadTo();
                break;
            case 'q':
                ParseRelativeQuadTo();
                break;
            case 'T':
                ParseSmoothQuadTo();
                break;
            case 't':
                ParseRelativeSmoothQuadTo();
                break;
            case 'C':
                ParseCurveTo();
                break;
            case 'c':
                ParseRelativeCurveTo();
                break;
            case 'S':
                ParseSmoothCubicTo();
                break;
            case 's':
                ParseRelativeSmoothCubicTo();
                break;
            case 'Z':
            case 'z':
                _path.ClosePath();
                break;
            default:
                throw new ArgumentException($"Invalid SVG command: '{command}'");
        }

        return command;
    }

    /// <summary>
    /// This method parses the absolute "move to" command.
    /// </summary>
    private void ParseMoveTo()
    {
        double[] coordinates = ParseNumbers(2);

        _path.MoveTo(coordinates[0], coordinates[1]);
    }

    /// <summary>
    /// This method parses the relative "move to" command.
    /// </summary>
    private void ParseRelativeMoveTo()
    {
        double[] coordinates = ParseNumbers(2);

        _path.RelativeMoveTo(coordinates[0], coordinates[1]);
    }

    /// <summary>
    /// This method parses the absolute "line to" command.
    /// </summary>
    private void ParseLineTo()
    {
        double[] coordinates = ParseNumbers(2);

        _path.LineTo(coordinates[0], coordinates[1]);
    }

    /// <summary>
    /// This method parses the relative "line to" command.
    /// </summary>
    private void ParseRelativeLineTo()
    {
        double[] coordinates = ParseNumbers(2);

        _path.RelativeLineTo(coordinates[0], coordinates[1]);
    }

    /// <summary>
    /// This method parses the absolute "horizontal line to" command.
    /// </summary>
    private void ParseHorizontalLineTo()
    {
        double coordinate = ParseNumber();

        _path.HorizontalLineTo(coordinate);
    }

    /// <summary>
    /// This method parses the relative "horizontal line to" command.
    /// </summary>
    private void ParseRelativeHorizontalLineTo()
    {
        double coordinate = ParseNumber();

        _path.RelativeHorizontalLineTo(coordinate);
    }

    /// <summary>
    /// This method parses the absolute "vertical line to" command.
    /// </summary>
    private void ParseVerticalLineTo()
    {
        double coordinate = ParseNumber();

        _path.VerticalLineTo(coordinate);
    }

    /// <summary>
    /// This method parses the relative "vertical line to" command.
    /// </summary>
    private void ParseRelativeVerticalLineTo()
    {
        double coordinate = ParseNumber();

        _path.RelativeVerticalLineTo(coordinate);
    }

    /// <summary>
    /// This method parses the absolute "quad curve to" command.
    /// </summary>
    private void ParseQuadTo()
    {
        double[] coordinates = ParseNumbers(4);

        _path.QuadTo(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
    }

    /// <summary>
    /// This method parses the relative "quad to" command.
    /// </summary>
    private void ParseRelativeQuadTo()
    {
        double[] coordinates = ParseNumbers(4);

        _path.RelativeQuadTo(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
    }

    /// <summary>
    /// This method parses the absolute "smooth quad curve to" command.
    /// </summary>
    private void ParseSmoothQuadTo()
    {
        double[] coordinates = ParseNumbers(2);

        _path.SmoothQuadTo(coordinates[0], coordinates[1]);
    }

    /// <summary>
    /// This method parses the relative "quad to" command.
    /// </summary>
    private void ParseRelativeSmoothQuadTo()
    {
        double[] coordinates = ParseNumbers(2);

        _path.RelativeSmoothQuadTo(coordinates[0], coordinates[1]);
    }

    /// <summary>
    /// This method parses the absolute "cubic curve to" command.
    /// </summary>
    private void ParseCurveTo()
    {
        double[] coordinates = ParseNumbers(6);

        _path.CubicTo(
            coordinates[0], coordinates[1], coordinates[2], coordinates[3],
            coordinates[4], coordinates[5]);
    }

    /// <summary>
    /// This method parses the relative "curve to" command.
    /// </summary>
    private void ParseRelativeCurveTo()
    {
        double[] coordinates = ParseNumbers(6);

        _path.RelativeCubicTo(
            coordinates[0], coordinates[1], coordinates[2], coordinates[3],
            coordinates[4], coordinates[5]);
    }

    /// <summary>
    /// This method parses the absolute "smooth cubic curve to" command.
    /// </summary>
    private void ParseSmoothCubicTo()
    {
        double[] coordinates = ParseNumbers(4);

        _path.SmoothCubicTo(
            coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
    }

    /// <summary>
    /// This method parses the relative "cubic to" command.
    /// </summary>
    private void ParseRelativeSmoothCubicTo()
    {
        double[] coordinates = ParseNumbers(4);

        _path.RelativeSmoothCubicTo(
            coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
    }

    /// <summary>
    /// This method is used to parse a series of numbers from the path spec.
    /// </summary>
    /// <param name="count">The number of numbers that need to be parsed.</param>
    /// <returns>The array of numbers that were parsed.</returns>
    private double[] ParseNumbers(int count)
    {
        double[] numbers = new double[count];

        for (int index = 0; index < count; index++)
        {
            numbers[index] = ParseNumber();

            EatWhiteSpace();

            if (index < count - 1 && Char() is ',')
                _cp++;

            EatWhiteSpace();
        }

        return numbers;
    }

    /// <summary>
    /// This method is used to parse a number from the path spec.
    /// </summary>
    /// <returns>The number that was parsed.</returns>
    private double ParseNumber()
    {
        if (!MoreToParse())
            throw new ArgumentException("Expecting a number but found the end of the path specified.");
        
        int start = _cp;

        if (Char() is '+' or '-')
            _cp++;

        while (char.IsDigit(Char()))
            _cp++;

        if (Char() is '.')
            _cp++;

        while (char.IsDigit(Char()))
            _cp++;

        if (Char() is 'e' or 'E')
        {
            _cp++;

            if (Char() is '+' or '-')
                _cp++;

            while (char.IsDigit(Char()))
                _cp++;
        }
        
        if (double.TryParse(_pathSpec[start.._cp], out double value))
            return value;

        throw new ArgumentException($"Invalid SVG number: '{_pathSpec[start.._cp]}'");
    }

    /// <summary>
    /// Access the current character in the path spec.  If the end of the spec has been
    /// reached, the <c>null</c> character is returned.
    /// </summary>
    /// <returns>The current character from the path spec.</returns>
    private char Char()
    {
        return _cp < _pathSpec.Length ? _pathSpec[_cp] : '\0';
    }

    /// <summary>
    /// This method is used to test whether there is any more text to be parsed.
    /// It consumes any leading whitespace in the process.
    /// </summary>
    /// <returns><c>true</c>, if there is more text to parse, or <c>false</c>, if not.</returns>
    private bool MoreToParse()
    {
        EatWhiteSpace();

        return _cp < _pathSpec.Length;
    }

    /// <summary>
    /// This method is used to skip over any whitespace at the current location in the
    /// path spec.
    /// </summary>
    private void EatWhiteSpace()
    {
        while (_cp < _pathSpec.Length && char.IsWhiteSpace(_pathSpec[_cp]))
            _cp++;
    }
}
