using System.Reflection;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These tests cover which surfaces are sheets, and what being one changes.
/// <para>
/// A sheet encloses nothing, so its normal points whichever way its author happened to write it
/// rather than naming an outside.  Half of all sheets therefore point away from whoever is looking,
/// and nudging the shading point along such a normal puts it *behind* the surface -- from where its
/// own shadow ray finds the surface in the way, and the whole thing renders black.  A parallelogram,
/// a disc, a triangle and a generic shape were each proved to do exactly that.
/// </para>
/// </summary>
[TestClass]
public class TestSheetSurfaces
{
    /// <summary>
    /// Works out where a ray meeting a surface puts its over point, as the renderer would.
    /// </summary>
    private static (Vector Nudge, Vector Eye) HitFrom(Surface surface, Ray ray)
    {
        List<Intersection> found = [];

        surface.Intersect(ray, found);

        Intersection hit = found
            .Where(crossing => crossing.Distance > 0)
            .OrderBy(crossing => crossing.Distance)
            .First();

        hit.PrepareUsing(ray, found);

        return (hit.LitPoint - hit.Point, hit.Eye);
    }

    /// <summary>
    /// The sheets that can be stood up in one line, each written so that its normal points *away*
    /// from a ray coming along positive Z -- which is the case that used to render black.
    /// </summary>
    private static IEnumerable<(string Name, Surface Surface)> SheetsFacingAway()
    {
        // These face +Z, and are met by a ray travelling that way.
        yield return ("parallelogram", new Parallelogram
        {
            Point = new Point(-1, -1, 0), Side1 = new Vector(2, 0, 0), Side2 = new Vector(0, 2, 0)
        });
        yield return ("disc", new Disc
        {
            Center = new Point(0, 0, 0), Normal = new Vector(0, 0, 1), Radius = 1
        });
        yield return ("triangle", new Triangle
        {
            Point1 = new Point(-1, -1, 0), Point2 = new Point(1, -1, 0), Point3 = new Point(0, 1, 0)
        });
        yield return ("smooth triangle", new SmoothTriangle
        {
            Point1 = new Point(-1, -1, 0), Point2 = new Point(1, -1, 0), Point3 = new Point(0, 1, 0),
            Normal1 = new Vector(0, 0, 1), Normal2 = new Vector(0, 0, 1), Normal3 = new Vector(0, 0, 1)
        });
    }

    /// <summary>
    /// This tests that a sheet whose normal points away from the eye still sets its lights off from
    /// the eye's side of itself.  That is the whole of the fix: a point on the far side is a point
    /// whose shadow ray leaves from under the very surface it is standing on.
    /// </summary>
    [TestMethod]
    public void TestASheetSetsOffTowardsTheEye()
    {
        foreach ((string name, Surface surface) in SheetsFacingAway())
        {
            surface.PrepareForRendering();

            Ray ray = new (new Point(0, 0, -3), new Vector(0, 0, 1));
            (Vector nudge, Vector eye) = HitFrom(surface, ray);

            Assert.IsTrue(nudge.Dot(eye) > 0,
                $"a {name} facing away nudges to {nudge}, which is away from the eye at {eye}");
        }
    }

    /// <summary>
    /// This tests that a solid is untouched by any of that.  A solid's normal genuinely names its
    /// outside, so it sets off along that normal whichever side it was met from.
    /// </summary>
    [TestMethod]
    public void TestASolidStillNudgesAlongItsOwnNormal()
    {
        Sphere sphere = new ();

        sphere.PrepareForRendering();

        // From inside, looking out: the normal points away from the eye, and must still be followed.
        Ray ray = new (new Point(0, 0, 0), new Vector(0, 0, 1));
        (Vector nudge, Vector eye) = HitFrom(sphere, ray);

        Assert.IsTrue(nudge.Dot(eye) < 0,
            "a solid met from within must still nudge along its own normal, which points outward");
    }

    /// <summary>
    /// This tests that every surface has had the question put to it, so that a new one cannot be
    /// added without someone deciding which it is.
    /// <para>
    /// **The two lists are written out rather than worked out**, because the fault this guards against
    /// is precisely a surface nobody thought about.  A test that derived the answer from the code
    /// would agree with whatever the code said.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestEverySurfaceHasDecidedWhetherItIsASheet()
    {
        HashSet<string> sheets =
        [
            "Disc", "Parallelogram", "GenericShape", "Triangle", "SmoothTriangle",
            "BicubicPatch", "BilinearPatch", "Parametric",
            // Cut to a rectangle, a saddle is a finite four-cornered surface with nothing inside it,
            // exactly as a patch is.  Endless it would divide space and be a half-space, which is
            // what `Quadric` gives anyone who wants that.
            "Saddle"
        ];
        HashSet<string> solids =
        [
            "Plane", "Swells", "Paraboloid", "Hyperboloid", "Quadric",
            // Both are marched by distance, and both are solids: the field is negative within them,
            // which is what "inside" means for a distance.
            "SignedDistanceSurface", "JuliaFractal",
            "Sphere", "Cube", "Cylinder", "Conic", "Torus", "Egg", "Superellipsoid", "Blob",
            "Isosurface", "Extrusion", "Lathe", "Tube", "TubeSegment", "TubeQuadSegment",
            "TubeCubicSegment", "CsgSurface", "Group", "Instance", "HeightField", "LSystem",
            "Sweep", "TextSolid"
        ];

        List<string> undecided = [];

        foreach (Type type in typeof(Surface).Assembly.GetTypes()
                     .Where(candidate => candidate.IsSubclassOf(typeof(Surface)))
                     .Where(candidate => !candidate.IsAbstract)
                     .OrderBy(candidate => candidate.Name))
        {
            bool isASheet = sheets.Contains(type.Name);

            if (!isASheet && !solids.Contains(type.Name))
            {
                undecided.Add(type.Name);

                continue;
            }

            Surface surface = (Surface) (type == typeof(Instance)
                ? new Instance { Prototype = new Sphere() }
                : Activator.CreateInstance(type, nonPublic: true));

            Assert.AreEqual(isASheet, surface.IsASheet,
                $"{type.Name} says it {(surface.IsASheet ? "is" : "is not")} a sheet, and it " +
                $"{(isASheet ? "is" : "is not")} one");
        }

        Assert.AreEqual(0, undecided.Count,
            $"no one has said whether these are sheets: {string.Join(", ", undecided)}");
    }
}
