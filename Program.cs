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

        renderer?.Render(options);
    });
