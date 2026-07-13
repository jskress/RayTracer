using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestLathe
{
    [TestMethod]
    public void TestShapeIntersections()
    {
        Point origin = new Point(2, 2, 2);
        Point lookAt = new Point(0, 1, 0);
        Vector direction = lookAt - origin;
        Ray ray = new Ray(origin, direction.Unit);
        using GeneralPath cylinder = new GeneralPath()
            .MoveTo(0, 0)
            .LineTo(1, 0)
            .LineTo(1, 2)
            .LineTo(0, 2);
        Lathe lathe = new Lathe { Path = cylinder };
        TwoDRay shapeRay = TwoDRay.ProjectedToXy(ray);

        lathe.PrepareForRendering();

        // Verify some basic stuff.
        Assert.AreEqual(6, cylinder.Segments.Count);
        Assert.AreEqual(2.0, shapeRay.Origin.X);
        Assert.AreEqual(2.0, shapeRay.Origin.Y);
        Assert.IsTrue(shapeRay.Direction.X.Near(-0.666667));
        Assert.IsTrue(shapeRay.Direction.Y.Near(-0.333333));

        // Verify cylinder structure.
        Line line = cylinder.Segments[0] as Line;
        Assert.IsNotNull(line);
        Assert.AreEqual(new TwoDPoint(0, 0), line.Points[0]);
        Assert.AreEqual(new TwoDPoint(1, 0), line.Points[1]);

        // Make sure our shape ray does not intersect segments it shouldn't
        Assert.AreEqual(0, cylinder.Segments[0].GetIntersections(shapeRay).Count());
        Assert.AreEqual(0, cylinder.Segments[2].GetIntersections(shapeRay).Count());
        Assert.AreEqual(0, cylinder.Segments[3].GetIntersections(shapeRay).Count());
        Assert.AreEqual(0, cylinder.Segments[5].GetIntersections(shapeRay).Count());

        // Now let's verify the shape intersections.
        // The first one...
        TwoDIntersection[] intersections = cylinder.Segments[1].GetIntersections(shapeRay).ToArray();

        Assert.AreEqual(1, intersections.Length);
        Assert.IsTrue(intersections[0].Distance.Near(1.5));
        Assert.AreEqual(new TwoDPoint(1, 1.5), intersections[0].Point);
        Assert.AreEqual(new TwoDVector(1, 0), intersections[0].TwoDNormal);

        // The second one...
        intersections = cylinder.Segments[4].GetIntersections(shapeRay).ToArray();

        Assert.AreEqual(1, intersections.Length);
        Assert.IsTrue(intersections[0].Distance.Near(4.5));
        Assert.AreEqual(new TwoDPoint(-1, 0.5), intersections[0].Point);
        Assert.AreEqual(new TwoDVector(-1, 0), intersections[0].TwoDNormal);
    }

    [TestMethod]
    public void TestCircleIntersections()
    {
        Point origin = new Point(2, 1, 1);
        Point lookAt = new Point(0, 0, 0);
        Vector direction = lookAt - origin;
        Ray ray = new Ray(origin, direction.Unit);
        double theta = Math.Atan(ray.Origin.Z / ray.Origin.X);
        Ray rotatedRay = Transforms.RotateAroundY(theta, true).Transform(ray);

        Console.WriteLine($"ray: {ray.Origin}, {ray.Direction}");
        Console.WriteLine($"theta: {theta}");
        Console.WriteLine($"theta: {theta.ToDegrees()}");
        Console.WriteLine($"rotated ray: {rotatedRay.Origin}, {rotatedRay.Direction}");

        // using GeneralPath cylinder = new GeneralPath()
        //     .MoveTo(0, 0)
        //     .LineTo(1, 0)
        //     .LineTo(1, 2)
        //     .LineTo(0, 2);
        // Lathe lathe = new Lathe { Path = cylinder };
        // List<LathePathSurface> surfaces = GetField<List<LathePathSurface>>(lathe, "_surfaces");
        // TwoDRay shapeRay = TwoDRay.ProjectedToXy(ray);
        // TwoDRay circleRay = TwoDRay.ProjectedToXz(ray);
        //
        // lathe.PrepareForRendering();
        //
        // // Verify some basic stuff.
        // Assert.AreEqual(6, surfaces!.Count);
        // Assert.AreEqual(2.0, circleRay.Origin.X);
        // Assert.AreEqual(2.0, circleRay.Origin.Y);
        // Assert.IsTrue(circleRay.Direction.X.Near(-0.666667));
        // Assert.IsTrue(circleRay.Direction.Y.Near(-0.666667));
        //
        // // Get all the shape intersections.
        // TwoDIntersection[] intersections = cylinder.Segments
        //     .Select(segment => segment.GetIntersections(shapeRay))
        //     .SelectMany(intersections => intersections)
        //     .ToArray();
        //
        // Assert.AreEqual(2, intersections.Length);
        //
        // // Now verify the details about the first point of intersection.
        // double[] t = LathePathSurface.GetTOnCircle(circleRay, intersections[0].Point.X)
        //     .ToArray();
        //
        // Assert.AreEqual(2, t.Length);
        // Assert.IsTrue(t[0].Near(1.93934));
        // Assert.IsTrue(t[1].Near(4.06066));
        //
        // TwoDPoint ip1 = circleRay.At(t[0]);
        // TwoDPoint ip2 = circleRay.At(t[1]);
        // Point p1 = new Point(ip1.X, intersections[0].Point.Y, ip1.Y);
        // Point p2 = new Point(ip2.X, intersections[0].Point.Y, ip2.Y);
        //
        // // Point 1 should be on the ray, but point 2 should not.
        // Assert.IsTrue(ray.Contains(p1));
        // Assert.IsFalse(ray.Contains(p2));
        //
        // // Now verify the details about the second point of intersection.
        // t = LathePathSurface.GetTOnCircle(circleRay, intersections[1].Point.X)
        //     .ToArray();
        //
        // Assert.AreEqual(2, t.Length);
        // Assert.IsTrue(t[0].Near(1.93934));
        // Assert.IsTrue(t[1].Near(4.06066));
        //
        // ip1 = circleRay.At(t[0]);
        // ip2 = circleRay.At(t[1]);
        // p1 = new Point(ip1.X, intersections[1].Point.Y, ip1.Y);
        // p2 = new Point(ip2.X, intersections[1].Point.Y, ip2.Y);
        //
        // // Point 2 should be on the ray, but point 1 should not.
        // Assert.IsFalse(ray.Contains(p1));
        // Assert.IsTrue(ray.Contains(p2));

        // TwoDPoint onCircle = circleRay.At(t[0]);
        // double theta = Math.Acos(onCircle.X / intersections[0].Point.X);

        // We're getting the wrong value of t here...
        //t = LathePathSurface.GetTOnCircle(circleRay, intersections[1].Point.X);
    }

    private static void Show(TwoDPoint point, string label = null)
    {
        label = label == null ? "" : $"{label}: ";

        Console.WriteLine($"--> {label}({point.X * 300}, {point.Y * -300})");
    }

    private static void Show(NumberTuple tuple, string label = null)
    {
        label = label == null ? "" : $"{label}: ";

        Console.WriteLine($"--> {label}({tuple.X}, {tuple.Y}, {tuple.Z})");
    }

    private static void Show(TwoDVector vector, string label = null)
    {
        label = label == null ? "" : $"{label}: ";

        Console.WriteLine($"--> {label}({vector.X * 300}, {vector.Y * -300})");
    }

    private static T GetField<T>(object obj, string name)
    {
        return (T) obj.GetType()
            .GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(obj);
    }
}
