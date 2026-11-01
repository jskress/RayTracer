using Regex = System.Text.RegularExpressions.Regex;
using Match = System.Text.RegularExpressions.Match;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover lights standing on things: a light written in a group goes wherever the group
/// goes, once for every place it stands, and is not shadowed by what closes round it.
/// </summary>
[TestClass]
public class TestLightsOnObjects
{
    private string _directory;

    [TestInitialize]
    public void MakeDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"lights-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        Directory.Delete(_directory, true);
    }

    private static Scene Placed(int instants, params Surface[] surfaces)
    {
        double[] times = instants == 1 ? null : Enumerable.Range(0, instants).Select(n => n / (instants - 1.0)).ToArray();
        Scene scene = new ();

        scene.Surfaces.AddRange(surfaces);

        foreach (Surface surface in surfaces)
            surface.PrepareForRendering(times);

        scene.ArrangeSurfaces();
        LightPlacer.Place(scene, instants);

        return scene;
    }

    /// <summary>
    /// A light stands where its group puts it, and turns and grows with it: a lamp's place, a
    /// spotlight's aim and an area light's face all go through the group's transform, and a distance it
    /// fades over grows with the group's scale.
    /// </summary>
    [TestMethod]
    public void TestALightStandsWhereItsGroupPutsIt()
    {
        Matrix transform = Transforms.Translate(3, 0, -1) * Transforms.RotateAroundY(90) * Transforms.Scale(2);
        Group lamp = new () { Transform = transform };

        lamp.Add(new PointLight { Location = new Point(0, 1, 0), FadeDistance = 0.5 });
        lamp.Add(new Spotlight { Location = new Point(0, 1, 0), PointAt = new Point(1, 0, 0) });
        lamp.Add(new AreaLight { Location = new Point(0, 2, 0), Axis1 = new Vector(1, 0, 0), Axis2 = new Vector(0, 0, 0.5) });

        Scene scene = Placed(1, lamp);
        PointLight point = scene.Lights.OfType<PointLight>().Single(light => light.GetType() == typeof(PointLight));
        Spotlight spot = scene.Lights.OfType<Spotlight>().Single();
        AreaLight area = scene.Lights.OfType<AreaLight>().Single();

        Assert.HasCount(3, scene.Lights);
        Assert.IsTrue(point.Location.Matches(transform * new Point(0, 1, 0)), $"the lamp stood at {point.Location}");
        Assert.AreEqual(1, point.FadeDistance!.Value, 1e-12, "a fade written in a group grows with its scale");
        Assert.IsTrue(spot.PointAt.Matches(transform * new Point(1, 0, 0)), $"the spotlight aimed at {spot.PointAt}");
        Assert.IsTrue(area.Location.Matches(new Point(3, 4, -1)), $"the panel stood at {area.Location}");
        Assert.IsTrue(area.Axis1.Matches(new Vector(0, 0, -2)), $"the panel's first side ran {area.Axis1}");
        Assert.IsTrue(area.Axis2.Matches(new Vector(1, 0, 0)), $"the panel's second side ran {area.Axis2}");

        // The light written in the group is left as it was written.
        Assert.IsTrue(lamp.Lights.OfType<PointLight>().First().Location.Matches(new Point(0, 1, 0)));
    }

    /// <summary>
    /// **A light for every place.**  A shape shared by instances is one shape standing in many places,
    /// and its light becomes one light at each, carried out through each instance in turn -- through an
    /// instance standing inside another, too.
    /// </summary>
    [TestMethod]
    public void TestASharedShapeGivesALightAtEveryPlace()
    {
        Group lamp = new ();

        lamp.Add(new Sphere { Transform = Transforms.Scale(0.1) });
        lamp.Add(new PointLight { Location = new Point(0, 1, 0) });

        Group pair = new ();

        pair.Add(new Instance { Prototype = lamp, Transform = Transforms.Translate(-1, 0, 0) });
        pair.Add(new Instance { Prototype = lamp, Transform = Transforms.Translate(1, 0, 0) });

        Instance left = new () { Prototype = pair, Transform = Transforms.Translate(0, 0, 5) };
        Instance right = new () { Prototype = pair, Transform = Transforms.Translate(0, 0, -5) * Transforms.Scale(2) };
        Scene scene = Placed(1, left, right);
        Point[] places = scene.Lights.OfType<PointLight>().Select(light => light.Location).ToArray();
        Point[] expected = [new (-1, 1, 5), new (1, 1, 5), new (-2, 2, -5), new (2, 2, -5)];

        Assert.HasCount(4, places);

        foreach (Point place in expected)
            Assert.IsTrue(places.Any(found => found.Matches(place)), $"no light stood at {place}");
    }

    private static Group Lamp(Surface bulb, Surface shade)
    {
        Group lamp = new ();

        lamp.Add(bulb);
        lamp.Add(shade);
        lamp.Add(new PointLight { Location = Point.Zero });

        return lamp;
    }

    /// <summary>
    /// **What closes round a light does not shadow it.**  The bulb round each lamp's light is its
    /// housing, kept with the instance it was found through, so one lamp's bulb is not taken for the
    /// next's; an open shade round the light is no housing, and nor is anything closing round it that is
    /// not part of its own group.
    /// </summary>
    [TestMethod]
    public void TestOnlyWhatClosesRoundALightIsItsHousing()
    {
        Sphere bulb = new () { Transform = Transforms.Scale(0.1) };
        Cylinder shade = new () { MinimumY = -0.3, MaximumY = 0.3, Closed = false, Transform = Transforms.Scale(0.5, 1, 0.5) };
        Group lamp = Lamp(bulb, shade);
        Instance first = new () { Prototype = lamp, Transform = Transforms.Translate(-1, 0, 0) };
        Instance second = new () { Prototype = lamp, Transform = Transforms.Translate(1, 0, 0) };
        Sphere room = new () { Transform = Transforms.Scale(20) };
        Scene scene = Placed(1, first, second, room);
        PointLight[] lights = scene.Lights.OfType<PointLight>().OrderBy(light => light.Location.X).ToArray();

        Assert.HasCount(2, lights);

        for (int index = 0; index < 2; index++)
        {
            Instance own = index == 0 ? first : second;
            Instance other = index == 0 ? second : first;
            IReadOnlySet<(Surface, Portals)> housing = lights[index].Housing;

            Assert.IsNotNull(housing, $"lamp {index} found nothing round its light");
            Assert.HasCount(1, housing, $"lamp {index}'s housing held {housing.Count} things");
            Assert.Contains((bulb, new Portals(own)), housing, "the lamp's own bulb is its housing");
            Assert.DoesNotContain((bulb, new Portals(other)), housing, "the other lamp's bulb is not");
            Assert.DoesNotContain((shade, new Portals(own)), housing, "an open shade is no housing");
        }
    }

    /// <summary>
    /// **A light on something moving goes with it.**  Set moving while the shutter is open, a group
    /// carries its light, which stands where the group stood at each instant; a group that stays put
    /// leaves its light one light.
    /// </summary>
    [TestMethod]
    public void TestALightOnSomethingMovingGoesWithIt()
    {
        Group moving = new () { MotionAt = fraction => Transforms.Translate(fraction * 2, 0, 0) };
        Group still = new () { Transform = Transforms.Translate(0, 0, 4) };

        moving.Add(new PointLight { Location = new Point(0, 1, 0) });
        still.Add(new PointLight { Location = new Point(0, 1, 0) });

        Scene scene = Placed(3, moving, still);
        PointLight carried = (PointLight) scene.Lights[0];
        PointLight standing = (PointLight) scene.Lights[1];

        Assert.IsTrue(((PointLight) carried.At(0)).Location.Matches(new Point(0, 1, 0)));
        Assert.IsTrue(((PointLight) carried.At(1)).Location.Matches(new Point(1, 1, 0)));
        Assert.IsTrue(((PointLight) carried.At(2)).Location.Matches(new Point(2, 1, 0)));
        Assert.AreSame(standing, standing.At(2), "a light that stays put is one light");
    }

    private const string Room = """
        camera { location [0, 0.6, -3]  look at [0, 0, 0]  up [0, 1, 0]  field of view 10 }
        plane { material { pigment White  ambient 0  diffuse 1  specular 0 } }
        """;

    /// <summary>
    /// The bulb round a lamp's light leaves the floor under it lit -- and the same bulb round the same
    /// light written outside any group puts the floor in its shadow, since nothing says the two belong
    /// together.
    /// </summary>
    [TestMethod]
    public void TestABulbDoesNotShadowItsOwnLight()
    {
        const string bulb = "sphere { scale 0.1  translate Y 1  material { pigment White  ambient 1  diffuse 0 } }";

        Color inGroup = Center($$"""
            {{Room}}
            group { {{bulb}}  point light { location [0, 1, 0] } }
            """);
        Color loose = Center($$"""
            {{Room}}
            {{bulb}}
            point light { location [0, 1, 0] }
            """);

        Assert.IsTrue(inGroup.Red > 0.9, $"the floor under the lamp came out {inGroup}");
        Assert.IsTrue(loose.Red < 0.01, $"the floor under a loose light in a ball came out {loose}");
    }

    /// <summary>
    /// A shade open at its ends shadows its light as anything does: the floor straight under it is lit
    /// through the opening, and the floor off to the side is in the shade's shadow.  And a spotlight in a
    /// group turned on its side lights where the group turns it.
    /// </summary>
    [TestMethod]
    public void TestAnOpenShadeShadowsAndASpotlightTurns()
    {
        Canvas picture = Rendered("""
            camera { location [0, 6, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 60 }
            plane { material { pigment White  ambient 0  diffuse 1  specular 0 } }
            group {
                cylinder { min Y -1  max Y 1  open  scale [0.3, 1, 0.3]  material { pigment White } }
                point light { location [0, 0, 0] }
                translate Y 1.5
            }
            """, 41, 41);
        Color under = picture.GetPixel(20, 20);
        Color aside = picture.GetPixel(37, 20);

        Assert.IsTrue(under.Red > 0.3, $"the floor under the shade came out {under}");
        Assert.IsTrue(aside.Red < 0.01, $"the floor beside the shade came out {aside}");

        Color Spot(string turn) => Rendered($$"""
            camera { location [0, 6, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 60 }
            plane { material { pigment White  ambient 0  diffuse 1  specular 0 } }
            group {
                spot light { location [0, 0, 0]  point at [0, -1, 0]  radius 10  falloff 12 }
                {{turn}}
                translate Y 1
            }
            """, 41, 41).GetPixel(26, 20);

        Assert.IsTrue(Spot("").Red < 0.01, "a spotlight straight down lit the floor off to the side");
        Assert.IsTrue(Spot("rotate Z 45").Red > 0.1, "a spotlight turned with its group did not light where it was turned");
    }

    /// <summary>
    /// **Its light blurs with it.**  A spotlight aimed straight down in a group that moves two units
    /// while the shutter is open sweeps its spot along the floor, so the floor is lit along the whole
    /// of that; a light left where the group stood at any one instant lights only the spot's own width.
    /// </summary>
    [TestMethod]
    public void TestAMovingLampsLightBlursWithIt()
    {
        int Lit(string motion)
        {
            Canvas picture = Rendered($$"""
                camera {
                    location [0, 6, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 60
                    shutter 1  blur samples 16
                }
                plane { material { pigment White  ambient 0  diffuse 1  specular 0 } }
                group {
                    spot light { location [0, 0, 0]  point at [0, -1, 0]  radius 10  falloff 12 }
                    translate Y 1
                    {{motion}}
                }
                """, 41, 41);

            return Enumerable.Range(0, 41).Count(x => picture.GetPixel(x, 20).Red > 0.002);
        }

        int still = Lit("");
        int moving = Lit("motion { translate [2, 0, 0] }");

        Assert.IsTrue(still <= 4, $"standing still, the spot lit {still} pixels across");
        Assert.IsTrue(moving >= 10, $"moving, the spot lit only {moving} pixels across");
    }

    /// <summary>
    /// Fog round a lamp is lit by it through the bulb, as the floor is: a light reaching a medium leaves
    /// out what closes round it too.
    /// </summary>
    [TestMethod]
    public void TestALampLightsTheFogThroughItsBulb()
    {
        Color fog = Center("""
            camera { location [0, 1, -4]  look at [0, 1, 0]  field of view 20 }
            environment { medium { scattering 0.3 } }
            group {
                sphere { scale 0.1  material { pigment White  ambient 1  diffuse 0 } }
                point light { location [0, 0, 0] }
                translate [0.8, 1, 0]
            }
            """);

        Assert.IsTrue(fog.Red > 0.05, $"the fog beside the lamp came out {fog}");
    }

    /// <summary>
    /// A strand of hair beside a lamp is lit by it through the bulb too: a fiber's shadow rays leave out
    /// what closes round the light, as a surface's do.  One strand, so that nothing reaches it but the
    /// lamp itself -- light bounced among many strands is gathered another way.
    /// </summary>
    [TestMethod]
    public void TestALampLightsAStrandThroughItsBulb()
    {
        Canvas picture = Rendered("""
            camera { location [0, 0, -4]  look at [0, 0, 0]  field of view 3 }
            strand {
                width 0.2 at [0, -1, 0]
                width 0.2 at [0, 1, 0]
                material { pigment [0.8, 0.8, 0.8]  ambient 0 }
            }
            group {
                sphere { scale 0.1  material { pigment White  ambient 1  diffuse 0 } }
                point light { location [0, 0, 0] }
                translate [0.8, 0, -1.5]
            }
            """, 5, 5);
        double brightest = Enumerable.Range(0, 5).Max(x => picture.GetPixel(x, 2).Red);

        Assert.IsTrue(brightest > 0.05, $"the strand beside the lamp came out {brightest} at its brightest");
    }

    /// <summary>
    /// A light may stand in a loop or a choice inside a group, and in a loop at a scene's top level; a
    /// primitive giving back a group may hold one, and each call gives its own -- even a group holding the light
    /// alone, which until 2026-11-01 was taken for an empty one and handed back with nothing in it.
    /// </summary>
    [TestMethod]
    public void TestLightsMayStandInLoopsAndPrimitives()
    {
        Assert.AreEqual(3, LightsIn("""
            group { for i in [0, 2] { point light { location [i, 1, 0] } } }
            """));
        Assert.AreEqual(2, LightsIn("""
            for i in [0, 1] { point light { location [i, 1, 0] } }
            """));
        Assert.AreEqual(1, LightsIn("""
            group { if (true) { area light { location [0, 2, 0] } } else { point light { } } }
            """));
        Assert.AreEqual(4, LightsIn("""
            primitive Lamp() -> group {
                return group { sphere { scale 0.1 }  point light { location [0, 0, 0] } }
            }
            for i in [0, 3] { object Lamp() { translate [i, 1, 0] } }
            """));
        Assert.AreEqual(3, LightsIn("""
            primitive Bare(x) -> group {
                return group { point light { location [x, 1, 0] } }
            }
            object Bare(0)
            object Bare(1)
            object Bare(2)
            """));
    }

    /// <summary>
    /// A sun or a sky has no place to stand, so neither may stand in a group.
    /// </summary>
    [TestMethod]
    public void TestASunCannotStandInAGroup()
    {
        StringAssert.Contains(Complaint("group { distant light { direction [0, -1, 0] } }"),
            "A distant light is infinitely far off, so it cannot stand in a group");
        StringAssert.Contains(Complaint("group { sky light { } }"),
            "A sky light is infinitely far off, so it cannot stand in a group");
    }

    private int LightsIn(string body)
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            Rendered("camera { location [0, 2, -5]  look at [0, 0, 0] }\n" + body, 4, 4, true);
        }
        finally
        {
            Console.SetOut(was);
        }

        Match match = Regex.Match(captured.ToString(), @"Lights\s+(\d+):");

        Assert.IsTrue(match.Success, $"no lights were counted in: {captured}");

        return int.Parse(match.Groups[1].Value);
    }

    private string Complaint(string body)
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            Rendered("camera { location [0, 2, -5]  look at [0, 0, 0] }\n" + body, 4, 4);
        }
        catch (Exception exception)
        {
            return exception.Message + captured;
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString();
    }

    private Color Center(string body)
    {
        return Rendered(body, 5, 5).GetPixel(2, 2);
    }

    private Canvas Rendered(string body, int width, int height, bool statistics = false)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(scene, "context { no gamma  angles are degrees }\n" + body + "\n");

        ImageRenderer renderer = new LanguageParser(scene).Parse();

        if (renderer is null)
            return null;

        renderer.Render(new RenderOptions
        {
            OutputFileName = output, Width = width, Height = height, ProgressStyleText = "none",
            ReportStatistics = statistics
        });

        return File.Exists(output) ? new ImageFile(output).Load()[0] : null;
    }
}
