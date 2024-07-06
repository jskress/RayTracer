using System.Diagnostics;
using CommandLine;
using CommandLine.Text;
using RayTracer;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Parser;

Parser.Default.ParseArguments<ProgramOptions>(args)
    .WithParsed(options =>
    {
        RenderData renderData = new FileParser(options)
            .Parse();
        Canvas canvas = renderData.NewCanvas;

        Terminal.Out(HeadingInfo.Default);
        Terminal.Out("Input file:", OutputLevel.Chatty);
        Terminal.Out($"--> {options.InputFileName}", OutputLevel.Chatty);
        Terminal.Out("Output file:", OutputLevel.Chatty);
        Terminal.Out($"--> {options.OutputFileName}", OutputLevel.Chatty);

        Terminal.Out("Generating...");

        Stopwatch stopwatch = Stopwatch.StartNew();

        renderData.Camera.Render(renderData.Scene, canvas, renderData.Scanner);

        stopwatch.Stop();

        Terminal.Out("Writing...");

        renderData.OutputFile.Save(canvas);

        Terminal.Out($"Done!  It took {stopwatch.Elapsed}");
    });
