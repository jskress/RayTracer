using System.Diagnostics;
using CommandLine;
using CommandLine.Text;
using RayTracer;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Parser;

Parser.Default.ParseArguments<ProgramOptions>(args)
    .WithParsed(options =>
    {
        Terminal.OutputLevel = options.OutputLevel;

        RenderContext context = new ();
        List<Scene> scenes = new FileParser(context, options)
            .Parse();

        context.ApplyOptions(options);

        Terminal.Out(HeadingInfo.Default);
        Terminal.Out("Input file:", OutputLevel.Chatty);
        Terminal.Out($"--> {options.InputFileName}", OutputLevel.Chatty);
        Terminal.Out("Output file:", OutputLevel.Chatty);
        Terminal.Out($"--> {options.OutputFileName}", OutputLevel.Chatty);

        Terminal.Out("Generating...");

        Stopwatch stopwatch = Stopwatch.StartNew();
        Scene scene = scenes.First();
        Camera camera = scene.Cameras.First();
        Canvas canvas = camera.Render(context, scene);

        stopwatch.Stop();

        Terminal.Out("Writing...");

        ImageFile outputFile = new ImageFile(options.OutputFileName, options.OutputImageFormat);

        outputFile.Save(context, canvas, info: context.ImageInformation);

        Terminal.Out($"Done!  It took {stopwatch.Elapsed}");
    });
