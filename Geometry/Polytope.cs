using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class holds a convex polytope as its faces: each a plane, n.x = d with n pointing out, and the polygon of
/// the plane that bounds the solid, its corners running counterclockwise seen from outside.  It is what a fracture
/// cuts its pieces from, a half-space at a time, and keeping the polygons -- not just the planes -- is what lets a
/// piece be boxed, have its middle found, and have its edges and corners rounded.
/// </summary>
internal sealed class Polytope
{
    /// <summary>
    /// This class holds one face of a polytope.
    /// </summary>
    internal sealed class Face(Vector normal, double offset, List<Point> corners)
    {
        public Vector Normal { get; } = normal;
        public double Offset { get; } = offset;
        public List<Point> Corners { get; } = corners;
    }

    /// <summary>
    /// This property holds the polytope's faces.
    /// </summary>
    public List<Face> Faces { get; }

    // How near a corner may be to a plane and still be on it, as a share of the polytope's size.
    private const double Tolerance = 1e-9;

    private Polytope(List<Face> faces)
    {
        Faces = faces;
    }

    /// <summary>
    /// This method returns the box with the given corners as a polytope.
    /// </summary>
    public static Polytope Box(Point low, Point high)
    {
        return Parallelepiped(Matrix.Identity, low, high);
    }

    /// <summary>
    /// This method returns a cube -- from -1 to 1 on every axis -- carried by a transform: a box, turned, stretched
    /// or sheared, which is still bounded by six planes.
    /// </summary>
    public static Polytope Cube(Matrix transform)
    {
        return Parallelepiped(transform, new Point(-1, -1, -1), new Point(1, 1, 1));
    }

    private static Polytope Parallelepiped(Matrix transform, Point low, Point high)
    {
        Point At(int x, int y, int z) => transform * new Point(
            x == 0 ? low.X : high.X, y == 0 ? low.Y : high.Y, z == 0 ? low.Z : high.Z);

        // Each face's corners counterclockwise seen from outside it, for a transform that keeps handedness; one
        // that mirrors turns them all the other way, and each face's normal is taken from its own corners, so it
        // still points out.
        int[][][] faces =
        [
            [[1, 0, 0], [1, 1, 0], [1, 1, 1], [1, 0, 1]],
            [[0, 0, 0], [0, 0, 1], [0, 1, 1], [0, 1, 0]],
            [[0, 1, 0], [0, 1, 1], [1, 1, 1], [1, 1, 0]],
            [[0, 0, 0], [1, 0, 0], [1, 0, 1], [0, 0, 1]],
            [[0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]],
            [[0, 0, 0], [0, 1, 0], [1, 1, 0], [1, 0, 0]]
        ];
        Point middle = transform * new Point((low.X + high.X) / 2, (low.Y + high.Y) / 2, (low.Z + high.Z) / 2);
        List<Face> result = [];

        foreach (int[][] face in faces)
        {
            List<Point> corners = face.Select(corner => At(corner[0], corner[1], corner[2])).ToList();
            Vector normal = (corners[1] - corners[0]).Cross(corners[2] - corners[0]).Unit;

            if (normal.Dot(corners[0] - middle) < 0)
            {
                normal = -normal;
                corners.Reverse();
            }

            result.Add(new Face(normal, normal.Dot(corners[0] - Point.Zero), corners));
        }

        return new Polytope(result);
    }

    /// <summary>
    /// This method returns every corner of the polytope once.
    /// </summary>
    public List<Point> Vertices()
    {
        List<Point> vertices = [];
        double near = Size * 1e-7;

        foreach (Point corner in Faces.SelectMany(face => face.Corners))
        {
            if (!vertices.Any(vertex => (vertex - corner).Magnitude < near))
                vertices.Add(corner);
        }

        return vertices;
    }

    /// <summary>
    /// This method returns every edge of the polytope once, each as its two ends.
    /// </summary>
    public List<(Point From, Point To)> Edges()
    {
        List<(Point From, Point To)> edges = [];
        double near = Size * 1e-7;

        foreach (Face face in Faces)
        {
            for (int index = 0; index < face.Corners.Count; index++)
            {
                Point from = face.Corners[index];
                Point to = face.Corners[(index + 1) % face.Corners.Count];

                if (!edges.Any(edge => ((edge.From - to).Magnitude < near && (edge.To - from).Magnitude < near) ||
                                       ((edge.From - from).Magnitude < near && (edge.To - to).Magnitude < near)))
                {
                    edges.Add((from, to));
                }
            }
        }

        return edges;
    }

    /// <summary>
    /// This property reports how big the polytope is: the longest side of the box round it.
    /// </summary>
    public double Size
    {
        get
        {
            BoundingBox box = Bounds();
            Vector side = box.Maximum - box.Minimum;

            return Math.Max(side.X, Math.Max(side.Y, side.Z));
        }
    }

    /// <summary>
    /// This method returns the box round the polytope.
    /// </summary>
    public BoundingBox Bounds()
    {
        BoundingBox box = new ();

        foreach (Point corner in Faces.SelectMany(face => face.Corners))
            box.Add(corner);

        return box;
    }

    /// <summary>
    /// This method returns the polytope's middle: the centroid of its volume, worked out from the pyramids each
    /// face makes with a point inside.
    /// </summary>
    public Point Centroid()
    {
        List<Point> vertices = Vertices();
        Point inside = new (
            vertices.Average(vertex => vertex.X), vertices.Average(vertex => vertex.Y), vertices.Average(vertex => vertex.Z));
        double volume = 0;
        Vector sum = new (0, 0, 0);

        foreach (Face face in Faces)
        {
            for (int index = 1; index + 1 < face.Corners.Count; index++)
            {
                Point a = face.Corners[0], b = face.Corners[index], c = face.Corners[index + 1];
                double piece = Math.Abs((a - inside).Dot((b - inside).Cross(c - inside))) / 6;

                volume += piece;
                sum += ((a - Point.Zero) + (b - Point.Zero) + (c - Point.Zero) + (inside - Point.Zero)) * (piece / 4);
            }
        }

        return volume <= 0 ? inside : Point.Zero + sum / volume;
    }

    /// <summary>
    /// This method returns the polytope's volume.
    /// </summary>
    public double Volume()
    {
        Point inside = Centroid();
        double volume = 0;

        foreach (Face face in Faces)
        {
            for (int index = 1; index + 1 < face.Corners.Count; index++)
            {
                Point a = face.Corners[0], b = face.Corners[index], c = face.Corners[index + 1];

                volume += Math.Abs((a - inside).Dot((b - inside).Cross(c - inside))) / 6;
            }
        }

        return volume;
    }

    /// <summary>
    /// This method returns what is left of the polytope on the inside of a plane -- the side where n.x is at most
    /// d -- or <c>null</c> if nothing is.  A plane that cuts nothing hands back the polytope itself.
    /// </summary>
    /// <param name="normal">The plane's normal, of unit length, pointing away from what is kept.</param>
    /// <param name="offset">The plane's offset along its normal.</param>
    public Polytope Clip(Vector normal, double offset)
    {
        double near = Math.Max(Size, 1e-12) * Tolerance;
        bool cuts = false;
        bool keeps = false;

        foreach (Point corner in Faces.SelectMany(face => face.Corners))
        {
            double side = normal.Dot(corner - Point.Zero) - offset;

            if (side > near)
                cuts = true;
            else if (side < -near)
                keeps = true;
        }

        if (!cuts)
            return this;

        if (!keeps)
            return null;

        List<Face> faces = [];
        List<Point> cut = [];

        foreach (Face face in Faces)
        {
            List<Point> kept = [];
            int count = face.Corners.Count;

            for (int index = 0; index < count; index++)
            {
                Point here = face.Corners[index];
                Point next = face.Corners[(index + 1) % count];
                double hereSide = normal.Dot(here - Point.Zero) - offset;
                double nextSide = normal.Dot(next - Point.Zero) - offset;

                if (hereSide <= near)
                {
                    kept.Add(here);

                    if (hereSide >= -near)
                        AddOnce(cut, here, near);
                }

                if ((hereSide < -near && nextSide > near) || (hereSide > near && nextSide < -near))
                {
                    Point crossing = here + (next - here) * (hereSide / (hereSide - nextSide));

                    kept.Add(crossing);
                    AddOnce(cut, crossing, near);
                }
            }

            if (kept.Count >= 3)
                faces.Add(new Face(face.Normal, face.Offset, kept));
        }

        if (cut.Count >= 3)
            faces.Add(new Face(normal, offset, AroundTheMiddle(cut, normal)));

        return faces.Count >= 4 ? new Polytope(faces) : null;
    }

    /// <summary>
    /// This method returns the polytope drawn in by a distance on every face -- the inside of every one of its
    /// planes moved that far in -- or <c>null</c> if nothing is left.  Grown back out by a ball of that radius it
    /// is the polytope with its edges and corners rounded.
    /// </summary>
    public Polytope Shrunk(double distance)
    {
        BoundingBox box = Bounds();
        Polytope shrunk = Box(box.Minimum, box.Maximum);

        foreach (Face face in Faces)
        {
            shrunk = shrunk.Clip(face.Normal, face.Offset - distance);

            if (shrunk is null)
                return null;
        }

        return shrunk;
    }

    /// <summary>
    /// This method returns the polytope carried by a transform that keeps flat things flat, as the pieces of a
    /// fracture are carried to where they fall.
    /// </summary>
    public Polytope Carried(Matrix transform)
    {
        Matrix normals = transform.Invert().Transpose();

        return new Polytope(Faces
            .Select(face =>
            {
                List<Point> corners = face.Corners.Select(corner => transform * corner).ToList();
                Vector normal = (normals * face.Normal).Unit;

                return new Face(normal, normal.Dot(corners[0] - Point.Zero), corners);
            })
            .ToList());
    }

    private static void AddOnce(List<Point> points, Point point, double near)
    {
        if (!points.Any(already => (already - point).Magnitude <= near * 10))
            points.Add(point);
    }

    // The points of a convex polygon lying in a plane, put in order counterclockwise seen from outside it -- from
    // the side the normal points to.
    private static List<Point> AroundTheMiddle(List<Point> points, Vector normal)
    {
        Point middle = new (points.Average(point => point.X), points.Average(point => point.Y), points.Average(point => point.Z));
        Vector across = (points[0] - middle).Unit;
        Vector up = normal.Cross(across);

        return points
            .OrderBy(point => Math.Atan2((point - middle).Dot(up), (point - middle).Dot(across)))
            .ToList();
    }
}
