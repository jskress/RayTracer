using CommandLine;
using RayTracer.Commands;
using RayTracer.Fonts;

namespace RayTracer.Options;

/// <summary>
/// This class represents the command line options that the user may specify to the ray
/// tracer for managing fonts.
/// </summary>
[Verb("fonts", HelpText = "This command is used to inspect and manage fonts that support text solids.")]
// ReSharper disable once ClassNeverInstantiated.Global
public class FontsOptions
{
    [Option('l', "list", Required = false, SetName = "list",
        HelpText = "Specifying this will list all the fonts registered with the ray tracer.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public bool ListFonts { get; set; }

    [Option('g', "--show-glyphs-for", Required = false, SetName = "glyphs",
        HelpText = "Shows information about all the glyphs in the indicated font face.")]
    public string ShowGlyphsFor
    {
        get => _showGlyphsFor;
        // ReSharper disable once UnusedMember.Global
        set
        {
            // This will verify that the format spec is correct and that the indicated
            // font face is known.
            ShowGlyphs = FontsCommand.ParseExistingFaceSpec(value);

            _showGlyphsFor = value;
        }
    }

    /// <summary>
    /// This property provides the font face identifier for the face to show glyph data.
    /// </summary>
    public FaceIdentifier ShowGlyphs { get; private set; }

    [Option('k', "--show-kerning-for", Required = false, SetName = "kerning",
        HelpText = "Shows kerning information stored in the font catalog for the indicated font face.")]
    public string ShowKerningFor
    {
        get => _showKerningFor;
        // ReSharper disable once UnusedMember.Global
        set
        {
            // This will verify that the format spec is correct and that the indicated
            // font face is known.
            ShowKerning = FontsCommand.ParseExistingFaceSpec(value);

            _showKerningFor = value;
        }
    }

    [Option('f', "--fetch", Required = false, SetName = "fetch",
        HelpText = "Fetches a font face from Google Fonts.")]
    public string FetchGoogleFontFace
    {
        get => _fetchFontFace;
        // ReSharper disable once UnusedMember.Global
        set
        {
            // This will verify that the format spec is correct.
            FetchFontFace = FontsCommand.ParseFaceSpec(value);

            _fetchFontFace = value;
        }
    }

    /// <summary>
    /// This property provides the font face identifier for the face to fetch from Google
    /// Fonts.
    /// </summary>
    public FaceIdentifier FetchFontFace { get; private set; }

    [Option('i', "--import", Required = false, SetName = "import",
        HelpText = "Imports a local true type file as a font face.")]
    public string ImportLocalFontFace
    {
        get => _importFontFace;
        // ReSharper disable once UnusedMember.Global
        set
        {
            // This will verify that the format spec is correct.
            ImportFontFace = FontsCommand.ParseFaceSpec(value);

            _importFontFace = value;
        }
    }

    /// <summary>
    /// This property provides the font face identifier for the face to import.
    /// </summary>
    public FaceIdentifier ImportFontFace { get; private set; }

    [Value(0, HelpText = "The name of the true type font file to import.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string ImportFileName { get; set; }

    [Option('o', "overwrite", Required = false,
        HelpText = "Specifying this will force a font face to be replaced when specifying --fetch or --import.")]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public bool Replace { get; set; }

    /// <summary>
    /// This property provides the font face identifier for the face to show kerning data.
    /// </summary>
    public FaceIdentifier ShowKerning { get; private set; }

    [Option('a', "--add-kerning-for", Required = false, SetName = "add-kerning",
        HelpText = "Adds a kerning pair to the font catalog for the indicated font face.  Requires the --pair option.")]
    public string AddKerningFor
    {
        get => _addKerningFor;
        // ReSharper disable once UnusedMember.Global
        set
        {
            // This will verify that the format spec is correct and that the indicated
            // font face is known.
            AddKerningPair = FontsCommand.ParseExistingFaceSpec(value);

            _addKerningFor = value;
        }
    }

    /// <summary>
    /// This property provides the font face identifier for the face to add a kerning pair to.
    /// </summary>
    public FaceIdentifier AddKerningPair { get; private set; }

    [Option('d', "--remove-kerning-for", Required = false, SetName = "remove-kerning",
        HelpText = "Removes a kerning pair from the font catalog for the indicated font face.  Requires the --pair option.")]
    public string RemoveKerningFor
    {
        get => _removeKerningFor;
        // ReSharper disable once UnusedMember.Global
        set
        {
            // This will verify that the format spec is correct and that the indicated
            // font face is known.
            RemoveKerningPair = FontsCommand.ParseExistingFaceSpec(value);

            _removeKerningFor = value;
        }
    }

    /// <summary>
    /// This property provides the font face identifier for the face to remove a kerning pair to.
    /// </summary>
    public FaceIdentifier RemoveKerningPair { get; private set; }

    [Option('p', "--pair", Required = false,
        HelpText = "Specifies the details of the kerning pair to add to, or remove from, the font catalog for the indicated font face.  Must be specified with the --add-kerning-for or --remove-kerning-pair option.")]
    public string KerningPairToAdd
    {
        get => _kerningPair;
        // ReSharper disable once UnusedMember.Global
        set
        {
            // This will verify that the format spec is correct.
            KerningPair = FontsCommand.ParseKernSpec(value);

            _kerningPair = value;
        }
    }

    /// <summary>
    /// This property provides the kerning pair to add.
    /// </summary>
    public KerningPair KerningPair { get; private set; }

    [Option('r', "--remove", Required = false,
        HelpText = "Remove a font face from the ray tracer's font catalog.")]
    public string RemoveFontFaceFromCatalog
    {
        get => _removeFontFace;
        // ReSharper disable once UnusedMember.Global
        set
        {
            // This will verify that the format spec is correct and that the indicated
            // font face is known.
            RemoveFontFace = FontsCommand.ParseExistingFaceSpec(value);

            _removeFontFace = value;
        }
    }

    /// <summary>
    /// This property provides the font face identifier for the face to remove.
    /// </summary>
    public FaceIdentifier RemoveFontFace { get; private set; }

    private string _showGlyphsFor;
    private string _showKerningFor;
    private string _fetchFontFace;
    private string _importFontFace;
    private string _addKerningFor;
    private string _removeKerningFor;
    private string _kerningPair;
    private string _removeFontFace;
}
