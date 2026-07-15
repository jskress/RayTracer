using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestPolygonTriangulator
{
    /// <summary>
    /// A simple convex square must triangulate into exactly 2 triangles (n - 2, for n = 4),
    /// whose combined area matches the square's own area exactly.
    /// </summary>
    [TestMethod]
    public void TestSquareProducesTwoTrianglesWithMatchingArea()
    {
        List<TwoDPoint> square =
        [
            new TwoDPoint(-1, -1), new TwoDPoint(1, -1), new TwoDPoint(1, 1), new TwoDPoint(-1, 1)
        ];

        List<(int A, int B, int C)> triangles = PolygonTriangulator.Triangulate(square);

        Assert.AreEqual(2, triangles.Count);
        Assert.IsTrue(4.0.Near(TotalArea(square, triangles)));
    }

    /// <summary>
    /// A concave "L" shape must still triangulate into n - 2 triangles with matching total
    /// area, and every one of those triangles must actually lie within the L's own outline
    /// -- the key property a naive fan-from-centroid triangulation would violate for a
    /// concave shape, but ear clipping must not.
    /// </summary>
    [TestMethod]
    public void TestConcaveLShapeStaysWithinItsOwnOutline()
    {
        List<TwoDPoint> lShape =
        [
            new TwoDPoint(0, 0), new TwoDPoint(2, 0), new TwoDPoint(2, 1),
            new TwoDPoint(1, 1), new TwoDPoint(1, 2), new TwoDPoint(0, 2)
        ];
        GeneralPath outline = new GeneralPath()
            .MoveTo(lShape[0].X, lShape[0].Y);

        foreach (TwoDPoint point in lShape.Skip(1))
            outline.LineTo(point.X, point.Y);

        outline.ClosePath();

        List<(int A, int B, int C)> triangles = PolygonTriangulator.Triangulate(lShape);

        Assert.AreEqual(4, triangles.Count);
        Assert.IsTrue(3.0.Near(TotalArea(lShape, triangles)));

        foreach ((int a, int b, int c) in triangles)
        {
            TwoDPoint centroid = new (
                (lShape[a].X + lShape[b].X + lShape[c].X) / 3,
                (lShape[a].Y + lShape[b].Y + lShape[c].Y) / 3);

            Assert.IsTrue(outline.Contains(centroid));
        }
    }

    /// <summary>
    /// A genuinely concave, self-crossing-adjacent star shape (the same profile used in the
    /// sweep gallery scene) must triangulate into n - 2 triangles with matching total area,
    /// and, just as with the L shape, every triangle must lie within the star's own
    /// outline -- confirming ear clipping correctly avoids the points where a naive fan
    /// triangulation would poke out between the star's arms.
    /// </summary>
    [TestMethod]
    public void TestConcaveStarStaysWithinItsOwnOutline()
    {
        List<TwoDPoint> star = BuildStar(outer: 1.0, inner: 0.4, points: 5);
        GeneralPath outline = new GeneralPath()
            .MoveTo(star[0].X, star[0].Y);

        foreach (TwoDPoint point in star.Skip(1))
            outline.LineTo(point.X, point.Y);

        outline.ClosePath();

        List<(int A, int B, int C)> triangles = PolygonTriangulator.Triangulate(star);

        Assert.AreEqual(star.Count - 2, triangles.Count);
        Assert.IsTrue(Math.Abs(SignedArea(star)).Near(TotalArea(star, triangles), 0.0001));

        foreach ((int a, int b, int c) in triangles)
        {
            TwoDPoint centroid = new (
                (star[a].X + star[b].X + star[c].X) / 3,
                (star[a].Y + star[b].Y + star[c].Y) / 3);

            Assert.IsTrue(outline.Contains(centroid));
        }
    }

    /// <summary>
    /// Builds a regular star polygon's vertices, alternating between the outer and inner
    /// radius.
    /// </summary>
    private static List<TwoDPoint> BuildStar(double outer, double inner, int points)
    {
        List<TwoDPoint> result = [];

        for (int i = 0; i < points * 2; i++)
        {
            double angle = Math.PI / 2 + i * Math.PI / points;
            double radius = i % 2 == 0 ? outer : inner;

            result.Add(new TwoDPoint(radius * Math.Cos(angle), radius * Math.Sin(angle)));
        }

        return result;
    }

    private static double TotalArea(List<TwoDPoint> polygon, List<(int A, int B, int C)> triangles)
    {
        return triangles.Sum(triangle =>
        {
            TwoDPoint a = polygon[triangle.A];
            TwoDPoint b = polygon[triangle.B];
            TwoDPoint c = polygon[triangle.C];

            return Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) / 2;
        });
    }

    private static double SignedArea(List<TwoDPoint> polygon)
    {
        double sum = 0;

        for (int i = 0; i < polygon.Count; i++)
        {
            TwoDPoint a = polygon[i];
            TwoDPoint b = polygon[(i + 1) % polygon.Count];

            sum += a.X * b.Y - b.X * a.Y;
        }

        return sum / 2;
    }
}
