using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Materials;
using RayTracer.Shapes;

namespace RayTracer.Core;

/// <summary>
/// This class represents a frame within a rendering.
/// </summary>
internal class Frame
{
    private readonly ImageFile _imageFile;
    private readonly Image _image;
    private readonly Scene _scene;
    private readonly int _antiAliasSamples;
    private readonly int _maxBounceDepth;
    private readonly Progress _progress;
    private readonly Random _rng;

    private long _pixelCount;

    internal Frame(string outputFileName, int antiAliasSamples, int maxBounceDepth)
    {
        _imageFile = new ImageFile(outputFileName);
        _image = new Image(Arguments.Instance.Width, Arguments.Instance.Height);
        _scene = Scene.CreateDefaultScene(new Camera(_image));
        _antiAliasSamples = antiAliasSamples;
        _maxBounceDepth = maxBounceDepth;
        _pixelCount = _image.Width * _image.Height;
        _progress = new Progress(_pixelCount);
        _rng = new Random(42);
    }

    /// <summary>
    /// This method builds the scene we are to render.
    /// </summary>
    private void BuildTheScene()
    {
        // Matte ground = new (new Color(0.5, 0.5, 0.5));

        // _scene.Add(new Sphere(
        //     new Point(0, -1000, 0), 1000, ground));

        // Vector max = new (4, 0.2, 0);

        // for (int a = -11; a < 11; a++)
        // {
        //     for (int b = -11; b < 11; b++)
        //     {
        //         Point center = new Point(
        //             a + _rng.NextDouble() * 0.9,
        //             0.2,
        //             b + _rng.NextDouble() * 0.9);
        //
        //         if ((center - max).Length > 0.9)
        //         {
        //             double selector = _rng.NextDouble();
        //             Material material;
        //
        //             if (selector < 0.8)
        //                 material = new Matte(Color.Random() * Color.Random());
        //             else if (selector < 0.95)
        //                 material = new Metal(Color.Random(min: 0.5), _rng.NextDouble() * 0.5);
        //             else
        //                 material = new Dielectric(1.5);
        //
        //             _scene.Add(new Sphere(center, 0.2, material));
        //         }
        //     }
        // }

        // Dielectric glass = new (1.5);
        // Matte matte = new Matte(new Color(0.4, 0.2, 0.1));
        // Metal metal = new Metal(new Color(0.7, 0.6, 0.5), 0);
        //
        // _scene.Add(new Sphere(new Point(0, 1, 0), 1, glass));
        // _scene.Add(new Sphere(new Point(-4, 1, 0), 1, matte));
        // _scene.Add(new Sphere(new Point(4, 1, 0), 1, metal));
    }

    /// <summary>
    /// This method is used to render the configured image.
    /// </summary>
    public void Render()
    {
        _scene.Camera.Initialize();

        for (int y = 0; y < _image.Height; y++)
        {
            for (int x = 0; x < _image.Width; x++)
            {
                Pixel pixel = new () { X = x, Y = y };

                ThreadPool.QueueUserWorkItem(RenderPixel, pixel, true);
            }
        }

        while (Interlocked.Read(ref _pixelCount) > 0)
            Task.Delay(TimeSpan.FromMilliseconds(5));

        _imageFile.Save(_image);

        Console.CursorLeft = 0;
        Console.WriteLine("Done!");
    }

    /// <summary>
    /// This method is used to determine the color for the given pixel and store
    /// it in the image we are rendering.
    /// </summary>
    /// <param name="pixel">The pixel to process.</param>
    private void RenderPixel(Pixel pixel)
    {
        CollectSamples(pixel);

        _image.SetColor(pixel.Color, pixel);

        Interlocked.Decrement(ref _pixelCount);

        _progress.Bump();
    }

    /// <summary>
    /// This method is used to gather all our color samples for the given pixel.
    /// </summary>
    /// <param name="pixel">The pixel to gather color samples for.</param>
    private void CollectSamples(Pixel pixel)
    {
        Camera camera = _scene.Camera;

        if (_antiAliasSamples > 1)
        {
            for (int sample = 0; sample < _antiAliasSamples; sample++)
            {
                Color color = CalculatePixelColor(
                    camera.CreateRayFor(pixel, true), _maxBounceDepth);

                pixel.Samples.Add(color);
            }
        }
        else
        {
            Color color = CalculatePixelColor(
                camera.CreateRayFor(pixel, false), _maxBounceDepth);

            pixel.Samples.Add(color);
        }
    }

    /// <summary>
    /// This method is used to determine the color seen by the specified ray.
    /// </summary>
    /// <param name="ray">The ray to get the color for.</param>
    /// <param name="maxDepth">The maximum bounce depth we want.</param>
    /// <returns>The color the ray "sees".</returns>
    private Color CalculatePixelColor(Ray ray, int maxDepth)
    {
        if (maxDepth <= 0)
            return Color.Black;

        Interval interval = new (0.001, double.PositiveInfinity);
        Intersection? intersection = _scene.FindHit(ray, interval);

        if (intersection != null)
        {
            Vector direction = intersection.Normal + Vector.RandomUnitVector();
            (Ray? scatterRay, Color attenuation) = intersection.Material.Scatter(ray, intersection);

            if (scatterRay != null)
                return CalculatePixelColor(scatterRay, maxDepth - 1) * attenuation;

            return Color.Black;
        }

        Vector unitDirection = ray.Direction.Unit();
        double a = (unitDirection.Y + 1) * 0.5;
        Vector colorData = new Vector(1, 1, 1) * (1 - a) +
                           new Vector(0.5, 0.7, 1.0) * a;

        return new Color(colorData);
    }
}
