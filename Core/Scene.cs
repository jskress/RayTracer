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

        Matte ground = new (new Color(0.8, 0.8, 0));
        Matte center = new (new Color(0.1, 0.2, 0.5));
        Glass left = new (1.5);
        Metal right = new (new Color(0.8, 0.6, 0.2), 0);

        scene.Add(new Sphere(new Point(0, -100.5, -1), 100, ground));
        scene.Add(new Sphere(new Point(0, 0, -1), 0.5, center));
        scene.Add(new Sphere(new Point(-1, 0, -1), 0.5, left));
        scene.Add(new Sphere(new Point(-1, 0, -1), -0.4, left));
        scene.Add(new Sphere(new Point(1, 0, -1), 0.5, right));

        camera.Location = new Point(-2, 2, 1);
        camera.LookAt = new Point(0, 0, -1);
        camera.Up = new Vector(0, 1, 0);
        camera.VerticalFieldOfView = 20;
        camera.DefocusAngle = 10;
        camera.FocusDistance = 3.4;

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
