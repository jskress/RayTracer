using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestCubes
{
    private record RayCubeTestCase(Point Origin, Vector Direction, double T1 = 0, double T2 = 0);
    private record CubeNormalTestCase(Point Point, Vector Normal);

    private static readonly List<RayCubeTestCase> RayCubeIntersectTestCases =
    [
        new (new Point(5, 0.5, 0), Directions.Left, 4, 6),
        new (new Point(-5, 0.5, 0), Directions.Right, 4, 6),
        new (new Point(0.5, 5, 0), Directions.Down, 4, 6),
        new (new Point(0.5, -5, 0), Directions.Up, 4, 6),
        new (new Point(0.5, 0, 5), Directions.Out, 4, 6),
        new (new Point(0.5, 0, -5), Directions.In, 4, 6),
        new (new Point(0, 0.5, 0), Directions.In, -1, 1)
    ];
    private static readonly List<RayCubeTestCase> RayCubeMissTestCases =
    [
        new (new Point(-2, 0, 0), new Vector(0.2673, 0.5345, 0.8018)),
        new (new Point(0, -2, 0), new Vector(0.8018, 0.2673, 0.5345)),
        new (new Point(0, 0, -2), new Vector(0.5345, 0.8018, 0.2673)),
        new (new Point(2, 0, 2), Directions.Out),
        new (new Point(0, 2, 2), Directions.Down),
        new (new Point(2, 2, 0), Directions.Left)
    ];

    private static readonly List<CubeNormalTestCase> CubeNormalTestCases =
    [
        new (new Point(1, 0.5, -0.8), Directions.Right),
        new (new Point(-1, -0.2, 0.9), Directions.Left),
        new (new Point(-0.4, 1, -0.1), Directions.Up),
        new (new Point(0.3, -1, -0.7), Directions.Down),
        new (new Point(-0.6, 0.3, 1), Directions.In),
        new (new Point(0.4, 0.4, -1), Directions.Out),
        new (new Point(1, 1, 1), Directions.Right),
        new (new Point(-1, -1, -1), Directions.Left)
    ];

    [TestMethod]
    public void TestRayCubeIntersection()
    {
        Cube cube = new ();

        foreach (RayCubeTestCase testCase in RayCubeIntersectTestCases)
        {
            Ray ray = new Ray(testCase.Origin, testCase.Direction);
            List<Intersection> intersections = new ();

            cube.AddIntersections(ray, intersections);

            intersections.Sort();

            Assert.AreEqual(2, intersections.Count);
            Assert.AreEqual(testCase.T1, intersections[0].Distance);
            Assert.AreEqual(testCase.T2, intersections[1].Distance);
        }
    }

    [TestMethod]
    public void TestRayCubeMiss()
    {
        Cube cube = new ();

        foreach (RayCubeTestCase testCase in RayCubeMissTestCases)
        {
            Ray ray = new Ray(testCase.Origin, testCase.Direction);
            List<Intersection> intersections = new ();

            cube.AddIntersections(ray, intersections);

            Assert.AreEqual(0, intersections.Count);
        }
    }

    [TestMethod]
    public void TestCubeNormal()
    {
        Cube cube = new ();

        foreach (CubeNormalTestCase testCase in CubeNormalTestCases)
            Assert.IsTrue(testCase.Normal.Matches(cube.SurfaceNormaAt(testCase.Point, null)));
    }
}
