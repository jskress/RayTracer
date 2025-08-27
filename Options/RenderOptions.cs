using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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

    [Option('w', "width", Required = false, Default = 800,
        HelpText = "The width of the image to generate.")]
    public int? Width
    {
        get => _width;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is < 1 or > 16384)
                throw new ArgumentException("Width must be between 1 and 16,384.");

            _width = value;
        }
    }

    [Option('h', "height", Required = false, Default = 600,
        HelpText = "The height of the image to generate.")]
    public int? Height
    {
        get => _height;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is < 1 or > 16384)
                throw new ArgumentException("Height must be between 1 and 16,384.");

            _height = value;
        }
    }

    [Option('r', "frame-rate", Required = false, Default = 24,
        HelpText = "The rate, in frames per second, to use when generating a series of images.")]
    public int FrameRate
    {
        get => _frameRate;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is < 1)
                throw new ArgumentException("Frame rate must be at least 1.");

            _frameRate = value;
        }
    }

    [Option('m', "frame", Required = false,
        HelpText = "The specific frame in an animation to render.")]
    public long? Frame
    {
        get => _frame;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is < 0)
                throw new ArgumentException("Frame must be at least 0.");

            _frame = value;
        }
    }

    [Option('c', "bits-per-channel", Required = false,
        HelpText = "The number of bits to use for each channel in colors in the image output file.")]
    public int BitsPerChannel
    {
        get => _bitsPerChannel;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is not 8 and not 16)
                throw new ArgumentException("Bits per color channel must be either 8 or 16.");

            _bitsPerChannel = value;
        }
    }

    [Option('g', "gamma", Required = false,
        HelpText = "The gamma correction to apply to colors in the image output file.")]
    public double? Gamma
    {
        get => _gamma;
        // ReSharper disable once UnusedMember.Global
        set
        {
            if (value is < 0 or > 5)
                throw new ArgumentException("Gamma correction must be between 0 and 5.");

            _gamma = value;
        }
    }

    [Option("no-gamma", Required = false,
        HelpText = "If specified, gamma correction will not be applied to colors in the image output file.")]
    public bool? NoGamma
    {
        get => !_applyGamma;
        // ReSharper disable once UnusedMember.Global
        set => _applyGamma = !value;
    }

    [Option("report-gamma", Required = false,
        HelpText = "If specified, the gamma correction value will be included in the image output file, if supported.")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public bool? ReportGamma { get; set; }

    [Option("no-shadows", Required = false,
        HelpText = "Disable shadow rendering on all objects.")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public bool? NoShadows { get; set; }

    [Option("grayscale", Required = false,
        HelpText = "Grayscale the image when written to image file.")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public bool Grayscale { get; set; }

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
        HelpText = "Sets what sort of antialiasing should be applied to the image being rendered.")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public string AntiAliasingText
    {
        get => AntiAliasing.ToString();
        set => AntiAliasing.Configure(value);
    }

    /// <summary>
    /// This property holds the antialiasing option for the ray tracer.
    /// </summary>
    public AliasingOption AntiAliasing { get; } = new();

    private string _inputFileName;
    private string _outputDirectory;
    private string _outputFileName;
    private string _outputFileExtension;
    private string _outputImageFormat = "png";
    private int? _width;
    private int? _height;
    private int _frameRate = 24;
    private long? _frame;
    private int _bitsPerChannel = 8;
    private double? _gamma;
    private bool? _applyGamma;

    /// <summary>
    /// This is a helper method for properly deriving the right output file name based on
    /// all the various things the user could have specified.
    /// </summary>
    /// <returns>The name of the output image file to write to.</returns>
    private string GetOutputFileName()
    {
        if (_outputFileName != null)
            return _outputDirectory;

        string dir = _outputDirectory ?? Path.GetDirectoryName(_inputFileName);
        string name = Path.GetFileNameWithoutExtension(_inputFileName)!;
        string extension = _outputFileExtension ?? _outputImageFormat;

        name = extension.StartsWith('.')
            ? $"{name}{extension}"
            : $"{name}.{extension}";

        return Path.Combine(dir!, name);
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
        levelText = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(levelText);

        foreach (string name in Enum.GetNames(typeof(OutputLevel)))
        {
            if (name.StartsWith(levelText))
            {
                return Enum.TryParse(name, out OutputLevel outputLevel)
                    ? outputLevel
                    : OutputLevel.Normal;
            }
        }

        throw new ArgumentException($"The text, '{levelText}', is not a valid output level.");
    }
}
