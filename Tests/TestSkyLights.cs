using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover light arriving from every direction at once.
/// <para>
/// A sky light differs from every other light here in kind rather than degree: the others are
/// somewhere, and this one is everywhere.  That makes two things worth checking above all.  What a
/// surface facing an open sky comes back as, since that is the number an author will tune against and
/// it has an exact answer.  And that a point which can only see a sliver of sky gets a sliver of light,
/// since shadowing itself is the whole of what separates this from the ambient fudge it replaces.
/// </para>
/// </summary>
[TestClass]
public class TestSkyLights
{
    /// <summary>
    /// Builds a scene lit by nothing but a sky of the given color, with a floor of the given ambient.
    /// </summary>
    private static Scene UnderASkyOf(Color sky, double? ambient = null, int samples = 400)
    {
        Plane floor = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White),
                Ambient = ambient,
                Diffuse = 0.9,
                Specular = 0
            }
        };
        Scene scene = new () { Background = new SolidPigment(sky) };

        floor.PrepareForRendering();
        scene.Surfaces.Add(floor);
        scene.Lights.Add(new SkyLight { Pigment = new SolidPigment(sky), Samples = samples });

        return scene;
    }

    /// <summary>
    /// Shades one point of a scene's floor, looking straight down at it.
    /// </summary>
    private static Color OnTheFloorOf(Scene scene, Point where, double from = 3)
    {
        Ray ray = new (where + new Vector(0, from, 0), Directions.Down);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        Assert.IsNotNull(hit, "the ray missed the floor");

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, 1);
    }

    [TestMethod]
    public void TestAnOpenSkyLightsASurfaceToItsOwnDiffuse()
    {
        // The number to hold on to, and the one an author tunes against: a surface that can see the
        // whole sky comes back as bright as the sky, times how much of what strikes it it takes.  It is
        // an exact answer rather than a matter of taste, and it is why a sky's samples must count double
        // -- a surface weighs each direction by how squarely it meets it, and over half the sky that
        // weighing averages a half.
        Color seen = OnTheFloorOf(UnderASkyOf(Colors.White, 0), Point.Zero);

        Assert.AreEqual(0.9, seen.Red, 0.01, $"a white sky over a white floor gave {seen.Red}");

        // Half as bright a sky lights it half as much, there being nothing else in the arithmetic.
        Color halfLit = OnTheFloorOf(UnderASkyOf(new Color(0.5, 0.5, 0.5), 0), Point.Zero);

        Assert.AreEqual(0.45, halfLit.Red, 0.01, $"half a sky gave {halfLit.Red}");
    }

    [TestMethod]
    public void TestASliverOfSkyGivesASliverOfLight()
    {
        // What separates a sky light from the ambient fudge it replaces.  Ambient is added everywhere
        // alike, shadow or no shadow; this arrives from real directions, so putting something in the way
        // of most of them takes most of the light away.  Nothing else in this renderer does that,
        // because nothing else comes from everywhere.
        Scene scene = UnderASkyOf(Colors.White, 0);
        Color inTheOpen = OnTheFloorOf(scene, new Point(0, 0, 0));

        // A wide slab a little above the floor, hiding most of the sky from what is under it.
        Cube lid = new ()
        {
            Material = new Material { Pigment = new SolidPigment(Colors.White) },
            Transform = Transforms.Translate(0, 1.2, 0) * Transforms.Scale(4, 0.1, 4)
        };

        lid.PrepareForRendering();
        scene.Surfaces.Add(lid);

        // Looked at from beneath the lid, or the ray would strike the lid's own sunlit top instead of
        // the floor it shades -- which is how this test read the wrong surface the first time.
        Color underTheLid = OnTheFloorOf(scene, new Point(0, 0, 0), 0.5);

        Assert.IsTrue(underTheLid.Red < inTheOpen.Red * 0.35,
            $"under cover should be far darker: {underTheLid.Red} against {inTheOpen.Red}");
        Assert.IsTrue(underTheLid.Red > 0,
            "but not black, some sky still being visible past the edges");
    }

    [TestMethod]
    public void TestTheSkyIsTheBackgroundUnlessToldOtherwise()
    {
        // The ordinary case, and the one worth having: what lights the scene is what the scene shows.
        SkyLight borrowed = new ();
        SkyLight ofItsOwn = new () { Pigment = new SolidPigment(Colors.Red) };

        Assert.IsNull(borrowed.Pigment, "it should arrive with nothing, for the scene to fill in");
        Assert.IsNotNull(ofItsOwn.Pigment);

        // The light's own color multiplies the sky rather than replacing it, so a sky may be dimmed
        // whole without giving up what it is a picture of.
        SkyLight dimmed = new ()
        {
            Pigment = new SolidPigment(new Color(0.8, 0.6, 0.4)),
            Color = new Color(0.5, 0.5, 0.5)
        };
        Color carried = dimmed.ColorFor(dimmed.SampleToward(Point.Zero, 0, Directions.Up));

        Assert.AreEqual(0.4, carried.Red, 1e-12);
        Assert.AreEqual(0.3, carried.Green, 1e-12);
        Assert.AreEqual(0.2, carried.Blue, 1e-12);
    }

    [TestMethod]
    public void TestTheSamplesFaceTheSurfaceOrSpreadOverEverything()
    {
        // Where there is a surface, no sample may fall behind it: the light there would be thrown away
        // after being paid for.  Where there is none -- a place in a medium, which faces no way -- they
        // spread over the whole sphere instead, and so average to nothing in particular.
        SkyLight sky = new () { Samples = 200 };
        Vector normal = new Vector(0.3, 0.8, -0.5).Unit;
        Vector total = new (0, 0, 0);

        for (int index = 0; index < sky.Samples; index++)
        {
            LightSample facing = sky.SampleToward(Point.Zero, index, normal);

            Assert.IsTrue(facing.Direction.Dot(normal) >= -1e-9,
                $"sample {index} fell behind the surface");
            Assert.AreEqual(2, facing.Cone, 1e-12, "a half-sky sample is worth two");

            LightSample everywhere = sky.SampleToward(Point.Zero, index);

            Assert.AreEqual(1, everywhere.Cone, 1e-12, "a whole-sky sample is worth one");

            total += everywhere.Direction;
        }

        Assert.IsTrue(total.Magnitude / sky.Samples < 0.05,
            $"spread over everything, the directions should cancel: {total.Magnitude / sky.Samples}");
    }

    [TestMethod]
    public void TestASkyLightsAMediumWithNoLampAtAll()
    {
        // The reason this was worth doing before anything else: a cloud is lit mostly by sky.  A medium
        // gathers from every light in the scene, so it needs nothing new to be lit by one -- and a place
        // inside it faces no way, so it takes its sky over the whole sphere.
        Sphere cloud = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White),
                Ambient = 0,
                Diffuse = 0,
                Specular = 0,
                Transparency = 1,
                Interior = new Interior
                {
                    Medium = new Medium { Scattering = new Color(1.5, 1.5, 1.5), Samples = 24 }
                }
            }
        };
        Scene scene = new () { Background = new SolidPigment(Colors.Black) };

        cloud.PrepareForRendering();
        scene.Surfaces.Add(cloud);
        scene.Lights.Add(new SkyLight
        {
            Pigment = new SolidPigment(Colors.White), Samples = 24
        });

        Color seen = scene.GetColorFor(new Ray(new Point(0, 0, -5), Directions.In), 3);

        Assert.IsTrue(seen.Red > 0.1, $"the sky should light the cloud, and gave {seen.Red}");

        // And with no sky at all it is dark, so what was seen came from the sky rather than anywhere else.
        Scene unlit = new () { Background = new SolidPigment(Colors.Black) };
        Sphere same = new ()
        {
            Material = cloud.Material, Transform = cloud.Transform
        };

        same.PrepareForRendering();
        unlit.Surfaces.Add(same);

        Assert.IsTrue(Colors.Black.Matches(
            unlit.GetColorFor(new Ray(new Point(0, 0, -5), Directions.In), 3)));
    }
}
