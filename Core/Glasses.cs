namespace RayTracer.Core;

/// <summary>
/// This class holds some real optical glasses, by name, with their index of refraction measured at
/// every wavelength rather than at one.
/// <para>
/// The measurements are from refractiveindex.info, which places them in the public domain (CC0), by
/// way of pbrt-v4, which carries the same seven.  BK7 is the everyday crown glass of lenses and windows;
/// FK51A a fluor crown that spreads colors very little; BAF10 and LASF9 dense barium and lanthanum
/// glasses that bend light hard; SF5, SF10 and SF11 heavy flints, the glasses a prism is made of when
/// the point is the rainbow.
/// </para>
/// </summary>
public static class Glasses
{
    /// <summary>
    /// This field holds the wavelength, in nanometers, at which an index of refraction means what it
    /// says when only one is given: helium's yellow d line, the reference a glass catalog uses.
    /// </summary>
    public const double ReferenceWavelength = 587.5618;

    /// <summary>
    /// This field holds the wavelength, in nanometers, of hydrogen's blue F line.
    /// </summary>
    public const double BlueWavelength = 486.1327;

    /// <summary>
    /// This field holds the wavelength, in nanometers, of hydrogen's red C line.
    /// </summary>
    public const double RedWavelength = 656.2725;

    /// <summary>
    /// This property holds the names of the glasses known here.
    /// </summary>
    public static IEnumerable<string> Names => Tables.Keys.Order(StringComparer.Ordinal);

    // Wavelength in nanometers and index, in turn.
    private static readonly Dictionary<string, double[]> Tables = new (StringComparer.OrdinalIgnoreCase)
    {
        ["BK7"] =
        [
            300, 1.5527702635739, 322, 1.5458699289209, 344, 1.5404466868331,
            366, 1.536090527917, 388, 1.53252773217, 410, 1.529568767224,
            432, 1.5270784291406, 454, 1.5249578457324, 476, 1.5231331738499,
            498, 1.5215482528369, 520, 1.5201596882463, 542, 1.5189334783109,
            564, 1.5178426478869, 586, 1.516865556749, 608, 1.5159846691816,
            630, 1.5151856452759, 652, 1.5144566604975, 674, 1.513787889767,
            696, 1.5131711117948, 718, 1.5125994024544, 740, 1.5120668948646,
            762, 1.5115685899969, 784, 1.5111002059336, 806, 1.5106580569705,
            828, 1.5102389559626, 850, 1.5098401349174, 872, 1.5094591800239,
            894, 1.5090939781792, 916, 1.5087426727363
        ],
        ["BAF10"] =
        [
            350, 1.7126880848268, 371, 1.7044510025682, 393, 1.6978539633931,
            414, 1.6924597573902, 436, 1.6879747521657, 457, 1.6841935148947,
            479, 1.6809676313681, 500, 1.6781870617363, 522, 1.6757684467878,
            543, 1.6736474831891, 565, 1.6717737892968, 586, 1.6701073530462,
            608, 1.6686160168249, 629, 1.6672736605352, 651, 1.6660588657981,
            672, 1.6649539185393, 694, 1.6639440538738, 715, 1.6630168772865,
            737, 1.6621619159417, 758, 1.6613702672977, 780, 1.6606343213443,
            801, 1.6599475391478, 823, 1.6593042748862, 844, 1.6586996317841,
            866, 1.6581293446924, 887, 1.6575896837763, 909, 1.6570773750475
        ],
        ["FK51A"] =
        [
            290, 1.5145777204082, 312, 1.5092112868865, 334, 1.5049961987453,
            356, 1.5016153970446, 378, 1.4988558885761, 400, 1.496569610433,
            422, 1.4946506898002, 444, 1.4930216011953, 466, 1.4916244098644,
            488, 1.49041505042, 511, 1.4893594837084, 533, 1.4884310526027,
            555, 1.4876086240083, 577, 1.486875258765, 599, 1.486217243501,
            621, 1.4856233753353, 643, 1.4850844262039, 665, 1.4845927367446,
            687, 1.484141904927, 709, 1.483726544853, 732, 1.4833420981287,
            754, 1.4829846850495, 776, 1.482650986233, 798, 1.4823381477539,
            820, 1.4820437045732, 842, 1.4817655183243, 864, 1.481501726448,
            886, 1.4812507003621, 908, 1.4810110108734
        ],
        ["LASF9"] =
        [
            370, 1.9199725545705, 391, 1.9057858245373, 412, 1.8945401582481,
            433, 1.8854121949451, 455, 1.877863643024, 476, 1.8715257028176,
            497, 1.8661362648008, 519, 1.8615034773283, 540, 1.8574834752011,
            561, 1.8539661699122, 583, 1.8508658556, 604, 1.8481148099285,
            625, 1.8456588222442, 646, 1.8434539988324, 668, 1.8414644361915,
            689, 1.8396604975285, 710, 1.8380175167434, 732, 1.8365148106821,
            753, 1.8351349171703, 774, 1.8338630007484, 796, 1.8326863845545,
            817, 1.8315941782006, 838, 1.8305769794709, 859, 1.8296266333424,
            881, 1.8287360359155, 902, 1.8278989738228
        ],
        ["SF5"] =
        [
            370, 1.7286549847245, 391, 1.7170151864402, 412, 1.7079037179421,
            433, 1.7005724270177, 455, 1.6945472844297, 476, 1.6895110487297,
            497, 1.685242265691, 519, 1.6815810964, 540, 1.678409006027,
            561, 1.6756360973958, 583, 1.6731928929908, 604, 1.6710248234743,
            625, 1.6690884260039, 646, 1.6673486579281, 668, 1.6657769585173,
            689, 1.6643498246044, 710, 1.6630477468358, 732, 1.6618544037398,
            753, 1.6607560432197, 774, 1.6597410023473, 796, 1.6587993305922,
            817, 1.6579224913632, 838, 1.6571031234995, 859, 1.6563348491305,
            881, 1.6556121177295, 902, 1.654930078671
        ],
        ["SF10"] =
        [
            380, 1.7905788948419, 401, 1.7776074571692, 422, 1.7673620572474,
            443, 1.7590649148507, 464, 1.7522127524444, 486, 1.7464635698826,
            507, 1.741575877046, 528, 1.7373738218659, 549, 1.7337260730259,
            570, 1.7305324562829, 592, 1.7277151818026, 613, 1.7252129043714,
            634, 1.7229765939984, 655, 1.7209665988467, 676, 1.719150514229,
            698, 1.7175016091415, 719, 1.7159976462946, 740, 1.7146199848831,
            761, 1.7133528897994, 782, 1.7121829937648, 804, 1.7110988742233,
            825, 1.7100907173852, 846, 1.7091500491754, 867, 1.7082695180523,
            888, 1.7074427184169, 910, 1.7066640460471
        ],
        ["SF11"] =
        [
            370, 1.8700216173234, 391, 1.8516255860581, 412, 1.8374707714715,
            433, 1.8262323798466, 455, 1.8170946940119, 476, 1.8095242343848,
            497, 1.803155581666, 519, 1.7977291183308, 540, 1.7930548640505,
            561, 1.7889903663666, 583, 1.7854266026774, 604, 1.7822786683156,
            625, 1.7794794394722, 646, 1.7769751487395, 668, 1.7747222267051,
            689, 1.7726850031375, 710, 1.770834004936, 732, 1.7691446766161,
            753, 1.7675964052635, 774, 1.7661717683505, 796, 1.764855947008,
            817, 1.7636362637211, 838, 1.7625018146862, 859, 1.7614431749629,
            881, 1.7604521601554, 902, 1.7595216323879
        ]
    };

    /// <summary>
    /// This method reports whether a glass of the given name is known.  Names are not case sensitive.
    /// </summary>
    /// <param name="name">The glass's name.</param>
    /// <returns><c>true</c>, if it is known, or <c>false</c>, if not.</returns>
    public static bool IsKnown(string name)
    {
        return name is not null && Tables.ContainsKey(name);
    }

    /// <summary>
    /// This method returns a glass's index of refraction at the given wavelength, read between its
    /// measurements in a straight line, and held at its first or last beyond them.
    /// </summary>
    /// <param name="name">The glass's name.</param>
    /// <param name="wavelength">The wavelength, in nanometers.</param>
    /// <returns>The index of refraction there.</returns>
    public static double IndexAt(string name, double wavelength)
    {
        double[] table = Tables[name];
        int last = table.Length - 2;

        if (wavelength <= table[0])
            return table[1];

        if (wavelength >= table[last])
            return table[last + 1];

        int index = 0;

        while (table[index + 2] < wavelength)
            index += 2;

        double fraction = (wavelength - table[index]) / (table[index + 2] - table[index]);

        return table[index + 1] + fraction * (table[index + 3] - table[index + 1]);
    }

    /// <summary>
    /// This method returns the index of refraction at the given wavelength of a substance described
    /// the way a glass catalog describes one: its index at <see cref="ReferenceWavelength"/>, and its
    /// Abbe number, which says how little it spreads colors -- the index less one, over how much the
    /// index falls from <see cref="BlueWavelength"/> to <see cref="RedWavelength"/>.  Crown glass is
    /// about sixty, flint about thirty-five, diamond fifty-five; the lower the number, the wider the
    /// rainbow.
    /// <para>
    /// Between those lines the index is taken to follow Cauchy's law, <c>A + B/λ²</c>, which is close
    /// to what clear substances do across the visible range and is fixed entirely by the two numbers.
    /// </para>
    /// </summary>
    /// <param name="index">The index of refraction at the reference wavelength.</param>
    /// <param name="abbeNumber">The Abbe number; it must be more than nothing.</param>
    /// <param name="wavelength">The wavelength, in nanometers.</param>
    /// <returns>The index of refraction there.</returns>
    public static double CauchyIndexAt(double index, double abbeNumber, double wavelength)
    {
        double spread = 1 / (BlueWavelength * BlueWavelength) - 1 / (RedWavelength * RedWavelength);
        double b = (index - 1) / abbeNumber / spread;
        double a = index - b / (ReferenceWavelength * ReferenceWavelength);

        return a + b / (wavelength * wavelength);
    }
}
