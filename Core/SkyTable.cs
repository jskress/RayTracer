using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class holds a sky that has already been worked out, so that looking one up is a matter of
/// reading rather than of marching through the air again.
/// <para>
/// This is not a speed-up bolted on afterward; without it the feature would not be usable at all.  A
/// sky light looks at the sky in some dozens of directions for every point it shades, and every one
/// of those would otherwise walk out through the atmosphere and, at every step of that walk, walk
/// back toward the sun -- millions of marches for a single frame.  What makes a table sound here is
/// that a sky depends on nothing but which way you look: not where you stand, not what time in the
/// render it is, not what else is in the scene.
/// </para>
/// <para>
/// Two savings come for free from the shape of the thing.  The sky is the same on either side of the
/// plane through the sun and straight up, so only half a turn need be kept.  And the rows are spread
/// by the square root of the height in the sky rather than evenly, which puts most of them near the
/// horizon -- where a view skims through the most air and the color changes fastest -- and few near
/// the zenith, where very little happens over a great many degrees.
/// </para>
/// </summary>
public class SkyTable
{
    private readonly Color[] _entries;
    private readonly int _rows;
    private readonly int _columns;
    private readonly int _horizon;
    private readonly Vector _towardSun;

    /// <summary>
    /// This constructor works the whole sky out, once.
    /// </summary>
    /// <param name="air">The air to look through.</param>
    /// <param name="towardSun">Which way the sun lies.</param>
    /// <param name="height">How far above the ground the viewer stands, in metres.</param>
    /// <param name="rows">How many heights in the sky to work out.</param>
    /// <param name="columns">How many ways round to work out.</param>
    public SkyTable(Atmosphere air, Vector towardSun, double height, int rows = 97, int columns = 64)
    {
        // An odd count puts a row exactly on the horizon, which matters: there is a real step in the
        // sky there, a view just above it running out to space and one just below it running into the
        // ground within moments.  Mixing across that step would smear the ground's darkness up into
        // the sky and the sky's light down into the ground.
        _rows = rows | 1;
        _columns = columns;
        _horizon = (_rows - 1) / 2;
        _towardSun = towardSun.Unit;
        _entries = new Color[_rows * _columns];

        // Which way round the sun lies, so that a direction can be measured against it.  Straight up
        // it has no way round at all, and then the sky is the same all the way round, so anything
        // will do.
        Vector sunAround = FlatPartOf(_towardSun);

        // Worked out before the sky and from the once-turned light alone, since it is the light that
        // has been turned again and cannot be built out of itself.
        air.Bounced ??= new MultipleScattering(air.Turbidity);

        for (int row = 0; row < _rows; row++)
        {
            double sine = SineAtRow(row);
            double cosine = Math.Sqrt(Math.Max(0, 1 - sine * sine));

            for (int column = 0; column < _columns; column++)
            {
                double around = Math.PI * (_columns == 1 ? 0 : (double) column / (_columns - 1));
                Vector view = TurnedFrom(sunAround, around, sine, cosine);

                _entries[row * _columns + column] =
                    SpectralColor.ToColor(air.RadianceToward(view, _towardSun, height));
            }
        }
    }

    /// <summary>
    /// This method returns the color of the sky in the given direction.
    /// </summary>
    /// <param name="direction">Which way to look.</param>
    /// <returns>The color of the sky that way.</returns>
    public Color Toward(Vector direction)
    {
        Vector looking = direction.Unit;
        double row = RowAtSine(looking.Y) * (_rows - 1);
        double column = WayRoundOf(looking) / Math.PI * (_columns - 1);

        // Kept to one side of the horizon or the other, so that the step there is read as the step it
        // is rather than being blurred across.
        return looking.Y >= 0
            ? Between(row, column, _horizon, _rows - 1)
            : Between(row, column, 0, _horizon);
    }

    /// <summary>
    /// This method reads the table at a place that will generally fall between entries, mixing the
    /// four around it in proportion to how near each one is.
    /// </summary>
    /// <param name="row">Which row is wanted, possibly between two.</param>
    /// <param name="column">Which column is wanted, possibly between two.</param>
    /// <param name="firstRow">The lowest row it may be read from.</param>
    /// <param name="lastRow">The highest row it may be read from.</param>
    /// <returns>The color there.</returns>
    private Color Between(double row, double column, int firstRow, int lastRow)
    {
        int lowRow = Math.Clamp((int) Math.Floor(row), firstRow, lastRow);
        int highRow = Math.Min(lowRow + 1, lastRow);
        int lowColumn = Math.Clamp((int) Math.Floor(column), 0, _columns - 1);
        int highColumn = Math.Min(lowColumn + 1, _columns - 1);
        double downRow = Math.Clamp(row - lowRow, 0, 1);
        double alongColumn = Math.Clamp(column - lowColumn, 0, 1);

        Color topLeft = _entries[lowRow * _columns + lowColumn];
        Color topRight = _entries[lowRow * _columns + highColumn];
        Color bottomLeft = _entries[highRow * _columns + lowColumn];
        Color bottomRight = _entries[highRow * _columns + highColumn];
        Color top = Mix(topLeft, topRight, alongColumn);
        Color bottom = Mix(bottomLeft, bottomRight, alongColumn);

        return Mix(top, bottom, downRow);
    }

    /// <summary>
    /// This method mixes two colors in the given proportion.
    /// </summary>
    /// <param name="first">The color at nothing.</param>
    /// <param name="second">The color at one.</param>
    /// <param name="howFar">How far between them to go.</param>
    /// <returns>The mixed color.</returns>
    private static Color Mix(Color first, Color second, double howFar)
    {
        return new Color(
            first.Red + (second.Red - first.Red) * howFar,
            first.Green + (second.Green - first.Green) * howFar,
            first.Blue + (second.Blue - first.Blue) * howFar);
    }

    /// <summary>
    /// This method returns where in the table a given height in the sky falls, from nothing at
    /// straight down to one at straight up.
    /// <para>
    /// The spread is by the square root of the height rather than by the height itself, which crowds
    /// the rows toward the horizon.  That is where they are wanted: over the ten degrees above the
    /// horizon a sky changes more than it does over the eighty degrees above that.
    /// </para>
    /// </summary>
    /// <param name="sine">How high in the sky, as the sine of the angle above the horizon.</param>
    /// <returns>Where that falls in the table, from nothing to one.</returns>
    private static double RowAtSine(double sine)
    {
        double climbed = Math.Sqrt(Math.Abs(Math.Clamp(sine, -1, 1)));

        return 0.5 + 0.5 * Math.Sign(sine) * climbed;
    }

    /// <summary>
    /// This method returns the height in the sky a given row stands for, which is the undoing of
    /// <see cref="RowAtSine"/>.
    /// </summary>
    /// <param name="row">The row in question.</param>
    /// <returns>The sine of the angle above the horizon there.</returns>
    private double SineAtRow(int row)
    {
        double along = _rows == 1 ? 0.5 : (double) row / (_rows - 1);
        double fromMiddle = 2 * along - 1;

        return Math.Sign(fromMiddle) * fromMiddle * fromMiddle;
    }

    /// <summary>
    /// This method returns how far round from the sun a direction lies, from nothing when it is the
    /// way the sun is to half a turn when it is straight away from it.
    /// </summary>
    /// <param name="direction">The direction in question.</param>
    /// <returns>How far round from the sun, in radians.</returns>
    private double WayRoundOf(Vector direction)
    {
        Vector sunAround = FlatPartOf(_towardSun);
        Vector viewAround = FlatPartOf(direction);

        // Either straight up or straight down has no way round, and there the sky is the same all the
        // way round in any case.
        if (sunAround is null || viewAround is null)
            return 0;

        return Math.Acos(Math.Clamp(sunAround.Dot(viewAround), -1, 1));
    }

    /// <summary>
    /// This method returns the part of a direction that lies flat, made a unit long, or <c>null</c>
    /// when the direction is straight up or straight down and so has no flat part at all.
    /// </summary>
    /// <param name="direction">The direction in question.</param>
    /// <returns>Which way round it points.</returns>
    private static Vector FlatPartOf(Vector direction)
    {
        double length = Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);

        return length < 1e-9 ? null : new Vector(direction.X / length, 0, direction.Z / length);
    }

    /// <summary>
    /// This method builds the direction that stands at a given height in the sky and a given way
    /// round from the sun.
    /// </summary>
    /// <param name="sunAround">Which way round the sun lies, or <c>null</c> if it is straight up.</param>
    /// <param name="around">How far round from the sun, in radians.</param>
    /// <param name="sine">The sine of the height above the horizon.</param>
    /// <param name="cosine">The cosine of the height above the horizon.</param>
    /// <returns>The direction that stands there.</returns>
    private static Vector TurnedFrom(Vector sunAround, double around, double sine, double cosine)
    {
        Vector toward = sunAround ?? new Vector(1, 0, 0);
        Vector aside = new (-toward.Z, 0, toward.X);
        double alongSun = Math.Cos(around);
        double alongAside = Math.Sin(around);

        return new Vector(
            cosine * (toward.X * alongSun + aside.X * alongAside),
            sine,
            cosine * (toward.Z * alongSun + aside.Z * alongAside)).Unit;
    }
}
