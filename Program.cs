using System.Diagnostics;
using CommandLine;
using RayTracer;
using RayTracer.Graphics;
using RayTracer.Parser;

Parser.Default.ParseArguments<ProgramOptions>(args)
    .WithParsed(options =>
    {
        RenderData renderData = new FileParser(options)
            .Parse();
        Canvas canvas = renderData.NewCanvas;

        Terminal.Out("Ray Tracer v1.0.0");
        Terminal.Out("Input file:", true);
        Terminal.Out($"--> {options.InputFileName}", true);
        Terminal.Out("Output file:", true);
        Terminal.Out($"--> {options.OutputFileName}", true);

        Terminal.Out("Generating...");

        Stopwatch stopwatch = Stopwatch.StartNew();

        renderData.Camera.Render(renderData.Scene, canvas, renderData.Scanner);

        stopwatch.Stop();

        Terminal.Out("Writing...");

        renderData.OutputFile.Save(canvas);

        Terminal.Out($"Done!  It took {stopwatch.Elapsed}");
    });
