using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class triangulates a simple (non-self-intersecting) 2D polygon via ear clipping --
/// repeatedly finding a vertex whose triangle with its two neighbors is both convex and
/// contains no other vertex of the polygon, "clipping" it off, and repeating until only one
/// triangle remains.  Unlike a naive fan triangulation from the centroid, this works
/// correctly for concave polygons (like a star), where a fan would produce triangles that
/// poke outside the polygon's own outline.
/// </summary>
public static class PolygonTriangulator
{
    /// <summary>
    /// This method triangulates the given polygon.
    /// </summary>
    /// <param name="polygon">The polygon's vertices, in order (either winding).  The first
    /// and last points must not be duplicated -- this is a plain list of unique vertices,
    /// not a closed loop.</param>
    /// <returns>The triangles making up the polygon, each a triple of indices into
    /// <paramref name="polygon"/>.</returns>
    public static List<(int A, int B, int C)> Triangulate(IReadOnlyList<TwoDPoint> polygon)
    {
        List<int> indices = Enumerable.Range(0, polygon.Count).ToList();
        List<(int A, int B, int C)> triangles = [];
        bool clockwise = SignedArea(polygon) < 0;
        int maxIterations = polygon.Count * polygon.Count;

        while (indices.Count > 3 && maxIterations-- > 0)
        {
            int earAt = FindEar(polygon, indices, clockwise);

            if (earAt < 0)
                break; // The remaining outline isn't simple; stop with whatever we have.

            int count = indices.Count;
            int prev = indices[(earAt - 1 + count) % count];
            int curr = indices[earAt];
            int next = indices[(earAt + 1) % count];

            triangles.Add((prev, curr, next));
            indices.RemoveAt(earAt);
        }

        if (indices.Count == 3)
            triangles.Add((indices[0], indices[1], indices[2]));

        return triangles;
    }

    /// <summary>
    /// This method finds the index (into <paramref name="indices"/>) of a vertex that forms
    /// a valid ear with its two current neighbors.
    /// </summary>
    private static int FindEar(IReadOnlyList<TwoDPoint> polygon, List<int> indices, bool clockwise)
    {
        int count = indices.Count;

        for (int i = 0; i < count; i++)
        {
            int prev = indices[(i - 1 + count) % count];
            int curr = indices[i];
            int next = indices[(i + 1) % count];

            if (IsEar(polygon, indices, prev, curr, next, clockwise))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// This method determines whether the triangle formed by a vertex and its two current
    /// neighbors is a valid ear: convex (turning the same way as the polygon's overall
    /// winding), and containing none of the polygon's other remaining vertices.
    /// </summary>
    private static bool IsEar(
        IReadOnlyList<TwoDPoint> polygon, List<int> indices, int prev, int curr, int next, bool clockwise)
    {
        TwoDPoint a = polygon[prev];
        TwoDPoint b = polygon[curr];
        TwoDPoint c = polygon[next];
        double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        if (clockwise ? cross > 0 : cross < 0)
            return false;

        foreach (int index in indices)
        {
            if (index == prev || index == curr || index == next)
                continue;

            if (IsPointInTriangle(polygon[index], a, b, c))
                return false;
        }

        return true;
    }

    /// <summary>
    /// This method returns the signed area of the given polygon (positive for
    /// counter-clockwise winding, negative for clockwise), via the shoelace formula.
    /// </summary>
    private static double SignedArea(IReadOnlyList<TwoDPoint> polygon)
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

    /// <summary>
    /// This method determines whether the given point lies within (or on the boundary of)
    /// the given triangle, via barycentric sign consistency -- the point is inside exactly
    /// when it's on the same side of all three edges.
    /// </summary>
    private static bool IsPointInTriangle(TwoDPoint point, TwoDPoint a, TwoDPoint b, TwoDPoint c)
    {
        double d1 = Cross(point, a, b);
        double d2 = Cross(point, b, c);
        double d3 = Cross(point, c, a);
        bool hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPositive = d1 > 0 || d2 > 0 || d3 > 0;

        return !(hasNegative && hasPositive);
    }

    private static double Cross(TwoDPoint point, TwoDPoint a, TwoDPoint b)
    {
        return (a.X - point.X) * (b.Y - point.Y) - (a.Y - point.Y) * (b.X - point.X);
    }
}
