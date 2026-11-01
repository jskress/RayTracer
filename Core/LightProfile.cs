using System.Collections.Concurrent;
using System.Globalization;
using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class holds a light's profile, read from an IES file (IESNA LM-63): how bright a real light fitting is in every
/// direction round it, measured by its maker on a grid of angles.  It is what gives a downlight its scalloped arcs up
/// a wall and a wall-washer its lopsided wash.
/// <para>
/// **Only its shape is kept.**  The file's candelas are scaled so that the brightest way is one, and the light the
/// profile is given to goes on saying how bright it is with its color, its temperature and its brightness, as any
/// light does: a scene's lights carry no unit, and fade with distance only when told, so the file's absolute numbers
/// would mean nothing in it.
/// </para>
/// <para>
/// **The angles are photometry's Type C**, which every architectural fitting is measured by: a vertical angle from
/// nought straight down to 180 straight up, and a horizontal angle round the vertical, from nought along the fitting's
/// length, counterclockwise seen from above.  A fitting is measured over as little of that as its symmetry allows --
/// one plane for a round one, a quarter, a half, or the whole way round -- and the rest is the same mirrored.  Types A
/// and B, which car lamps and floodlights are measured by, are refused.
/// </para>
/// </summary>
public sealed class LightProfile
{
    private static readonly ConcurrentDictionary<string, LightProfile> Profiles = new ();

    // The vertical angles, rising, in degrees; the horizontal ones, rising, in degrees; and the intensity at each,
    // for each horizontal angle the vertical ones in order, the brightest one.
    private readonly double[] _vertical;
    private readonly double[] _horizontal;
    private readonly double[][] _intensity;

    private LightProfile(double[] vertical, double[] horizontal, double[][] intensity)
    {
        _vertical = vertical;
        _horizontal = horizontal;
        _intensity = intensity;
    }

    /// <summary>
    /// This method returns the profile in the named file, read once however many lights name it.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The profile.</returns>
    public static LightProfile From(string path)
    {
        return Profiles.GetOrAdd(Path.GetFullPath(path), full =>
        {
            if (!File.Exists(full))
                throw new Exception($"There is no light profile at {full}.");

            return Parse(File.ReadAllText(full), Path.GetFileName(full));
        });
    }

    /// <summary>
    /// This method reads a profile from the text of an IES file.
    /// <para>
    /// What comes before the line saying <c>TILT=</c> is the maker's description, and is passed over.  After it the
    /// file is numbers, wrapped however the file likes: a tilt table, if the file says <c>TILT=INCLUDE</c>, which is
    /// passed over too; then how many lamps, their lumens, the candela multiplier, how many vertical and horizontal
    /// angles, the photometric type, the units, the fitting's size, the ballast factor, a number kept for the future,
    /// the input watts; then the vertical angles, the horizontal ones, and the candelas, plane by plane.
    /// </para>
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="name">What to call the file in a complaint.</param>
    /// <returns>The profile.</returns>
    public static LightProfile Parse(string text, string name)
    {
        string[] lines = text.Replace("\r", "").Split('\n');
        int tilt = Array.FindIndex(lines, line => line.TrimStart().StartsWith("TILT", StringComparison.OrdinalIgnoreCase));

        if (tilt < 0)
            throw new Exception($"The light profile {name} has no TILT line, so it is not an IES file.");

        string said = lines[tilt].Trim();
        string tiltSays = said[(said.IndexOf('=') + 1)..].Trim().ToUpperInvariant();
        Queue<double> numbers = new (lines
            .Skip(tilt + 1)
            .SelectMany(line => line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries))
            .Select(word => double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                ? number
                : throw new Exception($"The light profile {name} holds \"{word}\" where a number belongs.")));

        double Next() => numbers.Count > 0
            ? numbers.Dequeue()
            : throw new Exception($"The light profile {name} ends before all its numbers are given.");

        // A tilt table: the lamp's geometry, how many angles, the angles, and a multiplier for each.
        if (tiltSays == "INCLUDE")
        {
            Next();

            int angles = (int) Next();

            for (int index = 0; index < 2 * angles; index++)
                Next();
        }

        Next();                                   // how many lamps
        Next();                                   // their lumens
        double multiplier = Next();
        int verticals = (int) Next();
        int horizontals = (int) Next();
        int type = (int) Next();

        Next(); Next(); Next(); Next();           // the units, the width, length and height
        Next(); Next(); Next();                   // the ballast factor, the future, the watts

        if (type != 1)
        {
            throw new Exception(
                $"The light profile {name} is measured by photometric type {(type == 2 ? "B" : "A")}, " +
                "for floodlights or car lamps; only type C, for architectural fittings, is read.");
        }

        if (verticals < 1 || horizontals < 1)
            throw new Exception($"The light profile {name} has no angles to measure at.");

        double[] vertical = Enumerable.Range(0, verticals).Select(_ => Next()).ToArray();
        double[] horizontal = Enumerable.Range(0, horizontals).Select(_ => Next()).ToArray();
        double[][] intensity = Enumerable.Range(0, horizontals)
            .Select(_ => Enumerable.Range(0, verticals).Select(_ => Math.Max(0, Next() * multiplier)).ToArray())
            .ToArray();
        double brightest = intensity.SelectMany(plane => plane).DefaultIfEmpty(0).Max();

        if (brightest <= 0)
            throw new Exception($"The light profile {name} gives no light in any direction.");

        foreach (double[] plane in intensity)
        {
            for (int index = 0; index < plane.Length; index++)
                plane[index] /= brightest;
        }

        return new LightProfile(vertical, horizontal, intensity);
    }

    /// <summary>
    /// This method returns how bright the profile is in a direction, as a share of its brightest.
    /// </summary>
    /// <param name="direction">The direction, from the light.</param>
    /// <param name="down">The profile's straight down: its vertical angle nought.</param>
    /// <param name="along">The profile's horizontal angle nought, square to its straight down.</param>
    /// <returns>The share, from nought to one.</returns>
    public double Toward(Vector direction, Vector down, Vector along)
    {
        Vector unit = direction.Unit;
        Vector across = down.Cross(along);
        double vertical = Math.Acos(Math.Clamp(unit.Dot(down), -1, 1)) * 180 / Math.PI;
        double horizontal = Math.Atan2(unit.Dot(across), unit.Dot(along)) * 180 / Math.PI;

        return At(vertical, horizontal < 0 ? horizontal + 360 : horizontal);
    }

    /// <summary>
    /// This method returns how bright the profile is at a vertical and horizontal angle, as a share of its brightest:
    /// read along straight lines between the angles it was measured at, mirrored as its symmetry says, and dark past
    /// the vertical angles it covers.
    /// </summary>
    /// <param name="vertical">The vertical angle, in degrees, from nought straight down.</param>
    /// <param name="horizontal">The horizontal angle, in degrees, from nought to 360.</param>
    /// <returns>The share, from nought to one.</returns>
    public double At(double vertical, double horizontal)
    {
        if (vertical < _vertical[0] || vertical > _vertical[^1])
            return 0;

        if (_horizontal.Length == 1)
            return Along(_intensity[0], vertical);

        double first = _horizontal[0], last = _horizontal[^1];

        // The halves and quarters a symmetric fitting was measured over, each mirrored to stand for the rest.
        if (first == 0 && last == 90)
        {
            horizontal = horizontal > 180 ? 360 - horizontal : horizontal;
            horizontal = horizontal > 90 ? 180 - horizontal : horizontal;
        }
        else if (first == 0 && last == 180)
            horizontal = horizontal > 180 ? 360 - horizontal : horizontal;
        else if (first == 90 && last == 270)
            horizontal = horizontal < 90 ? 180 - horizontal : horizontal > 270 ? 540 - horizontal : horizontal;

        // Measured the whole way round, the last plane runs on to the first.
        int above = Array.FindIndex(_horizontal, angle => angle >= horizontal);

        if (above < 0)
        {
            if (last >= 360 - 1e-9)
                return Along(_intensity[^1], vertical);

            double share = (horizontal - last) / (360 + first - last);

            return Along(_intensity[^1], vertical) * (1 - share) + Along(_intensity[0], vertical) * share;
        }

        if (above == 0)
            return Along(_intensity[0], vertical);

        double from = _horizontal[above - 1], to = _horizontal[above];
        double t = to > from ? (horizontal - from) / (to - from) : 0;

        return Along(_intensity[above - 1], vertical) * (1 - t) + Along(_intensity[above], vertical) * t;
    }

    /// <summary>
    /// This method reads one measured plane at a vertical angle, along a straight line between the two angles round it.
    /// </summary>
    private double Along(double[] plane, double vertical)
    {
        int above = Array.FindIndex(_vertical, angle => angle >= vertical);

        if (above <= 0)
            return plane[0];

        double from = _vertical[above - 1], to = _vertical[above];
        double t = to > from ? (vertical - from) / (to - from) : 0;

        return plane[above - 1] * (1 - t) + plane[above] * t;
    }
}
