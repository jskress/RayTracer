using RayTracer.Core;
using RayTracer.General;
using RayTracer.Pigments;

namespace RayTracer.Parser;

/// <summary>
/// This class (and its fellows) implement a VERY simple, basic file parser for loading
/// information (camera, scene, etc.) to create a rendered image.
/// </summary>
public class FileParser
{
    private readonly RenderContext _context;
    private readonly string _inputFileName;

    private FileContent _fileContent;
    private Scene _scene;
    private ScannerParser _scannerParser;
    private LightParser _lightParser;
    private SurfaceParser _surfaceParser;

    public FileParser(RenderContext context, ProgramOptions options)
    {
        _context = context;
        _inputFileName = options.InputFileName;
        _fileContent = null;
        _scene = null;
        _scannerParser = null;
        _lightParser = null;
        _surfaceParser = null;

        ReadFile();
    }

    /// <summary>
    /// This method is used to actually perform the parsing of the input file.
    /// </summary>
    /// <returns>The data that should control the render.</returns>
    public List<Scene> Parse()
    {
        _scene = new Scene();

        _surfaceParser = new SurfaceParser(_fileContent, Path.GetDirectoryName(_inputFileName), _scene);

        while (_fileContent.GetNextWord() is { } word)
            ParseNextClause(word);

        return [_scene];
    }

    /// <summary>
    /// This method reads our input file, stripping out comments, and building the final
    /// string of the file's contents that will then be parsed.
    /// </summary>
    /// <returns>The text content of the file, ready to parse.</returns>
    private void ReadFile()
    {
        if (!File.Exists(_inputFileName))
            ErrorOut($"No such file named {_inputFileName}");

        IEnumerable<string> result = File.ReadAllLines(_inputFileName)
            .Select(line =>
            {
                int p = line.IndexOf("//", StringComparison.Ordinal);

                if (p >= 0)
                    line = line[..p];

                return line.Trim();
            })
            .Where(line => line.Length > 0);

        _fileContent = new FileContent(string.Join(" ", result));
        _scannerParser = new ScannerParser(_fileContent);
        _lightParser = new LightParser(_fileContent);
    }

    /// <summary>
    /// This method is used to parse the next top-level clause from the content.
    /// </summary>
    /// <param name="word">The word that started the content.</param>
    private void ParseNextClause(string word)
    {
        switch (word)
        {
            case "width":
                _context.Width = _fileContent.GetNextInt(1, 16384);
                break;
            case "height":
                _context.Height = _fileContent.GetNextInt(1, 16384);
                break;
            case "scanner":
                _context.Scanner = _scannerParser.Parse();
                break;
            case "camera":
                Camera camera = new Camera();
                CameraParser cameraParser = new (_fileContent, camera);
                cameraParser.Parse();
                _scene.Cameras.Add(camera);
                break;
            case "light":
                _scene.Lights.Add(_lightParser.ParseLight());
                break;
            case "background":
                _scene.Background = new SolidPigment(_fileContent.GetNextColor());
                break;
            case "define":
                ParseDefinition();
                break;
            default:
                if (!_surfaceParser.TryParseAttributes(word))
                    ErrorOut($"I don't know what to do with {word}");
                break;
        }
    }

    /// <summary>
    /// This method is used to parse the assignment of something to a name.
    /// </summary>
    private void ParseDefinition()
    {
        string name = _fileContent.GetNextWord(true);
        string type = _fileContent.GetNextWord(true);

        switch (type)
        {
            case "colorSource":
                _fileContent.ColorSources[name] =
                    new ColorSourceParser(_fileContent).ParseColorSource();
                break;
            case "material":
                _fileContent.Materials[name] =
                    new MaterialParser(_fileContent).ParseMaterial();
                break;
            default:
                ErrorOut($"Cannot define something of type {type}");
                break;
        }
    }

    /// <summary>
    /// This method handles stopping the program on a parsing error.
    /// </summary>
    /// <param name="message"></param>
    internal static void ErrorOut(string message)
    {
        Console.WriteLine($"Error: {message}");
        Environment.Exit(1);
    }
}
