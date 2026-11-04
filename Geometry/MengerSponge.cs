using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents the Menger sponge: a cube cut into twenty-seven, the middle one and the middle of each face
/// taken out, and the same done to each of the twenty that are left, and to theirs, as many times as said.
/// <para>
/// **Its distance is worked out from its shape**, Inigo Quilez's way, not estimated from an iteration: the distance
/// to the cube, then for each level the distance into the cross of square holes that level cuts -- the space
/// repeated every two units at that level's size, folded into one cell, and the cross's three bars measured there
/// -- the solid being what is in the cube and outside every cross, so the distance is the greatest of them.  It is
/// the true distance outside the cube, and within it never more than the true one (at the middle a third, where
/// the nearest solid is 0.47 off), which is all the march needs; it strides through the holes.
/// </para>
/// <para>
/// The sponge is the cube from -1 to 1 on every axis, and needs no <c>bounded by</c>.  Each level divides the
/// smallest holes by three: four levels make holes an eighty-first of the cube across, five a two hundred and
/// forty-third.
/// </para>
/// </summary>
public class MengerSponge : DistanceEstimatedSurface
{
    /// <summary>
    /// This property holds how many times the cube is cut into twenty-seven.
    /// </summary>
    public int Iterations { get; set; } = 4;

    private BoundingBox _domain;

    /// <summary>
    /// This method works out the region the march is confined to.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _domain = BoundingBox ?? GetDefaultBoundingBox();
    }

    /// <summary>
    /// This method returns the cube the sponge is cut from.
    /// </summary>
    /// <returns>The box the fractal sits in.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return new BoundingBox()
            .Add(new Point(-1, -1, -1))
            .Add(new Point(1, 1, 1));
    }

    /// <summary>
    /// This property holds the region the march is confined to.
    /// </summary>
    protected override BoundingBox MarchingDomain => _domain;

    /// <summary>
    /// This method works out how far the nearest surface of the sponge is from a point.
    /// </summary>
    protected override double DistanceAt(double x, double y, double z)
    {
        double distance = ToTheCube(x, y, z);
        double size = 1;

        for (int level = 0; level < Iterations; level++)
        {
            // The point in its cell of the repeated cross at this level, the cell from -1 to 1.
            double ax = Repeated(x * size), ay = Repeated(y * size), az = Repeated(z * size);

            size *= 3;

            double rx = Math.Abs(1 - 3 * Math.Abs(ax));
            double ry = Math.Abs(1 - 3 * Math.Abs(ay));
            double rz = Math.Abs(1 - 3 * Math.Abs(az));

            // Into the cross: the nearest of its three bars, each infinite along one axis.
            double cross = (Math.Min(Math.Max(rx, ry), Math.Min(Math.Max(ry, rz), Math.Max(rz, rx))) - 1) / size;

            distance = Math.Max(distance, cross);
        }

        return distance;
    }

    /// <summary>
    /// This method returns a coordinate carried into the cell from -1 to 1 of space repeated every two.
    /// </summary>
    private static double Repeated(double value)
    {
        return value - 2 * Math.Floor(value / 2) - 1;
    }

    /// <summary>
    /// This method returns how far a point is from the cube from -1 to 1, signed.
    /// </summary>
    private static double ToTheCube(double x, double y, double z)
    {
        double qx = Math.Abs(x) - 1, qy = Math.Abs(y) - 1, qz = Math.Abs(z) - 1;
        double outside = Math.Sqrt(
            Math.Pow(Math.Max(qx, 0), 2) + Math.Pow(Math.Max(qy, 0), 2) + Math.Pow(Math.Max(qz, 0), 2));

        return outside + Math.Min(Math.Max(qx, Math.Max(qy, qz)), 0);
    }
}
