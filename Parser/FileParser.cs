using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.ImageIO;

namespace RayTracer.Parser;

/// <summary>
/// This class (and its fellows) implement a VERY simple, basic file parser for loading
/// information (camera, scene, etc.) to create a rendered image.
/// </summary>
public class FileParser
{
    private readonly string _inputFileName;
    private readonly string _outputFileName;

    private RenderData _renderData;
    private FileContent _fileContent;
    private ScannerParser _scannerParser;
    private LightParser _lightParser;

    public FileParser(IReadOnlyList<string> args)
    {
        _inputFileName = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), args[0]));
        _outputFileName = Path.ChangeExtension(_inputFileName, ".ppm");
        _renderData = null;
        _fileContent = null;
        _scannerParser = null;
        _lightParser = null;

        ReadFile();
    }

    /// <summary>
    /// This method is used to actually perform the parsing of the input file.
    /// </summary>
    /// <returns>The data that should control the render.</returns>
    public RenderData Parse()
    {
        _renderData = new RenderData
        {
            OutputFile = new ImageFile(_outputFileName),
            Camera = new Camera(),
            Scene = new Scene()
        };

        while (_fileContent.GetNextWord() is { } word)
            ParseNextClause(word);

        return _renderData;
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
                _renderData.Width = _fileContent.GetNextInt(0, 16384);
                break;
            case "height":
                _renderData.Height = _fileContent.GetNextInt(0, 16384);
                break;
            case "scanner":
                _renderData.Scanner = _scannerParser.Parse();
                break;
            case "camera":
                CameraParser cameraParser = new (_fileContent, _renderData.Camera);
                cameraParser.Parse();
                break;
            case "light":
                _renderData.Scene.Lights.Add(_lightParser.ParseLight());
                break;
            case "sphere":
                _renderData.Scene.Surfaces.Add(ParseSurface(new Sphere()));
                break;
            case "plane":
                _renderData.Scene.Surfaces.Add(ParseSurface(new Plane()));
                break;
            case "cube":
                _renderData.Scene.Surfaces.Add(ParseSurface(new Cube()));
                break;
            case "cylinder":
                Cylinder cylinder = new ();
                CircularSurfaceAttributesParser circularSurfaceAttributesParser = new (_fileContent, cylinder);
                _renderData.Scene.Surfaces.Add(ParseSurface(cylinder, circularSurfaceAttributesParser));
                break;
            case "conic":
                Conic conic = new ();
                CircularSurfaceAttributesParser conicAttributesParser = new (_fileContent, conic);
                _renderData.Scene.Surfaces.Add(ParseSurface(conic, conicAttributesParser));
                break;
            case "backgroundColor":
                _renderData.Scene.BackgroundColor = _fileContent.GetNextColor();
                break;
            default:
                ErrorOut($"I don't know what to do with {word}");
                break;
        }
    }

    /// <summary>
    /// This method is used to parse the details about a surface.
    /// </summary>
    /// <param name="surface"></param>
    /// <param name="attributeParser">An optional parser for extra attributes.</param>
    /// <returns></returns>
    private Surface ParseSurface(Surface surface, AttributeParser attributeParser = null)
    {
        SurfaceParser parser = new (_fileContent, surface, attributeParser);

        parser.Parse();

        return surface;
    }

    /// <summary>
    /// This method handles stopping the program on a parsing error.
    /// </summary>
    /// <param name="message"></param>
    internal static void ErrorOut(string message)
    {
        Console.WriteLine($"Error: {message}");
        // Environment.Exit(1);
        throw new Exception(message);
    }
}
