using System.Text;
using RayTracer.Extensions;
using RayTracer.Fonts;
using RayTracer.Options;
using Typography.OpenFont;

namespace RayTracer.Commands;

/// <summary>
/// This class provides the implementation of our "fonts" command line verb.
/// </summary>
public static class FontsCommand
{
    private static readonly List<string> FaceDataHeadings = [
        "Weight", "Style", "Glyphs", "Source"
    ];
    private static readonly List<TextAlignment> FaceDataAlignments =
    [
        TextAlignment.Left, TextAlignment.Left, TextAlignment.Right,
        TextAlignment.Left
    ];
    private static readonly List<string> GlyphDataHeadings = [
        "Unicode", "Name", "Display", "Kind", "Index"
    ];
    private static readonly List<TextAlignment> GlyphDataAlignments =
    [
        TextAlignment.Right, TextAlignment.Left, TextAlignment.Left,
        TextAlignment.Left, TextAlignment.Right
    ];
    private static readonly List<string> KerningDataHeadings = [
        "Left", "Kerning", "Right"
    ];
    private static readonly List<TextAlignment> KerningDataAlignments =
    [
        TextAlignment.Right, TextAlignment.Center, TextAlignment.Left
    ];

    /// <summary>
    /// This method provides the meat of our "fonts" command line verb.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    public static void ManageFonts(FontsOptions options)
    {
        if (options.ListFonts)
            ShowExistingFonts();
        else if (options.ShowGlyphs != null)
            ShowFontGlyphs(options.ShowGlyphs);
        else if (options.ShowKerning != null)
            ShowFontKerning(options.ShowKerning);
        else if (options.FetchFontFace != null)
            FetchFontFace(options);
        else if (options.ImportFontFace != null)
            ImportFontFace(options);
        else if (options.AddKerningPair != null)
            AddKerningPair(options);
        else if (options.RemoveKerningPair != null)
            RemoveKerningPair(options);
        else if (options.RemoveFontFace != null)
            RemoveFontFace(options.RemoveFontFace);
        else
            Console.WriteLine("No action was specified.  Use '--help' for a list of options.");
    }

    /// <summary>
    /// This method is used to show all the known fonts to the user.
    /// </summary>
    private static void ShowExistingFonts()
    {
        FontCatalog catalog = FontManager.Instance.Catalog;

        foreach (FontFamily family in catalog.FontFamilies)
        {
            List<List<string>> faceData = FormatFontFaces(family);

            Terminal.Out(family.Name);
            Terminal.Out(faceData, alignments: FaceDataAlignments, hasHeadings: true);
            Terminal.Out("");
        }
    }

    /// <summary>
    /// This method formats the given font family's collection of faces for display.
    /// </summary>
    /// <param name="family">The font family to format the face data for.</param>
    /// <returns>The formatted face data.</returns>
    private static List<List<string>> FormatFontFaces(FontFamily family)
    {
        List<List<string>> result = [FaceDataHeadings];

        result.AddRange(from face in family.Faces
            let glyphCount = FontManager.Instance.GetExistingTypeFace(family, face).GlyphCount
            select (List<string>) [
                Enum.GetName(typeof(FontWeight), face.Weight) ?? face.Weight.ToString(),
                face.Italic ? "Italic" : "Normal",
                glyphCount.ToString("n0"),
                face.IsLocal ? "Local " : "Google"
            ]);

        return result;
    }

    /// <summary>
    /// This method is used to dump the information for all the glyphs in the given font
    /// face.
    /// </summary>
    /// <param name="id">The identifier for the font face to dump the glyph information for.</param>
    private static void ShowFontGlyphs(FaceIdentifier id)
    {
        Typeface typeface = FontManager.Instance.GetExistingTypeFace(id);
        List<List<string>> glyphData = FormatGlyphData(typeface);

        Terminal.Out(id.ForDisplay());
        Terminal.Out(glyphData, alignments: GlyphDataAlignments, hasHeadings: true);
        Terminal.Out("");
    }

    /// <summary>
    /// This method formats the given typeface's collection of glyphs for display.
    /// </summary>
    /// <param name="typeface">The typeface to format the glyph data for.</param>
    /// <returns>The formatted glyph data.</returns>
    private static List<List<string>> FormatGlyphData(Typeface typeface)
    {
        Dictionary<int, ushort> codePontMap = typeface.GetCodePointsMap();
        List<int> codePoints = codePontMap.Keys
            .OrderBy(codePoint => codePoint)
            .ToList();
        List<List<string>> result = [GlyphDataHeadings];

        result.AddRange(from codePoint in codePoints
            let unicode = $"\\u{codePoint:X4}"
            let glyph = typeface.Lookup(codePoint)
            let name = glyph.GetCff1GlyphData()?.Name ?? "(unknown)"
            let display = ToText(codePoint)
            select (List<string>) [
                unicode, name, display, glyph.GlyphClass.ToString(),
                glyph.GlyphIndex.ToString()
            ]);

        return result;
    }

    /// <summary>
    /// This method is used to convert a code point into a display string.
    /// </summary>
    /// <param name="codePoint">The code point to convert.</param>
    /// <returns>The code point as a string.</returns>
    private static string ToText(int codePoint)
    {
        try
        {
            Rune rune = new Rune(codePoint);

            return $"'{rune}'";
        }
        catch (ArgumentOutOfRangeException)
        {
            return codePoint.ToString();
        }
    }

    /// <summary>
    /// This method is used to dump the kerning information from our font face catalog for
    /// the given font face.
    /// </summary>
    /// <param name="id">The ID of the font face.</param>
    private static void ShowFontKerning(FaceIdentifier id)
    {
        Kerning kerning = FontManager.Instance.GetFontFace(id).GetKerning();

        Terminal.Out(id.ForDisplay());

        if (kerning.IsEmpty)
            Terminal.Out("  There is no stored kerning data for the given font face.");
        else
        {
            List<List<string>> kerningData = FormatKerningData(kerning);

            Terminal.Out(kerningData, alignments: KerningDataAlignments, hasHeadings: true);
        }

        Terminal.Out("");
    }

    /// <summary>
    /// This method formats the given collection of kerning pairs for display.
    /// </summary>
    /// <param name="kerning">The kerning pair collection to format.</param>
    /// <returns>The formatted kerning data.</returns>
    private static List<List<string>> FormatKerningData(Kerning kerning)
    {
        List<List<string>> result = [KerningDataHeadings];

        result.AddRange(from entry in kerning.KerningPairs
            let left = ToText(entry.Left)
            let kern = entry.Kern.ToString()
            let right = ToText(entry.Right)
            select (List<string>) [left, kern, right]);

        return result;
    }

    /// <summary>
    /// This method is used to fetch a font face from Google Fonts.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    private static void FetchFontFace(FontsOptions options)
    {
        FaceIdentifier id = options.FetchFontFace;

        if (FontManager.Instance.IsKnownTypeFace(id))
        {
            if (!options.Replace)
                Terminal.ShowError($"The {id.ForDisplay()} font face already exists.  Specify --overwrite if you want to overwrite it.");

            if (FontManager.Instance.GetFontFace(options.FetchFontFace, false).IsLocal)
                Terminal.ShowError($"The {id.ForDisplay()} font face is local.  If you want to pull it from Google Fonts, you need to remove it from the catalog first.");
        }

        try
        {
            _ = FontManager.Instance.GetTypeFace(id);
            
            Terminal.Out($"The font face, {id.ForDisplay()} has been fetched.");
        }
        catch (Exception exception)
        {
            Terminal.ShowError(exception.Message);
        }
    }

    /// <summary>
    /// This method is used to import a local true type file as a font face.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    private static void ImportFontFace(FontsOptions options)
    {
        FaceIdentifier id = options.ImportFontFace;

        if (FontManager.Instance.IsKnownTypeFace(id))
        {
            if (!options.Replace)
                Terminal.ShowError($"The {id.ForDisplay()} font face already exists.  Specify --overwrite if you want to overwrite it.");

            if (!FontManager.Instance.GetFontFace(id, false).IsLocal)
                Terminal.ShowError($"The {id.ForDisplay()} font face was fetched from Google.  If you want to import the local one, you need to remove it from the catalog first.");
        }
        
        if (string.IsNullOrEmpty(options.ImportFileName))
            Terminal.ShowError($"You must specify the local true type file name to import it as {id.ForDisplay()}.");

        string path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), options.ImportFileName!));

        if (!File.Exists(path))
            Terminal.ShowError($"The file, '{path}', does not exist.");
        
        if (FontManager.LoadTypeFace(path) == null)
            Terminal.ShowError($"The file, '{path}', does not appear to be a true type font file.");

        try
        {
            FontManager.Instance.ImportTypeFace(id, path);
            
            Terminal.Out($"The font face, {id.ForDisplay()} has been imported.");
        }
        catch (Exception exception)
        {
            Terminal.ShowError(exception.Message);
        }
    }

    /// <summary>
    /// This method is used to add a kerning pair for a font face.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    private static void AddKerningPair(FontsOptions options)
    {
        FaceIdentifier id = options.AddKerningPair;
        KerningPair pair = options.KerningPair;

        if (pair == null)
            Terminal.ShowError("No kerbing pair was specified.  Use the --pair option to provide one.");

        FontManager.Instance.AddKerningPair(id, pair);
        Terminal.Out("The kerning pair was successfully added.");
    }

    /// <summary>
    /// This method is used to remove a kerning pair for a font face.
    /// </summary>
    /// <param name="options">The options specified by the user on the command line.</param>
    private static void RemoveKerningPair(FontsOptions options)
    {
        FaceIdentifier id = options.RemoveKerningPair;
        KerningPair pair = options.KerningPair;

        if (pair == null)
            Terminal.ShowError("No kerning pair was specified.  Use the --pair option to provide one.");

        FontManager.Instance.RemoveKerningPair(id, pair);
        Terminal.Out("The kerning pair was successfully removed.");
    }

    /// <summary>
    /// This method is used to remove a font face from our font catalog.
    /// </summary>
    /// <param name="id">The ID of the font face.</param>
    private static void RemoveFontFace(FaceIdentifier id)
    {
        FontManager.Instance.RemoveFontFace(id);

        Terminal.Out($"The font face, {id.ForDisplay()} has been removed.");
    }

    /// <summary>
    /// This method is used to parse a text spec for a font face into the appropriate
    /// family name, weight and italic setting.  The font face must exist in our font
    /// catalog.
    /// </summary>
    /// <param name="spec">The spec to parse.</param>
    /// <returns>The identifier for the font face from the spec.</returns>
    public static FaceIdentifier ParseExistingFaceSpec(string spec)
    {
        FaceIdentifier id = ParseFaceSpec(spec);

        if (!FontManager.Instance.IsKnownTypeFace(id))
            throw new ArgumentException($"\"{spec}\" does not refer to a known font face.");

        return id;
    }

    /// <summary>
    /// This method is used to parse a text spec for a font face into the appropriate
    /// family name, weight and italic setting.  The face is allowed to not exist.
    /// </summary>
    /// <param name="spec">The spec to parse.</param>
    /// <returns>The identifier for the font face from the spec.</returns>
    public static FaceIdentifier ParseFaceSpec(string spec)
    {
        string[] split = spec.Split(':', 3);
        string familyName = split[0];
        FontWeight weight = FontWeight.Regular;
        bool italic = false;

        if (split.Length > 1)
        {
            if (!Enum.TryParse(split[1], out weight))
                throw new ArgumentException($"\"{spec}\" is not a valid font face specification.");
        }

        if (split.Length > 2)
        {
            if (!string.IsNullOrEmpty(split[2]) &&
                !"italic".StartsWith(split[2], StringComparison.CurrentCultureIgnoreCase))
                throw new ArgumentException($"\"{spec}\" is not a valid font face specification.");

            italic = true;
        }

        return new FaceIdentifier
        {
            FamilyName = familyName,
            Weight = (int) weight,
            Italic = italic
        };
    }

    /// <summary>
    /// This method is used to parse a text spec for a kerning pair into a kerning entry.
    /// </summary>
    /// <param name="spec">The spec to parse.</param>
    /// <returns>The kerning entry the spec represents.</returns>
    public static KerningPair ParseKernSpec(string spec)
    {
        string[] split = spec.Split(':', 3);
        List<int> leftCodePoints = split[0].AsCodePoints();
        List<int> rightCodePoints = split[2].AsCodePoints();

        if (leftCodePoints.Count != 1 || rightCodePoints.Count != 1 ||
            !short.TryParse(split[1], out short kern))
            throw new ArgumentException($"\"{spec}\" is not a valid kerning pair specification.");

        return new KerningPair
        {
            Left = leftCodePoints[0],
            Right = rightCodePoints[0],
            Kern = kern
        };
    }
}
