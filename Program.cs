using System.Diagnostics;
using RayTracer.Graphics;
using RayTracer.Parser;

RenderData renderData = new FileParser(args).Parse();
Canvas canvas = renderData.Canvas;

Console.WriteLine("Generating...");

Stopwatch stopwatch = Stopwatch.StartNew();

renderData.Camera.Render(renderData.Scene, canvas, renderData.Scanner);

stopwatch.Stop();

Console.WriteLine("Writing...");

renderData.OutputFile.Save(canvas);

Console.WriteLine($"Done!  It took {stopwatch.Elapsed}");
