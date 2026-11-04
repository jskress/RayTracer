using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestGroups
{
    [TestMethod]
    public void TestConstruction()
    {
        Group group = new ();

        Assert.IsTrue(Matrix.Identity.Matches(group.Transform));
        Assert.AreEqual(0, group.Surfaces.Count);
        Assert.IsNull(group.Parent);
    }

    [TestMethod]
    public void TestAddSurface()
    {
        Group group = new ();
        Sphere sphere = new ();

        group.Add(sphere);

        Assert.AreEqual(1, group.Surfaces.Count);
        Assert.AreSame(sphere, group.Surfaces[0]);
        Assert.AreSame(group, sphere.Parent);
    }

    [TestMethod]
    public void TestEmptyGroupIntersection()
    {
        Group group = new ();
        Ray ray = new (Point.Zero, Directions.In);
        List<Intersection> intersections = new ();

        group.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestNonEmptyGroupIntersection()
    {
        Group group = new ();
        Sphere s1 = new ();
        Sphere s2 = new ()
        {
            Transform = Transforms.Translate(0, 0, -3)
        };
        Sphere s3 = new ()
        {
            Transform = Transforms.Translate(5, 0, 0)
        };
        Ray ray = new (new Point(0, 0, -5), Directions.In);
        List<Intersection> intersections = new ();

        group.Add(s1);
        group.Add(s2);
        group.Add(s3);

        group.AddIntersections(ray, intersections);

        Assert.AreEqual(4, intersections.Count);
        Assert.AreSame(s2, intersections[0].Surface);
        Assert.AreSame(s2, intersections[1].Surface);
        Assert.AreSame(s1, intersections[2].Surface);
        Assert.AreSame(s1, intersections[3].Surface);
    }

    [TestMethod]
    public void TestGroupTransforms()
    {
        Group group = new ()
        {
            Transform = Transforms.Scale(2)
        };
        Sphere sphere = new ()
        {
            Transform = Transforms.Translate(5, 0, 0)
        };
        Ray ray = new Ray(new Point(10, 0, -10), Directions.In);
        List<Intersection> intersections = new ();

        group.Add(sphere);
        group.Intersect(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
    }

    [TestMethod]
    public void TestWorldToSurface()
    {
        Group outer = new ()
        {
            Transform = Transforms.RotateAroundY(Math.PI / 2, true)
        };
        Group inner = new ()
        {
            Transform = Transforms.Scale(2)
        };
        Sphere sphere = new ()
        {
            Transform = Transforms.Translate(5, 0,0)
        };

        outer.Add(inner);
        inner.Add(sphere);

        Point point = sphere.WorldToSurface(new Point(-2, 0, -10));
        Point expected = new (0, 0, -1);

        Assert.IsTrue(expected.Matches(point));
    }

    [TestMethod]
    public void TestNormalToWorld()
    {
        Group outer = new ()
        {
            Transform = Transforms.RotateAroundY(Math.PI / 2, true)
        };
        Group inner = new ()
        {
            Transform = Transforms.Scale(1, 2, 3)
        };
        Sphere sphere = new ()
        {
            Transform = Transforms.Translate(5, 0,0)
        };

        outer.Add(inner);
        inner.Add(sphere);

        double value = Math.Sqrt(3) / 3;
        Vector normal = sphere.NormalToWorld(new Vector(value, value, value));
        Vector expected = new (0.285714, 0.428571, -0.857143);

        Assert.IsTrue(expected.Matches(normal));
    }

    [TestMethod]
    public void TestBoundingBoxAccountsForChildTransforms()
    {
        // The group itself has no transform, but its child triangle is translated well
        // away from the origin.  The group's default bounding box must be built from each
        // child's extent *as transformed by that child's own transform* -- otherwise it
        // ends up centered on the origin instead of around the translated triangle, and a
        // ray that should hit the triangle gets rejected by the (wrongly-placed) box before
        // AddIntersections() ever runs.
        Group group = new ();
        Triangle triangle = new ()
        {
            Point1 = new Point(0, 0, 0),
            Point2 = new Point(1, 0, 0),
            Point3 = new Point(0, 1, 0),
            Transform = Transforms.Translate(10, 0, 0)
        };

        group.Add(triangle);
        group.PrepareForRendering();

        Ray ray = new (new Point(10.25, 0.25, -5), Directions.In);
        List<Intersection> intersections = [];

        group.Intersect(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
    }

    [TestMethod]
    public void TestGroupNormalAt()
    {
        Group outer = new ()
        {
            Transform = Transforms.RotateAroundY(Math.PI / 2, true)
        };
        Group inner = new ()
        {
            Transform = Transforms.Scale(1, 2, 3)
        };
        Sphere sphere = new ()
        {
            Transform = Transforms.Translate(5, 0,0)
        };

        outer.Add(inner);
        inner.Add(sphere);

        Vector normal = sphere.NormalAt(new Point(1.7321, 1.1547, -5.5774), null);
        Vector expected = new (0.285704, 0.428543, -0.857161);

        Assert.IsTrue(expected.Matches(normal));
    }

    /// <summary>
    /// A group's children are cut into boxes by where they spread in the world, not in the group's own space.  Rough
    /// ground written in meters up over its own unit square across, then stretched nine kilometers each way -- as a
    /// height field said in meters is -- spreads three thousand of its own units up and one across: cut by its own
    /// spread, every box was a slab as wide as the land, since rough ground at any one height lies all over it, and
    /// a ray straight down was handed most of the triangles.  Cut by the world's, it is handed a few.
    /// </summary>
    [TestMethod]
    public void TestAStretchedGroupIsDividedAsItStandsInTheWorld()
    {
        Group Slope(Matrix transform)
        {
            Group group = new () { Transform = transform };

            for (int row = 0; row < 60; row++)
            {
                for (int column = 0; column < 60; column++)
                {
                    double x = column / 60.0, z = row / 60.0, step = 1 / 60.0;
                    Point At(double u, double v) => new (u, 3000 * Rough(u, v), v);

                    group.Add(new Triangle
                    {
                        Point1 = At(x, z), Point2 = At(x + step, z), Point3 = At(x + step, z + step)
                    });
                    group.Add(new Triangle
                    {
                        Point1 = At(x, z), Point2 = At(x + step, z + step), Point3 = At(x, z + step)
                    });
                }
            }

            group.PrepareForRendering();

            return group;
        }

        // Ground as rough as noise: a height for each corner of the grid, as good as random.
        static double Rough(double u, double v)
        {
            double value = Math.Sin(Math.Round(u * 60) * 12.9898 + Math.Round(v * 60) * 78.233) * 43758.5453;

            return value - Math.Floor(value);
        }

        Ray down = new (new Point(0.31, 5000, 0.57), new Vector(0, -1, 0));
        int stretched = Slope(Transforms.Scale(9000, 1, 9000)).ChildrenHandedTo(down);

        Assert.IsTrue(stretched < 60, $"a ray straight down was handed {stretched} of 7200 triangles");
    }
}
