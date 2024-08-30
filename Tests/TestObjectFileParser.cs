using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Instructions.Surfaces;

namespace Tests;

[TestClass]
public class TestObjectFileParser
{
    private const string Garbage =
        """
        There was a young lady named Bright
        who traveled much faster than light.
        She set out one day
        in a relative way,
        and came back the previous night.
        """;
    private const string Vertices =
        """
        v -1 1 0
        v -1.00000 0.50000 0.00000
        v 1 0 0
        v 1 1 0
        """;
    private const string Triangles =
        """
        v -1 1 0
        v -1 0 0
        v 1 0 0
        v 1 1 0

        f 1 2 3
        f 1 3 4
        """;
    private const string Polygons =
        """
        v -1 1 0
        v -1 0 0
        v 1 0 0
        v 1 1 0
        v 0 2 0

        f 1 2 3 4 5
        """;
    private const string Groups =
        """
        v -1 1 0
        v -1 0 0
        v 1 0 0
        v 1 1 0

        g FirstGroup
        f 1 2 3
        g SecondGroup
        f 1 3 4
        """;
    private const string Normals =
        """
        vn 0 0 1
        vn 0.707 0 -0.707
        vn 1 2 3
        """;
    private const string SmoothTriangles =
        """
        v 0 1 0
        v -1 0 0
        v 1 0 0
        
        vn -1 0 0
        vn 1 0 0
        vn 0 1 0

        f 1//3 2//1 3//2
        f 1/0/3 2/102/1 3/14/2
        """;

    [TestMethod]
    public void TestGarbageFile()
    {
        ObjectFileParser parser = new (content: Garbage);
        Group group = parser.Parse();

        Assert.AreEqual(0, parser.Vertices.Count);
        Assert.AreEqual(0, parser.Triangles.Count);
        Assert.IsNull(group);
    }

    [TestMethod]
    public void TestVertices()
    {
        ObjectFileParser parser = new (content: Vertices);

        _ = parser.Parse();

        Assert.AreEqual(4, parser.Vertices.Count);
        Assert.IsTrue(new Point(-1, 1, 0).Matches(parser.Vertices[0]));
        Assert.IsTrue(new Point(-1, 0.5, 0).Matches(parser.Vertices[1]));
        Assert.IsTrue(new Point(1, 0, 0).Matches(parser.Vertices[2]));
        Assert.IsTrue(new Point(1, 1, 0).Matches(parser.Vertices[3]));
    }

    [TestMethod]
    public void TestTriangles()
    {
        ObjectFileParser parser = new (content: Triangles);
        Group group = parser.Parse();

        Assert.AreEqual(4, parser.Vertices.Count);
        Assert.AreEqual(2, parser.Triangles.Count);
        Assert.AreEqual(2, group.Surfaces.Count);
        Assert.AreSame(group.Surfaces[0], parser.Triangles[0]);
        Assert.AreSame(group.Surfaces[1], parser.Triangles[1]);

        Triangle t1 = (Triangle) group.Surfaces[0];
        Triangle t2 = (Triangle) group.Surfaces[1];

        Assert.AreSame(parser.Vertices[2], t1.Point1);
        Assert.AreSame(parser.Vertices[1], t1.Point2);
        Assert.AreSame(parser.Vertices[0], t1.Point3);
        Assert.AreSame(parser.Vertices[3], t2.Point1);
        Assert.AreSame(parser.Vertices[2], t2.Point2);
        Assert.AreSame(parser.Vertices[0], t2.Point3);
    }

    [TestMethod]
    public void TestPolygons()
    {
        ObjectFileParser parser = new (content: Polygons);
        Group group = parser.Parse();

        Assert.AreEqual(5, parser.Vertices.Count);
        Assert.AreEqual(3, parser.Triangles.Count);
        Assert.AreEqual(3, group.Surfaces.Count);
        Assert.AreSame(group.Surfaces[0], parser.Triangles[0]);
        Assert.AreSame(group.Surfaces[1], parser.Triangles[1]);
        Assert.AreSame(group.Surfaces[2], parser.Triangles[2]);

        Triangle t1 = (Triangle) group.Surfaces[0];
        Triangle t2 = (Triangle) group.Surfaces[1];
        Triangle t3 = (Triangle) group.Surfaces[2];

        Assert.AreSame(parser.Vertices[4], t1.Point1);
        Assert.AreSame(parser.Vertices[3], t1.Point2);
        Assert.AreSame(parser.Vertices[2], t1.Point3);
        Assert.AreSame(parser.Vertices[4], t2.Point1);
        Assert.AreSame(parser.Vertices[2], t2.Point2);
        Assert.AreSame(parser.Vertices[1], t2.Point3);
        Assert.AreSame(parser.Vertices[4], t3.Point1);
        Assert.AreSame(parser.Vertices[1], t3.Point2);
        Assert.AreSame(parser.Vertices[0], t3.Point3);
    }

    [TestMethod]
    public void TestGroups()
    {
        ObjectFileParser parser = new (content: Groups);
        Group group = parser.Parse();

        Assert.AreEqual(4, parser.Vertices.Count);
        Assert.AreEqual(2, parser.Triangles.Count);
        Assert.AreEqual(2, group.Surfaces.Count);
        Assert.IsTrue(group.Surfaces[0] is Group);
        Assert.IsTrue(group.Surfaces[1] is Group);

        Group g1 = (Group) group.Surfaces[0];
        Group g2 = (Group) group.Surfaces[1];

        Assert.AreEqual(1, g1.Surfaces.Count);
        Assert.AreEqual(1, g2.Surfaces.Count);
        Assert.IsTrue(g1.Surfaces[0] is Triangle);
        Assert.IsTrue(g2.Surfaces[0] is Triangle);

        Triangle t1 = (Triangle) g1.Surfaces[0];
        Triangle t2 = (Triangle) g2.Surfaces[0];

        Assert.AreSame(parser.Vertices[2], t1.Point1);
        Assert.AreSame(parser.Vertices[1], t1.Point2);
        Assert.AreSame(parser.Vertices[0], t1.Point3);
        Assert.AreSame(parser.Vertices[3], t2.Point1);
        Assert.AreSame(parser.Vertices[2], t2.Point2);
        Assert.AreSame(parser.Vertices[0], t2.Point3);
    }

    [TestMethod]
    public void TestVertexNormals()
    {
        ObjectFileParser parser = new (content: Normals);

        _ = parser.Parse();

        Assert.AreEqual(3, parser.Normals.Count);
        Assert.IsTrue(Directions.In.Matches(parser.Normals[0]));
        Assert.IsTrue(new Vector(0.707, 0, -0.707).Matches(parser.Normals[1]));
        Assert.IsTrue(new Vector(1, 2, 3).Matches(parser.Normals[2]));
    }

    [TestMethod]
    public void TestSmoothTriangles()
    {
        ObjectFileParser parser = new (content: SmoothTriangles);
        Group group = parser.Parse();

        Assert.AreEqual(3, parser.Vertices.Count);
        Assert.AreEqual(3, parser.Normals.Count);
        Assert.AreEqual(2, parser.Triangles.Count);
        Assert.AreEqual(2, group.Surfaces.Count);
        Assert.AreSame(group.Surfaces[0], parser.Triangles[0]);
        Assert.AreSame(group.Surfaces[1], parser.Triangles[1]);
        Assert.IsTrue(group.Surfaces[0] is SmoothTriangle);
        Assert.IsTrue(group.Surfaces[1] is SmoothTriangle);

        SmoothTriangle t1 = (SmoothTriangle) group.Surfaces[0];
        SmoothTriangle t2 = (SmoothTriangle) group.Surfaces[1];

        Assert.AreSame(parser.Vertices[2], t1.Point1);
        Assert.AreSame(parser.Vertices[1], t1.Point2);
        Assert.AreSame(parser.Vertices[0], t1.Point3);
        Assert.AreSame(parser.Normals[1], t1.Normal1);
        Assert.AreSame(parser.Normals[0], t1.Normal2);
        Assert.AreSame(parser.Normals[2], t1.Normal3);
        Assert.AreSame(t1.Point1, t2.Point1);
        Assert.AreSame(t1.Point2, t2.Point2);
        Assert.AreSame(t1.Point3, t2.Point3);
    }
}
