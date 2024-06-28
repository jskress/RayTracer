using CommandLine;
using RayTracer.ImageIO;

namespace RayTracer;

/// <summary>
/// This class represents the command line options that the user may specify to the ray
/// tracer.
/// </summary>
public class ProgramOptions
{
    /// <summary>
    /// This property exposes our singleton instance.
    /// </summary>
    public static ProgramOptions Instance { get; private set; }

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

    [Option('f', "output-image-format", Required = true,
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

    [Option('c', "bits-per-channel", Required = false,
        HelpText = "The number of bits to use for each channel in colors in the image output file.")]
    public int BitsPerChannel
    {
        get => _bitsPerChannel;
        set
        {
            if (value is < 8 or > 16)
                throw new ArgumentException($"Bits per color channel must be between 8 and 16.");

            _bitsPerChannel = value;
        }
    }

    [Option('g', "gamma", Required = false,
        HelpText = "The gamma correction to apply to colors in the image output file.")]
    public double Gamma
    {
        get => _gamma;
        set
        {
            if (value is < 8 or > 5)
                throw new ArgumentException($"Gamma correction must be between 0 ang 5.");

            _gamma = value;
        }
    }

    [Option('q', "quiet", Required = false,
        HelpText = "Set this to true to get no output.")]
    public bool Quiet { get; set; }

    [Option('v', "verbose", Required = false,
        HelpText = "Set this to true to get more output.")]
    public bool Verbose { get; set; }

    /// <summary>
    /// This property provides the largest value a color channel can have.
    /// </summary>
    public int MaxColorChannelValue => (1 << _bitsPerChannel) - 1;

    private string _inputFileName;
    private string _outputDirectory;
    private string _outputFileName;
    private string _outputFileExtension;
    private string _outputImageFormat;
    private int _bitsPerChannel;
    private double _gamma;

    public ProgramOptions()
    {
        _bitsPerChannel = 8;
        _gamma = 2.2;

        Instance = this;
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
}
