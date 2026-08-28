using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Fields;
using RayTracer.ImageIO;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a height field.  This uses an image to produce a mesh of triangles
/// for the squares between pixels.  It is typically used to approximate terrain.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class HeightField : Group
{
    /// <summary>
    /// This property holds the information for the image to use.  A height field takes its heights
    /// either from this or from <see cref="Function"/>, and must be given exactly one of them.
    /// </summary>
    public ImageReference ImageReference { get; set; }

    /// <summary>
    /// This property holds the function whose value gives the height, when the terrain is described
    /// rather than drawn.  It is evaluated over the same unit square the image covers, so <c>x</c>
    /// and <c>z</c> each run from 0 to 1 and whatever the function answers is the height outright --
    /// there is no quarter-scaling here, unlike the image form, which is a difference worth knowing
    /// when a pigment is mapped by altitude across the two.
    /// </summary>
    public FieldExpression Function { get; set; }

    /// <summary>
    /// This property holds how many points across the function is sampled at, in each direction.  An
    /// image brings its own size with it and ignores this.
    /// </summary>
    public int Samples { get; set; } = 256;

    /// <summary>
    /// This property notes the level, if any, at which the height field is clipped.  It belongs to
    /// the image form: a picture's pixels cannot be reached from the scene, so a floor has to be
    /// applied here, where a function can simply say <c>max(..., 0)</c> for itself.
    /// </summary>
    public double Clip { get; set; } = -1;

    /// <summary>
    /// This property notes whether the height field has walls and a bottom.
    /// </summary>
    public bool Closed { get; set; } = true;

    /// <summary>
    /// This property notes whether the terrain is shaded as the smooth thing it stands for, rather
    /// than as the flat triangles it is really made of.
    /// <para>
    /// It is off by default, and deliberately: turning it on changes the shading of every height
    /// field ever written, so it is asked for rather than assumed.  Terrain with crags in it hides
    /// its faceting well enough that the flat form is no hardship; a smooth surface lit at a graze
    /// -- dunes are the worst case -- shows every triangle it owns.
    /// </para>
    /// </summary>
    public bool Smooth { get; set; }

    // The heights of every grid point, worked out once up front.  Each one is read by up to four of
    // the triangles around it, and having both sources land here is what lets everything below this
    // point be written once rather than twice.
    private double[,] _heights;
    private Vector[,] _normals;
    private int _columns;
    private int _rows;

    // The level an open field's flat triangles are dropped at.  That is the clip for a picture,
    // whose pixels the scene cannot reach; for a function there is no such level, since the author
    // writes whatever floor they want into the expression and every height they ask for is meant.
    private double _floor;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        // First, work out the height at every point of the grid, from whichever source we were given.
        if (Function is null)
            ReadHeightsFromTheImage();
        else
            ReadHeightsFromTheFunction();

        if (_columns < 2 || _rows < 2)
            throw new Exception("Height field needs to be at least 2 points in both dimensions.");

        // Next, we need to create all our triangles.
        CreateTriangles();

        // Finally, let the group do what it needs to.
        base.PrepareSurfaceForRendering();
    }

    /// <summary>
    /// This method reads the heights out of our image, as a gray scale, and applies any clip level
    /// we carry.  The quarter-scaling is applied here and only here: a height field made from a
    /// picture has always stood a quarter as tall as the picture is bright, and every scene built on
    /// one is composed around that.
    /// </summary>
    private void ReadHeightsFromTheImage()
    {
        Canvas canvas = ImageReference.Canvas.ToGrayScale();

        _columns = canvas.Width;
        _rows = canvas.Height;
        _floor = Clip;

        if (_columns < 2 || _rows < 2)
            return;

        _heights = new double[_columns, _rows];

        for (int y = 0; y < _rows; y++)
        {
            for (int x = 0; x < _columns; x++)
                _heights[x, y] = ApplyClip(canvas.GetPixel(x, y).Red);
        }
    }

    /// <summary>
    /// This method works out the heights by asking our function for one at every point of the grid.
    /// <para>
    /// The function sees the same unit square the image covers, so it is written in the terrain's own
    /// coordinates and scaled into the world afterwards like anything else.  It is handed a Y of
    /// nought, since a height is a thing of two dimensions; a function that uses Y will simply see it
    /// as a constant.
    /// </para>
    /// </summary>
    private void ReadHeightsFromTheFunction()
    {
        _columns = _rows = Samples;
        _floor = double.NegativeInfinity;

        if (_columns < 2)
            return;

        FieldFunction function = FieldFunction.Compile(Function);
        double step = 1.0 / (Samples - 1);

        _heights = new double[_columns, _rows];

        for (int y = 0; y < _rows; y++)
        {
            for (int x = 0; x < _columns; x++)
                _heights[x, y] = function.Evaluate(x * step, 0, y * step);
        }
    }

    /// <summary>
    /// This method creates all our needed triangles to represent the height field.
    /// </summary>
    private void CreateTriangles()
    {
        double scaleX = 1.0 / (_columns - 1);
        double scaleY = 1.0 / (_rows - 1);

        if (Smooth)
            WorkOutNormals(scaleX, scaleY);

        int count = CreateSurfaceTriangles(scaleX, scaleY);

        if (Closed)
        {
            count += CreateWallTriangles(scaleX, scaleY);

            Point point0 = Point.Zero;
            Point point2 = new Point(1, 0, 0);
            Point point1 = new Point(0, 0, 1);

            Add(new Parallelogram
            {
                Point = point0,
                Side1 = point1 - point0,
                Side2 = point2 - point0
            });
        }
        
        Terminal.Out($"Height field created {count} triangles.", OutputLevel.Chatty);
    }

    /// <summary>
    /// This method works out the normal at every point of the grid, from the slope of the ground
    /// either side of it.
    /// <para>
    /// For a surface <c>y = h(x, z)</c> the normal is <c>(-dh/dx, 1, -dh/dz)</c>, and a central
    /// difference across the neighbouring points is what makes the triangles meeting at a point
    /// agree about which way it faces -- which is the whole of the difference between a smooth
    /// surface and a faceted one.  The points along the edges have a neighbour on one side only, so
    /// they take a one-sided difference.
    /// </para>
    /// <para>
    /// This is done from the sampled grid rather than from the function that may have made it, and
    /// on purpose: the grid is what the triangles are actually built from, so normals taken from it
    /// describe the surface being drawn.  It also means the image form and the function form get
    /// this by the same route.
    /// </para>
    /// </summary>
    /// <param name="sx">The distance between grid points in the X direction.</param>
    /// <param name="sy">The distance between grid points in the Z direction.</param>
    private void WorkOutNormals(double sx, double sy)
    {
        _normals = new Vector[_columns, _rows];

        for (int y = 0; y < _rows; y++)
        {
            for (int x = 0; x < _columns; x++)
            {
                int left = Math.Max(x - 1, 0);
                int right = Math.Min(x + 1, _columns - 1);
                int back = Math.Max(y - 1, 0);
                int front = Math.Min(y + 1, _rows - 1);
                double slopeX = (_heights[right, y] - _heights[left, y]) / ((right - left) * sx);
                double slopeZ = (_heights[x, front] - _heights[x, back]) / ((front - back) * sy);

                _normals[x, y] = new Vector(-slopeX, 1, -slopeZ).Unit;
            }
        }
    }

    /// <summary>
    /// This method creates the triangles to represent the height field "terrain".
    /// </summary>
    /// <param name="sx">The scale in the X direction to map to a unit cube.</param>
    /// <param name="sy">The scale in the X direction to map to a unit cube.</param>
    /// <returns>The number of triangles created.</returns>
    private int CreateSurfaceTriangles(double sx, double sy)
    {
        int count = 0;

        // The triangles go straight into this group rather than into one group per row of the image.
        // The banding was an attempt at the very problem a group now solves for itself, and a poor
        // one: a row is a long thin slab, so a ray crossing the terrain lengthwise passes through most
        // of them, and the terrain was divided along one axis and not the other.  Handed the lot, a
        // group divides it by where the triangles actually sit, in whichever direction they are most
        // spread out.
        //
        // Measured on the terrain in docs/examples at 400x300: 1.34s unbanded against 1.42s banded,
        // three runs each and no overlap between them.  Worth knowing that the same comparison at
        // 200x150 came out the other way round -- at a render of about a second, building the tree and
        // starting the process cost more than the tracing they were being compared through.
        for (int y = 0; y < _rows - 1; y++)
        {
            for (int x = 0; x < _columns - 1; x++)
                count += AddSurfaceTriangles(this, x, y, sx, sy);
        }

        return count;
    }

    /// <summary>
    /// This method is used to add the pair of triangles for a given pixel.  If the height
    /// field is open and a triangle is flat to the clip plane, it is omitted.
    /// </summary>
    /// <param name="group">The group to add the triangle to.</param>
    /// <param name="x">The x coordinate of the pixel.</param>
    /// <param name="y">The y coordinate of the pixel.</param>
    /// <param name="sx">The scale in the X direction to map to a unit cube.</param>
    /// <param name="sy">The scale in the X direction to map to a unit cube.</param>
    /// <returns>The number of triangles created.</returns>
    private int AddSurfaceTriangles(Group group, int x, int y, double sx, double sy)
    {
        double x1 = x * sx;
        double x2 = (x + 1) * sx;
        double z1 = y * sy;
        double z2 = (y + 1) * sy;
        int count = 0;

        double y1 = _heights[x, y];
        double y2 = _heights[x + 1, y];
        double y3 = _heights[x + 1, y + 1];
        double y4 = _heights[x, y + 1];

        Point point1 = new Point(x1, y1, z1);
        Point point2 = new Point(x2, y2, z1);
        Point point3 = new Point(x2, y3, z2);
        Point point4 = new Point(x1, y4, z2);

        if (Closed || y1 > _floor || y2 > _floor || y3 > _floor)
        {
            group.Add(TriangleFor(point1, point2, point3, x, y, x + 1, y, x + 1, y + 1));
            count++;
        }

        if (Closed || y1 > _floor || y3 > _floor || y4 > _floor)
        {
            group.Add(TriangleFor(point1, point3, point4, x, y, x + 1, y + 1, x, y + 1));
            count++;
        }

        return count;
    }

    /// <summary>
    /// This method makes one triangle of the terrain, carrying the normals of the grid points it
    /// stands on when the terrain is to be shaded smooth.  Only the terrain is smoothed; the walls
    /// below it are flat because they really are flat.
    /// </summary>
    private Triangle TriangleFor(
        Point point1, Point point2, Point point3,
        int x1, int y1, int x2, int y2, int x3, int y3)
    {
        if (!Smooth)
        {
            return new Triangle
            {
                Point1 = point1,
                Point2 = point2,
                Point3 = point3
            };
        }

        return new SmoothTriangle
        {
            Point1 = point1,
            Point2 = point2,
            Point3 = point3,
            Normal1 = _normals[x1, y1],
            Normal2 = _normals[x2, y2],
            Normal3 = _normals[x3, y3]
        };
    }

    /// <summary>
    /// This is a helper method to apply any clip value we carry
    /// </summary>
    /// <param name="z">The Z value to clip</param>
    /// <returns>The clipped value.</returns>
    private double ApplyClip(double z)
    {
        return (Clip > z ? Clip : z) * 0.25;
    }

    /// <summary>
    /// This method creates the triangles for the vertical walls the height field terrain.
    /// </summary>
    /// <param name="sx">The scale in the X direction to map to a unit cube.</param>
    /// <param name="sy">The scale in the X direction to map to a unit cube.</param>
    /// <returns>The number of triangles created.</returns>
    private int CreateWallTriangles(double sx, double sy)
    {
        int maxX = _columns - 1;
        int maxY = _rows - 1;
        int count = 0;
        Group front = new Group();
        Group back = new Group();
        Group left = new Group();
        Group right = new Group();

        for (int x = 0; x < _columns - 1; x++)
        {
            AddWallTriangles(front, x + 1, 0, x, 0, sx, sy);
            AddWallTriangles(back, x, maxY, x + 1, maxY, sx, sy);

            count += 4;
        }

        for (int y = 0; y < _rows - 1; y++)
        {
            AddWallTriangles(left, 0, y, 0, y + 1, sx, sy);
            AddWallTriangles(right, maxX, y + 1, maxX, y, sx, sy);

            count += 4;
        }

        Add(front);
        Add(back);
        Add(left);
        Add(right);

        return count;
    }

    /// <summary>
    /// This method is used to add a pair of triangles for each border pixel to make a vertical
    /// wall around the height field.
    /// </summary>
    /// <param name="group">The group to add the triangles to.</param>
    /// <param name="px1">The X coordinate of the first pixel.</param>
    /// <param name="py1">The Y coordinate of the first pixel.</param>
    /// <param name="px2">The X coordinate of the second pixel.</param>
    /// <param name="py2">The Y coordinate of the second pixel.</param>
    /// <param name="sx">The scale in the X direction to map to a unit cube.</param>
    /// <param name="sy">The scale in the X direction to map to a unit cube.</param>
    private void AddWallTriangles(Group group, int px1, int py1, int px2, int py2, double sx, double sy)
    {
        double x1 = px1 * sx;
        double x2 = px2 * sx;
        double y1 = _heights[px1, py1];
        double y2 = _heights[px2, py2];
        double z1 = py1 * sy;
        double z2 = py2 * sy;

        AddTriangles(group,
            new Point(x1, 0, z1), new Point(x1, y1, z1),
            new Point(x2, y2, z2), new Point(x2, 0, z2));
    }

    /// <summary>
    /// This is a helper method for creating a pair of triangles for a quartet of points.
    /// </summary>
    /// <param name="group">The group to add the triangles to.</param>
    /// <param name="point1">The first point.</param>
    /// <param name="point2">The second point.</param>
    /// <param name="point3">The third point.</param>
    /// <param name="point4">The fourth point.</param>
    private static void AddTriangles(Group group, Point point1, Point point2, Point point3, Point point4)
    {
        group.Add(new Triangle
        {
            Point1 = point1,
            Point2 = point2,
            Point3 = point3
        });
        group.Add(new Triangle
        {
            Point1 = point1,
            Point2 = point3,
            Point3 = point4
        });
    }
}
