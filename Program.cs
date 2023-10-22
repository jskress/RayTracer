
using System.Diagnostics;
using RayTracer.Basics;
using RayTracer.ColorSources;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Scanners;

Plane floor = new ()
{
    Material = new Material
    {
        ColorSource = new RingColorSource(
            new Color(0, 0, 0.25),
            new Color(0, 0, 0.75))
    }
};
Sphere middle = new ()
{
    Transform = Transforms.Translate(-0.5, 1, 0.5),
    Material = new Material
    {
        ColorSource = new ConstantColorSource(Color.Black),
        Reflective = 0.9,
        Transparency = 0.9,
        IndexOfRefraction = IndicesOfRefraction.Glass
    }
};
Sphere behind = new()
{
    Transform = Transforms.Translate(0.25, 1, 12.5),
    Material = new Material
    {
        ColorSource = new ConstantColorSource(new Color(1, 1, 0))
    }
};
Sphere right = new ()
{
    Transform = Transforms.Translate(1.5, 0.5, -0.5) *
                Transforms.Scale(0.5),
    Material = new Material
    {
        ColorSource = new CheckerColorSource(Color.White, new Color(1, 0.1, 0.1))
        {
            Transform = Transforms.Scale(0.33)
        },
        Diffuse = 0.7,
        Specular = 0.3
    }
};
Sphere left = new ()
{
    Transform = Transforms.Translate(-1.5, 0.33, -0.75) *
                Transforms.Scale(0.33),
    Material = new Material
    {
        ColorSource = new LinearGradientColorSource(
            new Color(0, 1, 0),
            new Color(0, 0, 1)),
        Diffuse = 0.7,
        Specular = 0.3
    }
};
PointLight light = new ()
{
    Location = new Point(-10, 10, -10)
};
Camera camera = new ()
{
    Location = new Point(0, 1.5, -5),
    LookAt = new Point(0, 1, 0),
    FieldOfView = 60
};
Scene scene = new ()
{
    Lights = new List<PointLight> { light },
    Surfaces = new List<Surface> { floor, left, middle, right, behind }
};
Canvas canvas = new (800, 400);

Console.WriteLine("Generating...");

Stopwatch stopwatch = Stopwatch.StartNew();

camera.Render(scene, canvas, new PixelParallelScanner());

stopwatch.Stop();

Console.WriteLine("Writing...");

ImageFile file = new ("/Users/jskress/dev/csharp/RayTracer/test.ppm");

file.Save(canvas);

Console.WriteLine($"Done!  It took {stopwatch.Elapsed}");
