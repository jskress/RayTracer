using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class provides the leaf an L-system stamps down for a <c>~</c> command when the scene
/// names no leaf surface of its own.  It is a small green bicubic patch shaped like a blade:
/// authored in the turtle's own local frame so it grows along +Z (the branch heading), spreads
/// its width along X, cups gently upward toward +Y, and tapers to a point at both its base and
/// its tip.  It carries a green material of its own so that a bare, unstyled tree already reads
/// as green leaves on branches, rather than inheriting the branch colour.
/// </summary>
public static class DefaultLeaf
{
    // The blade is laid out as a 4x4 grid: four rows marching from base (z = 0) to tip (z = 1),
    // and four columns spanning its width.  The width swells just past the base and narrows
    // again to the tip, and the two inner columns lift slightly to give the blade a shallow cup.
    private static readonly double[] RowZ = [0.0, 0.35, 0.7, 1.0];
    private static readonly double[] RowHalfWidth = [0.02, 0.22, 0.18, 0.02];
    private static readonly double[] RowCup = [0.0, 1.0, 0.8, 0.0];
    private static readonly double[] ColumnFraction = [-1.0, -1.0 / 3, 1.0 / 3, 1.0];

    private const double CupHeight = 0.05;

    /// <summary>
    /// This method builds a fresh default leaf surface.  A new instance is returned every call
    /// so each <c>~</c> in a production gets its own copy to place independently.
    /// </summary>
    /// <returns>A new default leaf surface.</returns>
    public static Surface Create()
    {
        Point[,] controlPoints = new Point[4, 4];

        for (int u = 0; u < 4; u++)
        {
            double fraction = ColumnFraction[u];

            for (int v = 0; v < 4; v++)
            {
                double x = RowHalfWidth[v] * fraction;
                double y = CupHeight * RowCup[v] * (1 - fraction * fraction);

                controlPoints[u, v] = new Point(x, y, RowZ[v]);
            }
        }

        // No material here.  The blade's green is a fallback rather than something it owns, and it
        // is handed out separately (see CreateMaterial) so that a material the production names can
        // displace it -- letting the leaves on a young shoot differ from those on old wood.
        return new BicubicPatch { ControlPoints = controlPoints };
    }

    /// <summary>
    /// This method builds the green a default leaf falls back on when nothing else has coloured it.
    /// <para>
    /// It exists so that a bare, unstyled tree still reads as green leaves on branches rather than
    /// taking the colour of its own bark, while leaving that green last in line: a material the
    /// production named, whether by character or by branching depth, is used in preference to it.
    /// A fresh instance is returned per leaf rather than one being shared, because seeding a
    /// pigment writes to it, and leaves sharing one would quietly share that too.
    /// </para>
    /// </summary>
    /// <returns>A new default leaf material.</returns>
    public static Material CreateMaterial()
    {
        return new Material { Pigment = new SolidPigment(Colors.ForestGreen) };
    }
}
