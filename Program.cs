using System.Diagnostics;
using CommandLine;
using CommandLine.Text;
using RayTracer;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Parser;
using RayTracer.Renderer;

Parser.Default.ParseArguments<ProgramOptions>(args)
    .WithParsed(options =>
    {
        Terminal.OutputLevel = options.OutputLevel;

        LanguageParser parser = new LanguageParser(options.InputFileName);
        ImageRenderer renderer = parser.Parse();

        if (renderer != null)
        {
            renderer.Render(options);

            // Everything below here is legacy, and will be moved as appropriate to the
            // ImageRenderer class.

            RenderContext context = new ();
            List<Scene> scenes = new FileParser(context, options)
                .Parse();

            Terminal.Out(HeadingInfo.Default);
            Terminal.Out("Input file:", OutputLevel.Chatty);
            Terminal.Out($"--> {options.InputFileName}", OutputLevel.Chatty);
            Terminal.Out("Generating...");

            Stopwatch stopwatch = Stopwatch.StartNew();
            Scene scene = scenes.First();
            Camera camera = scene.Cameras.First();
            Canvas canvas = camera.Render(context, scene);

            stopwatch.Stop();

            Terminal.Out("Output file:", OutputLevel.Chatty);
            Terminal.Out($"--> {options.OutputFileName}", OutputLevel.Chatty);
            Terminal.Out("Writing...");

            ImageFile outputFile = new ImageFile(options.OutputFileName, options.OutputImageFormat);

            outputFile.Save(context, canvas, info: context.ImageInformation);

            Terminal.Out($"Done!  It took {stopwatch.Elapsed}");
        }
    });
