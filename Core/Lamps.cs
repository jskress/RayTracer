using System.Globalization;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class holds the light some real lamps give, by name: not the color they come out, but how much they give at
/// every wavelength, which is what decides how the colors they fall on come out.
/// <para>
/// **A lamp that is not something hot glowing does not give a glow's smooth spread of light.**  A low pressure sodium
/// lamp gives nearly all of its light at one yellow wavelength, so that under it a red car and a blue one are only
/// darker and lighter yellow; a high pressure one a hump about that yellow with little blue in it; a fluorescent tube
/// mercury's few sharp lines and a phosphor's humps; a white LED a blue spike and a broad yellow hump.  Two lamps of the same color may make the same paint two different colors, and
/// only light carried wavelength by wavelength (a spectral render) shows it; in red, green and blue a lamp is its
/// color.
/// </para>
/// <para>
/// The fluorescent tubes, the high pressure sodium and metal halide lamps and the LEDs are the CIE's measurements
/// (CIE 015:2018), read from the tables it publishes, kept unchanged under their license in <c>Data/CIE</c>.  The
/// two the CIE does not tabulate are built from the strong lines of sodium and of mercury in NIST's Handbook of
/// Basic Atomic Spectroscopic Data, each line's light put in the band it falls in: low pressure sodium, 98% of it
/// sodium's two yellow D lines, and low pressure mercury, its violet, blue, green and yellow lines with
/// nothing between, which glows blue-violet.  NIST's intensities are of a discharge at low pressure; a mercury street
/// lamp runs at high pressure, which strengthens its green and yellow and fills in between, and comes out bluish
/// white -- no measured table of one is published openly, so it is not here.
/// </para>
/// </summary>
public static class Lamps
{
    // Sodium's lines from 380 to 780 nm, air wavelengths in nanometers and relative intensities, from NIST's
    // Handbook of Basic Atomic Spectroscopic Data, "Strong Lines of Sodium" (Na I).
    private static readonly double[] SodiumLines =
    [
        432.462, 1, 439.003, 1, 439.334, 1, 449.418, 1, 449.766, 1, 466.4811, 2, 466.8560, 2, 497.8541, 2,
        498.2813, 5, 514.8838, 1, 515.3402, 1, 568.2633, 4, 568.8193, 1, 568.8205, 7, 588.9950, 1000,
        589.5924, 500, 615.4225, 2, 616.0747, 3
    ];

    // Mercury's lines from 380 to 780 nm, as sodium's are, from "Strong Lines of Mercury" (Hg I).
    private static readonly double[] MercuryLines =
    [
        404.6563, 400, 433.9223, 60, 434.7494, 100, 435.8328, 1000, 546.0735, 500, 576.9598, 50, 579.0663, 60,
        708.190, 25
    ];

    // What each name is: a column of one of the CIE's tables, or lines.  The names everyday words give a lamp come
    // first, then the CIE's own.
    private static readonly Dictionary<string, Func<double[]>> Sources = new (StringComparer.OrdinalIgnoreCase)
    {
        ["low pressure sodium"] = () => FromLines(SodiumLines),
        ["high pressure sodium"] = () => FromTable("HPs", "HP1"),
        ["enhanced high pressure sodium"] = () => FromTable("HPs", "HP2"),
        ["low pressure mercury"] = () => FromLines(MercuryLines),
        ["warm metal halide"] = () => FromTable("HPs", "HP3"),
        ["metal halide"] = () => FromTable("HPs", "HP4"),
        ["fluorescent"] = () => FromTable("FLs", "FL2"),
        ["warm white fluorescent"] = () => FromTable("FLs", "FL4"),
        ["daylight fluorescent"] = () => FromTable("FLs", "FL7"),
        ["triphosphor fluorescent"] = () => FromTable("FLs", "FL11"),
        ["warm white LED"] = () => FromTable("LEDs", "LED-B2"),
        ["neutral white LED"] = () => FromTable("LEDs", "LED-B3"),
        ["cool white LED"] = () => FromTable("LEDs", "LED-B4"),
        ["daylight LED"] = () => FromTable("LEDs", "LED-B5"),
        ["RGB LED"] = () => FromTable("LEDs", "LED-RGB1")
    };

    // The headings of the CIE's tables' columns after the wavelength, in the order the datasets' metadata names them.
    private static readonly Dictionary<string, string[]> Headings = new ()
    {
        ["FLs"] = Enumerable.Range(1, 12).Select(index => $"FL{index}")
            .Concat(Enumerable.Range(1, 15).Select(index => $"FL3.{index}")).ToArray(),
        ["HPs"] = Enumerable.Range(1, 5).Select(index => $"HP{index}").ToArray(),
        ["LEDs"] = ["LED-B1", "LED-B2", "LED-B3", "LED-B4", "LED-B5", "LED-BH1", "LED-RGB1", "LED-V1", "LED-V2"]
    };

    // The CIE's tables, read when first asked for: each column by its heading, at 380 to 780 nm in 5 nm steps.
    private static readonly Dictionary<string, Dictionary<string, double[]>> Tables = [];

    // What each name has been worked out as.
    private static readonly Dictionary<string, (Color, double[])> Known = new (StringComparer.OrdinalIgnoreCase);

    private static readonly Lock Guard = new ();

    static Lamps()
    {
        Everyday = Sources.Keys.ToArray();

        foreach ((string table, string[] columns) in Headings)
        {
            foreach (string column in columns)
                Sources[column] = () => FromTable(table, column);
        }

        // FL1 to FL12 are still known by their old names, F1 to F12.
        for (int index = 1; index <= 12; index++)
        {
            string column = $"FL{index}";

            Sources[$"F{index}"] = () => FromTable("FLs", column);
        }

        Names = Sources.Keys.ToList();
    }

    /// <summary>
    /// This property holds the names of the lamps known here: everyday names first, then the CIE's.
    /// </summary>
    public static IReadOnlyList<string> Names { get; }

    /// <summary>
    /// This property holds the lamps known here as a refusal lists them: the everyday names, and the CIE's in runs.
    /// </summary>
    public static string Listed =>
        string.Join(", ", Everyday.Select(name => $"'{name}'")) +
        ", or one of the CIE's own: FL1 to FL12 (or F1 to F12), FL3.1 to FL3.15, HP1 to HP5, LED-B1 to LED-B5, " +
        "LED-BH1, LED-RGB1, LED-V1 or LED-V2";

    // The names everyday words give the lamps, as against the CIE's own.
    private static readonly string[] Everyday;

    /// <summary>
    /// This method notes whether a name is a lamp known here, whatever its case.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><c>true</c>, if it is.</returns>
    public static bool Knows(string name) => name is not null && Sources.ContainsKey(name);

    /// <summary>
    /// This method returns the light a lamp gives, as a color and band by band (see <see cref="SpectralColor"/>), both
    /// scaled so that the brightest channel of the color is one, as a light given a temperature is.  A color no screen
    /// can show -- low pressure sodium's yellow is purer than any -- is brought in as the sky's colors are.
    /// </summary>
    /// <param name="name">The lamp's name, whatever its case.</param>
    /// <returns>The lamp's color, and its light band by band.</returns>
    /// <exception cref="ArgumentException">When no lamp of that name is known.</exception>
    public static (Color Color, double[] PerBand) Named(string name)
    {
        if (!Knows(name))
        {
            throw new ArgumentException(
                $"There is no lamp named \"{name}\"; the lamps known are {Listed}.");
        }

        lock (Guard)
        {
            if (!Known.TryGetValue(name, out (Color Color, double[] PerBand) known))
            {
                double[] perBand = Sources[name]();
                Color color = SpectralColor.ToColor(perBand);
                double brightest = Math.Max(color.Red, Math.Max(color.Green, color.Blue));

                known = (color / brightest, perBand.Select(amount => amount / brightest).ToArray());
                Known[name] = known;
            }

            return (known.Color, known.PerBand.ToArray());
        }
    }

    /// <summary>
    /// This method returns a lamp's light band by band from a column of one of the CIE's tables: the table's points,
    /// five nanometers apart, joined by straight lines and averaged over each band, so that every line the CIE has
    /// put in a five-nanometer step keeps all its light.
    /// </summary>
    private static double[] FromTable(string table, string column)
    {
        double[] values = Column(table, column);
        double[] perBand = new double[SpectralColor.Bands];
        double width = (SpectralColor.LongestWavelength - SpectralColor.ShortestWavelength) / SpectralColor.Bands;

        for (int band = 0; band < SpectralColor.Bands; band++)
        {
            double from = SpectralColor.ShortestWavelength + band * width;

            perBand[band] = Integral(values, from, from + width) / width;
        }

        return perBand;
    }

    /// <summary>
    /// This method integrates a table's points, joined by straight lines, between two wavelengths.
    /// </summary>
    private static double Integral(double[] values, double from, double to)
    {
        const double Step = 5;
        double sum = 0;

        for (int index = 0; index < values.Length - 1; index++)
        {
            double left = SpectralColor.ShortestWavelength + index * Step;
            double right = left + Step;
            double start = Math.Max(left, from);
            double end = Math.Min(right, to);

            if (end <= start)
                continue;

            double AtWavelength(double wavelength) =>
                values[index] + (values[index + 1] - values[index]) * (wavelength - left) / Step;

            sum += (AtWavelength(start) + AtWavelength(end)) / 2 * (end - start);
        }

        return sum;
    }

    /// <summary>
    /// This method returns a lamp's light band by band from its lines: each line's light spread across the band it
    /// falls in.
    /// </summary>
    private static double[] FromLines(double[] lines)
    {
        double[] perBand = new double[SpectralColor.Bands];
        double width = (SpectralColor.LongestWavelength - SpectralColor.ShortestWavelength) / SpectralColor.Bands;

        for (int index = 0; index < lines.Length; index += 2)
        {
            int band = (int) ((lines[index] - SpectralColor.ShortestWavelength) / width);

            if (band is >= 0 and < SpectralColor.Bands)
                perBand[band] += lines[index + 1] / width;
        }

        return perBand;
    }

    /// <summary>
    /// This method returns one column of one of the CIE's tables, reading the table the first time it is asked for.
    /// </summary>
    private static double[] Column(string table, string column)
    {
        if (!Tables.TryGetValue(table, out Dictionary<string, double[]> columns))
        {
            columns = Read(table);
            Tables[table] = columns;
        }

        return columns[column];
    }

    /// <summary>
    /// This method reads one of the CIE's tables, embedded in the build from <c>Data/CIE</c>: a wavelength and then
    /// a number for each lamp on every line, 380 to 780 nm, in the order the dataset's metadata names them.
    /// </summary>
    private static Dictionary<string, double[]> Read(string table)
    {
        string[] headings = Headings[table];
        using Stream stream = typeof(Lamps).Assembly.GetManifestResourceStream($"Lamps.CIE_illum_{table}.csv") ??
                              throw new InvalidOperationException($"This build carries no CIE table of {table}.");
        using StreamReader reader = new (stream);
        List<double[]> rows = [];

        while (reader.ReadLine() is { } line)
        {
            if (line.Trim().Length == 0)
                continue;

            rows.Add(line.Split(',').Select(field => double.Parse(field, CultureInfo.InvariantCulture)).ToArray());
        }

        if (rows.Count != 81 || rows[0][0] != 380 || rows[^1][0] != 780 ||
            rows.Any(row => row.Length != headings.Length + 1))
            throw new InvalidOperationException($"The CIE table of {table} is not the 380 to 780 nm table expected.");

        Dictionary<string, double[]> columns = [];

        for (int index = 0; index < headings.Length; index++)
            columns[headings[index]] = rows.Select(row => row[index + 1]).ToArray();

        return columns;
    }
}
