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
    /// This tests where a solid met from inside works its light out from.  Glass sets off along its own
    /// normal, outward, whichever side it was met from: its inner face takes the light falling on its outer
    /// one.  An opaque solid has no light falling through it, so seen from inside -- a hollow ball, the inside
    /// of a box -- it is lit by whatever is inside with it, and sets off toward the eye; from outside, a lamp
    /// at the middle of a ball was hidden from the whole of its wall by the wall itself.
    /// </summary>
    [TestMethod]
    public void TestASolidMetFromWithinIsLitFromTheSideItIsLitOn()
    {
        Sphere glass = new () { Material = new Material { Transparency = 1 } };
        Sphere opaque = new ();

        glass.PrepareForRendering();
        opaque.PrepareForRendering();

        // From inside, looking out: the normal points away from the eye.
        Ray ray = new (new Point(0, 0, 0), new Vector(0, 0, 1));
        (Vector throughGlass, Vector eye) = HitFrom(glass, ray);
        (Vector inTheBall, _) = HitFrom(opaque, ray);

        Assert.IsTrue(throughGlass.Dot(eye) < 0,
            "glass met from within must still nudge along its own normal, which points outward");
        Assert.IsTrue(inTheBall.Dot(eye) > 0,
            "an opaque solid met from within must be lit from the side the eye is on");
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
            "Sweep", "TextSolid",
            // A ribbon is a strip of sheets and never answers a ray itself -- what does is one of the
            // patches inside it, and those say they are sheets.  It sits here with `Sweep` and
            // `TextSolid` for the same reason: what a holder says about itself is never asked.
            "Ribbon",
            // A field is a holder too: it fills an outline with copies of something else, and what
            // answers a ray is whichever of those it hit.
            "Field",
            // And a grown tree: what answers a ray is one of the tube segments or leaves it grew.
            "GrownTree",
            // A fracture holds its pieces, and each piece is a convex solid, sharp or worn round.
            "Fracture", "Polyhedron", "RoundedPolyhedron",
            // A shape grammar holds the walls, roofs and placed things its rules made.
            "ShapeGrammar",
            // A swarm holds copies of the one thing it throws, and what answers a ray is a copy.
            "Swarm",
            // A strand is hit as a ribbon but shaded as a rod: its normal always faces whoever is
            // looking, and a ray leaving it sets off from the round surface the shading pretends is
            // there (see HairIntersection.LeavingPoints), so it is never met from behind.
            "Strand", "Hair", "HairPart"
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
