
using CommandLine;
using RayTracer;

Parser.Default.ParseArguments<Arguments>(args)
    .WithParsed(arguments =>
    {
        Arguments.Init(arguments);

        new RayTracer.Core.RayTracer().Render();
    });
