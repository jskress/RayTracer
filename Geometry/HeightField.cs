using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;
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
    /// This property holds the information for the image to use.
    /// </summary>
    public ImageReference ImageReference { get; set; }

    /// <summary>
    /// This property notes the level, if any, at which the height field is clipped.
    /// </summary>
    public double Clip { get; set; } = -1;

    /// <summary>
    /// This property notes whether the height field has walls and a bottom.
    /// </summary>
    public bool Closed { get; set; } = true;

    private Canvas _canvas;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        // First, load the image and ensure it's a gray scale.
        _canvas = ImageReference.Canvas.ToGrayScale();

        if (_canvas.Width < 2 || _canvas.Height < 2)
            throw new Exception("Height field needs to be at least 2 pixels in both dimensions.");

        // Next, we need to create all our triangles.
        CreateTriangles();

        // Finally, let the group do what it needs to.
        base.PrepareSurfaceForRendering();
    }

    /// <summary>
    /// This method creates all our needed triangles to represent the height field.
    /// </summary>
    private void CreateTriangles()
    {
        double scaleX = 1.0 / (_canvas.Width - 1);
        double scaleY = 1.0 / (_canvas.Height - 1);
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
    /// This method creates the triangles to represent the height field "terrain".
    /// </summary>
    /// <param name="sx">The scale in the X direction to map to a unit cube.</param>
    /// <param name="sy">The scale in the X direction to map to a unit cube.</param>
    /// <returns>The number of triangles created.</returns>
    private int CreateSurfaceTriangles(double sx, double sy)
    {
        int count = 0;

        for (int y = 0; y < _canvas.Height - 1; y++)
        {
            Group group = new Group();

            for (int x = 0; x < _canvas.Width - 1; x++)
                count += AddSurfaceTriangles(group, x, y, sx, sy);

            Add(group);
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

        double y1 = ApplyClip(_canvas.GetPixel(x, y).Red);
        double y2 = ApplyClip(_canvas.GetPixel(x + 1, y).Red);
        double y3 = ApplyClip(_canvas.GetPixel(x + 1, y + 1).Red);
        double y4 = ApplyClip(_canvas.GetPixel(x, y + 1).Red);

        Point point1 = new Point(x1, y1, z1);
        Point point2 = new Point(x2, y2, z1);
        Point point3 = new Point(x2, y3, z2);
        Point point4 = new Point(x1, y4, z2);

        if (Closed || y1 > Clip || y2 > Clip || y3 > Clip)
        {
            group.Add(new Triangle
            {
                Point1 = point1,
                Point2 = point2,
                Point3 = point3
            });
            count++;
        }

        if (Closed || y1 > Clip || y3 > Clip || y4 > Clip)
        {
            group.Add(new Triangle
            {
                Point1 = point1,
                Point2 = point3,
                Point3 = point4
            });
            count++;
        }

        return count;
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
        int maxX = _canvas.Width - 1;
        int maxY = _canvas.Height - 1;
        int count = 0;
        Group front = new Group();
        Group back = new Group();
        Group left = new Group();
        Group right = new Group();

        for (int x = 0; x < _canvas.Width - 1; x++)
        {
            AddWallTriangles(front, x + 1, 0, x, 0, sx, sy);
            AddWallTriangles(back, x, maxY, x + 1, maxY, sx, sy);

            count += 4;
        }

        for (int y = 0; y < _canvas.Height - 1; y++)
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
        double y1 = ApplyClip(_canvas.GetPixel(px1, py1).Red);
        double y2 = ApplyClip(_canvas.GetPixel(px2, py2).Red);
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
