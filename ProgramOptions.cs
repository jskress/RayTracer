using System.Globalization;
using System.Reflection;
using CommandLine;
using RayTracer.General;
using RayTracer.ImageIO;

[assembly: AssemblyTitle("My Ray Tracer")]
[assembly: AssemblyDescription("A ray tracer based on the book, 'The Ray Tracer Challenge'")]
[assembly: AssemblyCopyright("Copyright \u00a9 2024")]
[assembly: AssemblyInformationalVersion("1.0.1")]

namespace RayTracer;

/// <summary>
/// This class represents the command line options that the user may specify to the ray
/// tracer.
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class ProgramOptions
{
    [Option('i', "input-file", Required = true,
        HelpText = "The name of the input file to process.")]
    public string InputFileName
    {
        get => _inputFileName;
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
        set
        {
            string path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), value));

            _outputFileName = path;
        }
    }

    [Option('e', "output-extension", Required = false, SetName = "outputExtension",
        HelpText = "The name of the output file to write the rendered image to.")]
    public string OutputFileExtension
    {
        get => _outputFileExtension;
        set => _outputFileExtension = value.StartsWith('.') ? value : $".{value}";
    }

    [Option('f', "output-image-format", Required = false,
        HelpText = "The image format to use for saving the rendered image to the output file.")]
    public string OutputImageFormat
    {
        get => _outputImageFormat;
        set
        {
            if (!AvailableCodecs.IsFormatSupported(value))
                throw new ArgumentException($"There is no image format named, '{value}'");

            _outputImageFormat = value;
        }
    }

    [Option('w', "width", Required = false, Default = 800,
        HelpText = "The width of the image to generate.")]
    public int? Width
    {
        get => _width;
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
        set
        {
            if (value is < 1 or > 16384)
                throw new ArgumentException("Height must be between 1 and 16,384.");

            _height = value;
        }
    }

    [Option('c', "bits-per-channel", Required = false,
        HelpText = "The number of bits to use for each channel in colors in the image output file.")]
    public int BitsPerChannel
    {
        get => _bitsPerChannel;
        set
        {
            if (value is not 8 and not 16)
                throw new ArgumentException($"Bits per color channel must be either 8 or 16.");

            _bitsPerChannel = value;
        }
    }

    [Option('g', "gamma", Required = false,
        HelpText = "The gamma correction to apply to colors in the image output file.  Set this to 1 to turn gamma correction off.")]
    public double? Gamma
    {
        get => _gamma;
        set
        {
            if (value is < 0 or > 5)
                throw new ArgumentException($"Gamma correction must be between 0 and 5.");

            _gamma = value;
        }
    }

    [Option("grayscale", Required = false,
        HelpText = "Grayscale the image when written to image file.")]
    public bool Grayscale { get; set; }

    [Option('l', "output-level", Required = false, Default = "normal",
        // ReSharper disable once StringLiteralTypo
        HelpText = "Sets the desired level of output.  Must be one of, [q]uiet, [n]ormal, [c]hatty or [v]erbose.  The values are not case-sensitive.")]
    public string OutputLevelText
    {
        get => OutputLevel.ToString();
        set => OutputLevel = ToOutputLevel(value);
    }

    /// <summary>
    /// This property holds the output level the renderer is to use.
    /// </summary>
    public OutputLevel OutputLevel { get; private set; }

    private string _inputFileName;
    private string _outputDirectory;
    private string _outputFileName;
    private string _outputFileExtension;
    private string _outputImageFormat;
    private int? _width;
    private int? _height;
    private int _bitsPerChannel;
    private double? _gamma;

    public ProgramOptions()
    {
        _width = null;
        _height = null;
        _bitsPerChannel = 8;
        _gamma = null;
        _outputImageFormat = "png";

        OutputLevel = OutputLevel.Normal;
    }
    
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
    /// represents.  We do so by making the input case-insensitive and allowed to be an
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
