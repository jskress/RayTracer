using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Materials;
using RayTracer.Shapes;

namespace RayTracer.Core;

public class Scene
{
    public static Scene CreateDefaultScene(Camera camera)
    {
        Scene scene = new (camera);
        // Matte left = new (new Color(0, 0, 1));
        // Matte right = new (new Color(1, 0, 0));
        // double radius = Math.Cos(double.Pi / 4);
        //
        // scene.Add(new Sphere(new Point(-radius, 0, -1), radius, left));
        // scene.Add(new Sphere(new Point(radius, 0, -1), radius, right));

        // Matte ground = new (new Color(0.8, 0.8, 0));
        // Matte center = new (new Color(0.1, 0.2, 0.5));
        // Glass left = new (1.5);
        // Metal right = new (new Color(0.8, 0.6, 0.2), 0);
        //
        // scene.Add(new Sphere(new Point(0, -100.5, -1), 100, ground));
        // scene.Add(new Sphere(new Point(0, 0, -1), 0.5, center));
        // scene.Add(new Sphere(new Point(-1, 0, -1), 0.5, left));
        // scene.Add(new Sphere(new Point(-1, 0, -1), -0.4, left));
        // scene.Add(new Sphere(new Point(1, 0, -1), 0.5, right));
        //
        // camera.Location = new Point(-2, 2, 1);
        // camera.LookAt = new Point(0, 0, -1);
        // camera.Up = new Vector(0, 1, 0);
        // camera.VerticalFieldOfView = 20;
        // camera.DefocusAngle = 0;
        // camera.FocusDistance = 3.4;

        Matte ground = new (new Color(0.5, 0.5, 0.5));
        Glass glass = new (1.5);
        Matte matte = new (new Color(0.4, 0.2, 0.1));
        Metal metal = new (new Color(0.7, 0.6, 0.5), 0);
        Point point = new (4, 0.2, 0);

        scene.Add(new Sphere(new Point(0, -1000, 0), 1000, ground));

        for (int a = -11; a < 11; a++)
        {
            for (int b = -11; b < 11; b++)
            {
                double materialSelector = DoubleExtensions.RandomDouble();
                double x = DoubleExtensions.RandomDouble() * 0.9 + a;
                double z = DoubleExtensions.RandomDouble() * 0.9 + b;
                Point center = new (x, 0.2, z);

                if ((center - point).Length > 0.9)
                {
                    Material material;

                    if (materialSelector < 0.8)
                    {
                        Color color = Color.Random() * Color.Random();

                        material = new Matte(color);
                    }
                    else if (materialSelector < 0.95)
                    {
                        Color color = Color.Random(0.5);
                        double fuzz = DoubleExtensions.RandomDouble(0, 0.5);

                        material = new Metal(color, fuzz);
                    }
                    else
                        material = glass;

                    scene.Add(new Sphere(center, 0.2, material));
                }
            }
        }

        scene.Add(new Sphere(new Point(0, 1, 0), 1.0, glass));
        scene.Add(new Sphere(new Point(-4, 1, 0), 1.0, matte));
        scene.Add(new Sphere(new Point(4, 1, 0), 1.0, metal));

        camera.VerticalFieldOfView = 20;
        camera.Location = new Point(13, 2, 3);
        camera.LookAt = new Point(0, 0, 0);
        camera.Up = new Vector(0, 1, 0);
        camera.DefocusAngle = 0;
        camera.FocusDistance = 10.0;

        return scene;
    }

    private readonly List<Shape> _shapes = new();

    public Camera Camera { get; }

    public Scene(Camera camera)
    {
        Camera = camera;
    }

    /// <summary>
    /// This method is used to add a shape to our scene.
    /// </summary>
    /// <param name="shape"></param>
    public void Add(Shape shape)
    {
        _shapes.Add(shape);
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects any
    /// shapes in the scene.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="interval">The interval of acceptable values for distance.</param>
    /// <returns>A ray/shape intersection object describing the intersection, or
    /// <c>null</c>, if the ray missed us.</returns>
    public Intersection? FindHit(Ray ray, Interval interval)
    {
        Intersection? result = null;
        Interval workingInterval = new (interval);

        foreach (Shape shape in _shapes)
        {
            Intersection? intersection = shape.FindHit(ray, workingInterval);

            if (intersection != null)
            {
                result = intersection;
                workingInterval.Maximum = result.Distance;
            }
        }

        return result;
    }
}
