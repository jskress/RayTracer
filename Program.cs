using CommandLine;
using Newtonsoft.Json;
using RayTracer;
using RayTracer.Parser;
using RayTracer.Renderer;

JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Formatting = Formatting.Indented
};

Parser.Default.ParseArguments<ProgramOptions>(args)
    .WithParsed(options =>
    {
        Terminal.OutputLevel = options.OutputLevel;

        LanguageParser parser = new LanguageParser(options.InputFileName);
        ImageRenderer renderer = parser.Parse();

        try
        {
            renderer?.Render(options);
        }
        catch (Exception exception)
        {
            Terminal.ShowException(exception);
        }
    });
