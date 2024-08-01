using CommandLine;
using RayTracer;
using RayTracer.Parser;
using RayTracer.Renderer;

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
