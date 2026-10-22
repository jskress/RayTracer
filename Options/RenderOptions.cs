using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CommandLine;
using RayTracer.General;
using RayTracer.Pixels;

[assembly: AssemblyTitle("Raymond")]
[assembly: AssemblyDescription("A CSG ray tracer based on the book, 'The Ray Tracer Challenge.'")]
[assembly: AssemblyCopyright("Copyright \u00a9 2024")]
[assembly: AssemblyInformationalVersion("1.0.1")]

namespace RayTracer.Options;

/// <summary>
/// This class represents the command line options that the user may specify to the ray
/// tracer for rendering.
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
[Verb("render", isDefault: true, HelpText = "This command is used to render ray traced images.")]
public class RenderOptions
{
    [Option('i', "input-file", Required = true,
        HelpText = "The name of the input file to process.")]
    public string InputFileName
    {
        get => _inputFileName;
        // ReSharper disable once UnusedMember.Global
        set
        {
            string path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), value));

            if (!File.Exists(path))
                throw new ArgumentException($"The file, '{path}', does not exist.");

            _inputFileName = path;
        }
    }

    [Option('d', "output-dir", Required = false,
        HelpText = "The name of the directory where the output file will be written.")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            string path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), value));

            if (!Directory.Exists(path))
                throw new ArgumentException($"The directory, '{path}', does not exist.");

            _outputDirectory = path;
        }
    }

    [Option('o', "output-file", Required = false, SetName = "outputName",
        HelpText = "The name of the output file to write the rendered image to.")]
    public string OutputFileName
    {
        get => GetOutputFileName();
        // ReSharper disable once UnusedMember.Global
        set
        {
            string path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), value));

            _outputFileName = path;
        }
    }

    [Option('e', "output-extension", Required = false, SetName = "outputExtension",
        HelpText = "The name of the output file to write the rendered image to.")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public string OutputFileExtension
    {
        get => _outputFileExtension;
        set => _outputFileExtension = value.StartsWith('.') ? value : $".{value}";
    }

    [Option("scene", Required = false,
        HelpText = "The name of the scene to render, for a file that defines more than one.  This takes precedence over any scene named by a 'render' command in the file.")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public string SceneName { get; set; }

    [Option("camera", Required = false,
        HelpText = "The name of the camera to render with, for a scene that defines more than one.  This takes precedence over any camera named by a 'render' command in the file.")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public string CameraName { get; set; }

    // Note: no Default here, and none on Height below, deliberately.  `RenderContext.ApplyOptions`
    // merges these with `options.Width ?? Width`, so that a size the scene's own `context { }`
    // block asked for survives when the command line says nothing -- exactly as Gamma does.  A
    // Default would defeat that: CommandLineParser fills the property in whether or not the flag
    // was passed, so the value would never be null and the scene's size was silently overwritten
    // every time.  The fallback when neither says anything is `RenderContext`'s own 800 by 600.
    [Option('w', "width", Required = false,
        HelpText = "The width of the image to generate.  Defaults to 800, or whatever the scene's context block asks for.")]
    public int? Width
    {
        get => field;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is < 1 or > 16384)
                throw new ArgumentException("Width must be between 1 and 16,384.");

            field = value;
        }
    }

    [Option('h', "height", Required = false,
        HelpText = "The height of the image to generate.  Defaults to 600, or whatever the scene's context block asks for.")]
    public int? Height
    {
        get => field;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is < 1 or > 16384)
                throw new ArgumentException("Height must be between 1 and 16,384.");

            field = value;
        }
    }

    // Null until given, like the antialiasing, so that a rate the scene's animation asks for is not
    // overruled by a command line that said nothing about it.
    [Option('r', "frame-rate", Required = false,
        HelpText = "The rate, in frames per second, an animation is rendered at (24 unless the scene says).")]
    public int? FrameRate
    {
        get => field;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is < 1)
                throw new ArgumentException("Frame rate must be at least 1.");

            field = value;
        }
    }

    [Option('m', "frame", Required = false,
        HelpText = "The one frame of an animation to render, as a still at that frame's time.")]
    public long? Frame
    {
        get => field;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is < 0)
                throw new ArgumentException("Frame must be at least 0.");

            field = value;
        }
    }

    [Option("frames", Required = false,
        HelpText = "The frames of an animation to render: one, as 48, or a run of them, as 0-143.")]
    public string Frames
    {
        get => field;
        // ReSharper disable once UnusedMember.Global
        set
        {
            FrameRun = FrameRunOf(value);
            field = value;
        }
    }

    [Option("still", Required = false,
        HelpText = "Render an animation as its still: the one frame it names for that, or its first.")]
    public bool Still { get; set; }

    /// <summary>
    /// This property holds the first and last frames <see cref="Frames"/> asks for, or null if it was not given.
    /// </summary>
    public (long First, long Last)? FrameRun { get; private set; }

    private static (long First, long Last)? FrameRunOf(string text)
    {
        if (text is null)
            return null;

        string[] ends = text.Split('-');

        if (ends.Length is 1 or 2 && long.TryParse(ends[0], out long first) &&
            long.TryParse(ends[^1], out long last) && first >= 0 && last >= first)
        {
            return (first, last);
        }

        throw new ArgumentException($"Frames are given as one, like 48, or a run, like 0-143, not '{text}'.");
    }

    [Option('c', "bits-per-channel", Required = false,
        HelpText = "The number of bits to use for each channel in colors in the image output file.")]
    // Null until `-c` is given, so that `RenderContext.ApplyOptions` can leave a depth the scene
    // settled on alone rather than putting the default back over the top of it.
    public int? BitsPerChannel
    {
        get => field;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is not 8 and not 16)
                throw new ArgumentException("Bits per color channel must be either 8 or 16.");

            field = value;
        }
    }

    [Option('g', "gamma", Required = false,
        HelpText = "A plain gamma power to write the image file with, instead of the sRGB curve every viewer reads a picture with.")]
    public double? Gamma
    {
        get => field;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is < 0 or > 5)
                throw new ArgumentException("Gamma correction must be between 0 and 5.");

            field = value;
        }
    }

    // Note: these are plain, non-nullable `bool` (rather than `bool?`) because
    // CommandLineParser 2.9.1 doesn't support `bool?` as a zero-argument switch — it treats
    // it as expecting an explicit value, consumes whatever token follows on the command line
    // as that value, and fails with a "bad format" error (which also corrupts parsing of
    // every option after it) as soon as that token isn't literally "true"/"false".  Each of
    // these is a one-directional override (there's no CLI flag to force the opposite), so a
    // plain `bool` works: `false` (the unset default) means "don't override whatever the
    // scene's own `context { }` block configured," exactly like the `?? existing value`
    // pattern these used to rely on.  See `RenderContext.ApplyOptions`.
    [Option("no-gamma", Required = false,
        HelpText = "If specified, the light is written to the image file as it stands, with no curve at all.")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public bool NoGamma { get; set; }

    [Option("no-shadows", Required = false,
        HelpText = "Disable shadow rendering on all objects.")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public bool NoShadows { get; set; }

    [Option("grayscale", Required = false,
        HelpText = "Grayscale the image when written to image file.")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public bool Grayscale { get; set; }

    [Option("spectral", Required = false,
        HelpText = "Carry light wavelength by wavelength rather than as red, green and blue, so that colored light on colored surfaces, and anything else that depends on wavelength, comes out as it physically would.")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public bool Spectral { get; set; }

    [Option("white-balance", Required = false,
        HelpText = "Balance the picture so that a glow at this temperature, in kelvin, comes out white: about 2800 for a room lit by household bulbs, 1900 for candlelight.  Left out, white is noon sunlight, about 5500.")]
    // Null until given, so that a balance the scene settled on is left alone.
    public double? WhiteBalance
    {
        get => field;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is <= 0)
                throw new ArgumentException("A white balance is a temperature in kelvin, and must be above nothing.");

            field = value;
        }
    }

    [Option("exposure", Required = false,
        HelpText = "How many stops brighter, or darker if less than nought, to expose the picture than the light the scene gives: each stop doubles it.  Given, it takes the place of what every camera says for itself.")]
    // Null until given, so that what each camera says is left alone.
    public double? Exposure { get; set; }

    [Option("film-curve", Required = false,
        HelpText = "How the light is developed into a picture: soft (the default) eases each channel brighter than eight tenths of white toward white, neutral does the same keeping the color's hue, agx is a film's curve over the whole range, and none cuts it off at white.")]
    // Null until given, so that a curve the scene settled on is left alone.
    public string FilmCurve
    {
        get => field;
        // ReSharper disable once UnusedMember.Global
        set
        {
            Graphics.FilmCurve.Named(value);

            field = value;
        }
    }

    [Option('l', "output-level", Required = false, Default = "normal",
        // ReSharper disable once StringLiteralTypo
        HelpText = "Sets the desired level of output.  Must be one of, [q]uiet, [n]ormal, [c]hatty or [v]erbose.  The values are not case-sensitive.")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public string OutputLevelText
    {
        get => OutputLevel.ToString().ToLowerInvariant();
        set => OutputLevel = ToOutputLevel(value);
    }

    /// <summary>
    /// This property holds the output level the renderer is to use.
    /// </summary>
    public OutputLevel OutputLevel { get; private set; } = OutputLevel.Normal;

    [Option('a', "antialias", Required = false,
        HelpText = "The antialiasing to apply: off, or adaptive[:depth[:threshold]], such as adaptive:3 or adaptive:3:0.2.  The depth is how many times the sampler may look further into a pixel, a whole number (5 if left out); the threshold is how far two samples must disagree before it does, more than 0 and at most 1 (0.1 if left out).  A depth alone, such as 3, means adaptive:3.  Defaults to off, or whatever the scene's context block asks for.")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public string AntiAliasingText
    {
        get => AntiAliasing?.ToString();
        set => (AntiAliasing ??= new AliasingOption()).Configure(value);
    }

    /// <summary>
    /// This property holds the antialiasing option for the ray tracer.
    /// </summary>
    // Null until `-a` is given, so that `RenderContext.ApplyOptions` can tell "the user asked for
    // no antialiasing" from "the user said nothing about antialiasing" and leave the scene's own
    // setting standing in the second case.
    public AliasingOption AntiAliasing { get; private set; }

    [Option('p', "progress", Required = false,
        HelpText = "Sets how progress is reported: bar (the default), tool or none.")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public string ProgressStyleText
    {
        get => ProgressStyle.ToString().ToLowerInvariant();
        set => ProgressStyle = ToProgressStyle(value);
    }

    /// <summary>
    /// This property holds how progress should be reported.
    /// </summary>
    public ProgressStyle ProgressStyle { get; private set; } = ProgressStyle.Bar;

    [Option("stats", Required = false,
        HelpText = "Report what the render cost and what its scene held, once it finishes.")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public bool ReportStatistics { get; set; }

    private string _inputFileName;
    private string _outputDirectory;
    private string _outputFileName;
    private string _outputFileExtension;
    private string _outputImageFormat = "png";

    /// <summary>
    /// This is a helper method for properly deriving the right output file name based on
    /// all the various things the user could have specified.
    /// </summary>
    /// <returns>The name of the output image file to write to.</returns>
    private string GetOutputFileName()
    {
        if (_outputFileName != null)
            return _outputFileName;

        string dir = _outputDirectory ?? Path.GetDirectoryName(_inputFileName);
        string name = Path.GetFileNameWithoutExtension(_inputFileName)!;
        string extension = _outputFileExtension ?? _outputImageFormat;

        name = extension.StartsWith('.')
            ? $"{name}{extension}"
            : $"{name}.{extension}";

        return Path.Combine(dir!, name);
    }

    /// <summary>
    /// This is a helper method for converting a piece of text to the progress style it names.  The
    /// text is taken case-insensitively and may be abbreviated, in the same way the output level's
    /// text may be, so <c>-p tool</c> and <c>-p t</c> mean the same thing.
    /// </summary>
    /// <param name="styleText">The text to start with.</param>
    /// <returns>The progress style the text names.</returns>
    private static ProgressStyle ToProgressStyle(string styleText)
    {
        return ToNamedValue<ProgressStyle>(styleText, "style of progress", "bar, tool or none");
    }

    /// <summary>
    /// This is a helper method for converting a piece of text to the output level it
    /// represents.
    /// We do so by treating the input in a case-insensitive way and allow it to be an
    /// abbreviation.
    /// </summary>
    /// <param name="levelText">The text to start with.</param>
    /// <returns>The output level the text represents.</returns>
    private static OutputLevel ToOutputLevel(string levelText)
    {
        return ToNamedValue<OutputLevel>(levelText, "output level", "quiet, normal, chatty or verbose");
    }

    /// <summary>
    /// This is a helper method for finding the one value of an enumeration that a piece of text names.
    /// The text is taken case-insensitively and may be abbreviated to any leading part of a name that
    /// only one value shares, so <c>-l v</c> and <c>-l VERBOSE</c> both mean the same thing.
    /// <para>
    /// Two things here are worth not undoing, since both were live faults.  The comparison is
    /// case-insensitive rather than done by title-casing the text first: title-casing leaves a word
    /// that is already all capitals alone, on the assumption that it is an acronym, so <c>VERBOSE</c>
    /// came out of it unchanged, matched nothing, and was refused by an option whose own help text
    /// promises that its values are not case-sensitive.
    /// </para>
    /// <para>
    /// And empty text is refused rather than searched for.  Every name begins with an empty string, so
    /// a search would match the first value declared and return it -- which for the output level meant
    /// that <c>-l ''</c> rendered in complete silence, no error and no output, where a script filling
    /// that in from an unset variable would have wanted the ordinary default.
    /// </para>
    /// <para>
    /// One caveat comes with matching on a leading part: no two names of either enumeration share a
    /// first letter today, so every abbreviation names exactly one value, but a name added later that
    /// did share one would be reachable only by spelling more of it out -- the earlier-declared value
    /// would take the shorter form.  Worth a thought before adding to either list.
    /// </para>
    /// </summary>
    /// <param name="text">The text to interpret.</param>
    /// <param name="noun">What the value is, for the message if the text names nothing.</param>
    /// <param name="choices">The values to offer, for that same message.</param>
    /// <returns>The one value the text names.</returns>
    private static TValue ToNamedValue<TValue>(string text, string noun, string choices)
        where TValue : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (string name in Enum.GetNames<TValue>())
            {
                if (name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                    return Enum.Parse<TValue>(name);
            }
        }

        throw new ArgumentException($"'{text}' is not an {noun}; use {choices}.");
    }
}
