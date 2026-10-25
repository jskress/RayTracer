using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Instructions;
using RayTracer.Instructions.Transforms;
using RayTracer.Options;
using RayTracer.Parser;

namespace Tests;

/// <summary>
/// These tests hold joints, marks, aiming, reaching and spans to what they say: a joint turns about its pivot as a
/// rotation does, within its limits; aiming turns its facing toward a place or a named thing; a chain reaches; a span
/// stands between its marks -- and cover the scene language that makes them.
/// </summary>
[TestClass]
public class TestKinematics
{
    private static LiteralResolver<T> Literal<T>(T value) => new () { Value = value };

    // A scene of the given surfaces, prepared as a render prepares them.
    private static Scene SceneOf(params Surface[] surfaces)
    {
        Scene scene = new ();

        foreach (Surface surface in surfaces)
        {
            scene.Surfaces.Add(surface);
            surface.PrepareForRendering(null);
        }

        return scene;
    }

    private static Mark MarkAt(string name, Point at) => new ()
    {
        Name = name, Transform = Transforms.Translate(at.X, at.Y, at.Z)
    };

    /// <summary>
    /// A joint's turn is a rotation about its pivot: turned about Y, exactly `rotate Y` carried to the pivot.
    /// </summary>
    [TestMethod]
    public void TestAJointTurnsAsARotationAboutItsPivot()
    {
        JointCreator turn = new ()
        {
            PivotResolver = Literal(new Point(1, 2, 3)),
            AxisResolver = Literal(new Vector(0, 1, 0)),
            AngleResolver = Literal(0.5)
        };
        Matrix made = turn.ResolveAt(new RenderContext(), new Variables(), 1);
        Matrix expected = Transforms.Translate(1, 2, 3) * Transforms.RotateAroundY(0.5, true) *
                          Transforms.Translate(-1, -2, -3);

        Assert.IsTrue(made.Matches(expected), "a joint turned about Y is a rotation about Y at its pivot");
    }

    /// <summary>
    /// A joint is held within its limits, however far it is told or asked to turn.
    /// </summary>
    [TestMethod]
    public void TestAJointIsHeldWithinItsLimits()
    {
        JointCreator turn = new ()
        {
            AxisResolver = Literal(new Vector(0, 0, 1)),
            AngleResolver = Literal(2.0),
            LowestResolver = Literal(-0.5),
            HighestResolver = Literal(1.0)
        };
        Matrix made = turn.ResolveAt(new RenderContext(), new Variables(), 1);

        Assert.IsTrue(made.Matches(Transforms.RotateAroundZ(1.0, true)), "held at its highest");

        // Asked to aim behind it, a hinge turns only as far as it may.
        Joint hinge = new () { Axis = new Vector(0, 1, 0), Lowest = -0.3, Highest = 0.3, Aim = new Target(new Point(-5, 0, 0.1), null) };

        KinematicsSettler.Settle(SceneOf(hinge), null);

        Vector facing = hinge.DirectionToWorld(hinge.Facing);

        Assert.AreEqual(0.3, Math.Abs(Math.Atan2(facing.Z, facing.X)), 1e-9, "turned to its limit and no further");
    }

    /// <summary>
    /// A hinge aims by turning about its axis alone: its facing comes round to point at the place as seen along the
    /// axis, and keeps its lean along the axis.  A joint with no axis points straight at it.
    /// </summary>
    [TestMethod]
    public void TestAJointAimsAtAPlace()
    {
        Point target = new (3, 5, 4);
        Joint hinge = new () { Pivot = new Point(0, 1, 0), Axis = new Vector(0, 1, 0), Aim = new Target(target, null) };

        KinematicsSettler.Settle(SceneOf(hinge), null);

        Vector facing = hinge.DirectionToWorld(hinge.Facing);

        Assert.AreEqual(Math.Atan2(4, 3), Math.Atan2(facing.Z, facing.X), 1e-9, "faces the place seen from above");
        Assert.AreEqual(0, facing.Y, 1e-12, "a hinge about Y keeps its facing level");

        Joint free = new () { Pivot = new Point(0, 1, 0), Aim = new Target(target, null) };

        KinematicsSettler.Settle(SceneOf(free), null);

        Vector pointing = free.DirectionToWorld(free.Facing).Unit;
        Vector wanted = (target - free.SurfaceToWorld(free.Pivot)).Unit;

        Assert.IsTrue((pointing - wanted).Magnitude < 1e-9, $"points at it: {pointing} against {wanted}");
    }

    /// <summary>
    /// A joint aims at a named thing's origin -- the nearest of that name, the one sharing the most of its groups --
    /// and is refused a name nothing has, or one two things have and neither nearer.
    /// </summary>
    [TestMethod]
    public void TestAJointAimsAtTheNearestThingOfAName()
    {
        Joint hinge = new () { Axis = new Vector(0, 1, 0), Aim = new Target(null, "spot") };
        Group near = new ();
        Group far = new ();

        near.Add(hinge);
        near.Add(MarkAt("spot", new Point(0, 0, 4)));
        far.Add(MarkAt("spot", new Point(4, 0, 0)));

        // The other group's spot comes first in the scene, so the nearest is not simply the first found.
        KinematicsSettler.Settle(SceneOf(far, near), null);

        Vector facing = hinge.DirectionToWorld(hinge.Facing);

        Assert.AreEqual(Math.PI / 2, Math.Atan2(facing.Z, facing.X), 1e-9, "aims at its own group's spot, not the other");

        Joint lost = new () { Axis = new Vector(0, 1, 0), Aim = new Target(null, "nowhere") };

        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidOperationException>(() => KinematicsSettler.Settle(SceneOf(lost), null)).Message,
            "Nothing is named 'nowhere'");

        Joint torn = new () { Axis = new Vector(0, 1, 0), Aim = new Target(null, "twin") };

        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidOperationException>(() => KinematicsSettler.Settle(
                SceneOf(torn, MarkAt("twin", new Point(1, 0, 0)), MarkAt("twin", new Point(0, 0, 1))), null)).Message,
            "none nearer than another");
    }

    // A two-link arm in the X-Y plane: a shoulder at the origin turning about Z, an upper arm of 1.2 to an elbow
    // turning about Z, a forearm of 1 to the tip, the whole reaching for a place.
    private static (Joint Shoulder, Joint Elbow, Mark Tip) Arm(Point target, double? elbowLowest = null)
    {
        Joint shoulder = new () { Axis = new Vector(0, 0, 1), Reach = new Target(target, null), Effector = "tip" };
        Joint elbow = new () { Pivot = new Point(1.2, 0, 0), Axis = new Vector(0, 0, 1), Lowest = elbowLowest };
        Mark tip = MarkAt("tip", new Point(2.2, 0, 0));

        elbow.Add(tip);
        shoulder.Add(elbow);

        return (shoulder, elbow, tip);
    }

    /// <summary>
    /// What holds a joint that has turned is boxed again round where its parts now are: a cube aimed round from +X
    /// to +Z is found there by a ray through the group holding it, which was boxed round +X when it was prepared.
    /// </summary>
    [TestMethod]
    public void TestWhatHoldsATurnedJointIsBoxedAgain()
    {
        Joint hinge = new () { Axis = new Vector(0, 1, 0), Aim = new Target(new Point(0, 0, 5), null) };
        Group holder = new ();

        hinge.Add(new Cube { Transform = Transforms.Translate(3, 0, 0) * Transforms.Scale(0.5, 0.5, 0.5) });
        holder.Add(hinge);

        KinematicsSettler.Settle(SceneOf(holder), null);

        Assert.IsTrue((hinge.SurfaceToWorld(new Point(3, 0, 0)) - new Point(0, 0, 3)).Magnitude < 1e-9);

        List<Intersection> hits = [];

        holder.Intersect(new Ray(new Point(0, 5, 3), new Vector(0, -1, 0)), hits);

        Assert.IsTrue(hits.Count > 0, "a ray down onto where the cube now stands should meet it");
    }

    /// <summary>
    /// A chain reaches: its tip comes to a place within its reach, and toward one beyond it points straight at it;
    /// an elbow kept from bending one way bends the other.
    /// </summary>
    [TestMethod]
    public void TestAChainReaches()
    {
        Point within = new (1.0, 1.4, 0);
        (Joint shoulder, _, Mark tip) = Arm(within);

        KinematicsSettler.Settle(SceneOf(shoulder), null);

        Assert.IsTrue((tip.SurfaceToWorld(Point.Zero) - within).Magnitude < 1e-5,
            $"the tip should reach {within}, but stands at {tip.SurfaceToWorld(Point.Zero)}");

        Point beyond = new (3, 3, 0);
        (Joint stretching, _, Mark straining) = Arm(beyond);

        KinematicsSettler.Settle(SceneOf(stretching), null);

        Vector toward = (straining.SurfaceToWorld(Point.Zero) - Point.Zero).Unit;

        Assert.IsTrue((toward - (beyond - Point.Zero).Unit).Magnitude < 1e-4, "out of reach, it points straight at it");
        Assert.AreEqual(2.2, (straining.SurfaceToWorld(Point.Zero) - Point.Zero).Magnitude, 1e-4, "at full stretch");

        // Kept from bending clockwise, the elbow bends the other way to reach the same place.
        (Joint keeping, Joint elbow, Mark kept) = Arm(within, elbowLowest: 0);

        KinematicsSettler.Settle(SceneOf(keeping), null);

        Assert.IsTrue((kept.SurfaceToWorld(Point.Zero) - within).Magnitude < 1e-5, "it still reaches");
        Assert.IsTrue(elbow.DirectionToWorld(new Vector(1, 0, 0)).Cross(
            kept.SurfaceToWorld(Point.Zero) - elbow.SurfaceToWorld(elbow.Pivot)).Z >= -1e-9, "bent the allowed way");
    }

    /// <summary>
    /// A span stands its origin at one mark and its +X toward the other, its +Y as near up as that allows, and
    /// stretched, reaches the second exactly.
    /// </summary>
    [TestMethod]
    public void TestASpanStandsBetweenItsMarks()
    {
        Group holder = new ();
        Cube rod = new () { Span = new Span("from", "to", true) };

        holder.Add(MarkAt("from", new Point(1, 2, 0)));
        holder.Add(MarkAt("to", new Point(4, 6, 0)));
        holder.Add(rod);
        holder.Transform = Transforms.Translate(0, 0, 3);

        KinematicsSettler.Settle(SceneOf(holder), null);

        Assert.IsTrue(rod.SurfaceToWorld(Point.Zero).Matches(new Point(1, 2, 3)), "its origin at the first mark");
        Assert.IsTrue(rod.SurfaceToWorld(new Point(1, 0, 0)).Matches(new Point(4, 6, 3)), "its +X reaching the second");
        Assert.AreEqual(0, rod.DirectionToWorld(new Vector(0, 1, 0)).Z, 1e-12, "its +Y kept upright, in the plane");
        Assert.IsTrue(rod.DirectionToWorld(new Vector(0, 1, 0)).Y > 0, "and above");
    }

    /// <summary>
    /// What aims moves with what it aims at while the shutter is open: aimed at a thing in motion, a joint stands at
    /// each instant facing where the thing is then.
    /// </summary>
    [TestMethod]
    public void TestAimingFollowsAMovingThingThroughTheShutter()
    {
        double[] instants = [0, 0.5, 1];
        Mark moving = MarkAt("moving", new Point(4, 0, 0));

        moving.MotionAt = fraction => Transforms.Translate(0, 0, 4 * fraction);

        Joint hinge = new () { Axis = new Vector(0, 1, 0), Aim = new Target(null, "moving") };
        Scene scene = new ();

        scene.Surfaces.Add(hinge);
        scene.Surfaces.Add(moving);
        hinge.PrepareForRendering(instants);
        moving.PrepareForRendering(instants);

        KinematicsSettler.Settle(scene, instants);

        for (int instant = 0; instant < instants.Length; instant++)
        {
            Vector facing = hinge.DirectionToWorld(hinge.Facing, instant);

            Assert.AreEqual(Math.Atan2(4 * instants[instant], 4), Math.Atan2(facing.Z, facing.X), 1e-9, $"instant {instant}");
        }
    }

    /// <summary>
    /// A joint turned at one of the shutter's instants and not at another -- what it aims at passing through its
    /// axis, where no turn is the right one -- stands as it was resolved at the instant it was not turned.
    /// </summary>
    [TestMethod]
    public void TestAJointLeftUnturnedAtAnInstantStandsAsResolved()
    {
        double[] instants = [0, 0.5, 1];
        Mark moving = MarkAt("moving", new Point(4, 0, 4));

        // At the last instant the mark stands on the hinge's own axis, straight above its pivot.
        moving.MotionAt = fraction => Transforms.Translate(-4 * fraction, 5 * fraction, -4 * fraction);

        Joint hinge = new () { Axis = new Vector(0, 1, 0), Aim = new Target(null, "moving") };
        Scene scene = new ();

        scene.Surfaces.Add(hinge);
        scene.Surfaces.Add(moving);
        hinge.PrepareForRendering(instants);
        moving.PrepareForRendering(instants);

        KinematicsSettler.Settle(scene, instants);

        Vector first = hinge.DirectionToWorld(hinge.Facing, 0);
        Vector last = hinge.DirectionToWorld(hinge.Facing, 2);

        Assert.AreEqual(Math.PI / 4, Math.Atan2(first.Z, first.X), 1e-9, "turned toward the mark at first");
        Assert.IsTrue(last.Matches(hinge.Facing), "and as resolved once the mark is on its axis");
    }

    /// <summary>
    /// A shape holding a joint that aims or reaches, or a span, is not shared between places, and one that is
    /// shared anyway -- a joint the settler meets behind an instance -- is refused.
    /// </summary>
    [TestMethod]
    public void TestSolvedShapesAreNotShared()
    {
        Group aiming = new ();

        aiming.Add(new Joint { Axis = new Vector(0, 1, 0), Aim = new Target(new Point(1, 0, 0), null) });

        Assert.IsFalse(Instance.MayBeShared(aiming), "a group holding an aiming joint may not be shared");
        Assert.IsFalse(Instance.MayBeShared(new Cube { Span = new Span("a", "b", false) }), "nor a spanned surface");
        Assert.IsTrue(Instance.MayBeShared(new Joint { Axis = new Vector(0, 1, 0), Angle = 0.3 }), "a joint turned by its angle may");
        Assert.IsFalse(Instance.MayBeShared(new Group().Add(MarkAt("pin", Point.Zero))), "nor a shape holding a mark");

        Instance behind = new () { Prototype = aiming };

        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidOperationException>(() => KinematicsSettler.Settle(SceneOf(behind), null)).Message,
            "shared between places");
    }

    private static (Canvas Picture, string Error) Rendered(string body, long frame = 0)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"kinematics-tests-{Guid.NewGuid():N}");
        string scene = Path.Combine(directory, "scene.igl");
        string written = Path.Combine(directory, "scene.png");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Directory.CreateDirectory(directory);
        File.WriteAllText(scene,
            "context { angles are degrees  no gamma  animation length 1  rate 10 }\n" +
            "camera { location [0, 2, -6]  look at [0, 1, 0] }\n" +
            "point light { location [-4, 9, -9] }\n" + body);
        Console.SetOut(captured);

        try
        {
            new LanguageParser(scene).Parse()?.Render(new RenderOptions
            {
                OutputFileName = written, Width = 32, Height = 24, Frame = frame
            });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (new ImageFile(written).Load()[0], null);
        }
        catch (Exception exception)
        {
            return (null, exception.Message);
        }
        finally
        {
            Console.SetOut(was);
            Directory.Delete(directory, true);
        }
    }

    private static bool Same(Canvas one, Canvas other) =>
        Enumerable.Range(0, one.Width * one.Height)
            .All(index => one.GetPixel(index % one.Width, index / one.Width)
                .Matches(other.GetPixel(index % one.Width, index / one.Width)));

    /// <summary>
    /// The scene language: a joint turned by its angle is the same as a rotation about its pivot; one aiming,
    /// a chain reaching, marks and a stretched span all render; and what cannot be is refused.
    /// </summary>
    [TestMethod]
    public void TestTheSceneLanguageMakesJoints()
    {
        (Canvas joint, string error) = Rendered(
            "joint { pivot [0, 1, 0]  axis Z  angle 30  cube { scale [0.8, 0.1, 0.1]  translate [0.8, 1, 0] } }");
        (Canvas rotated, _) = Rendered(
            "group { cube { scale [0.8, 0.1, 0.1]  translate [0.8, 1, 0] }  translate [0, -1, 0]  rotate Z 30  translate [0, 1, 0] }");

        Assert.IsNull(error);
        Assert.IsTrue(Same(joint, rotated), "a joint turned by its angle is a rotation about its pivot");

        // The turn comes before the joint's own transforms, wherever in the block they are written.
        (Canvas placed, string placedError) = Rendered(
            "joint { translate [0.6, 0, 0]  axis Z  angle 30  cube { scale [0.8, 0.1, 0.1]  translate X 0.8 } }");
        (Canvas turnedThenMoved, _) = Rendered(
            "group { cube { scale [0.8, 0.1, 0.1]  translate X 0.8 }  rotate Z 30  translate [0.6, 0, 0] }");

        Assert.IsNull(placedError);
        Assert.IsTrue(Same(placed, turnedThenMoved), "a joint is turned in its own space, then placed");

        (Canvas solved, string solvedError) = Rendered("""
            sphere { named 'ball'  scale 0.2  translate [1.5, 1, 1] }
            joint { axis Y  aim at 'ball'  facing [1, 0, 0]  cube { scale [0.5, 0.05, 0.05]  translate X 0.5 } }
            group {
                joint {
                    pivot [0, 0, 0]  axis Z  limits -90 to 90  reach [1.2, 1.6, 0] with 'tip'
                    cube { scale [0.5, 0.04, 0.04]  translate X 0.5 }
                    joint {
                        pivot [1, 0, 0]  axis Z
                        cube { scale [0.5, 0.04, 0.04]  translate X 1.5 }
                        mark 'tip' at [2, 0, 0]
                    }
                }
                mark 'a' at [-1, 0.5, 0]
                mark 'b' at [-0.2, 1.5, 0]
                cube { scale [0.5, 0.03, 0.03]  translate X 0.5  span from 'a' to 'b' stretched }
            }
            """);

        Assert.IsNull(solvedError);
        Assert.IsNotNull(solved);

        StringAssert.Contains(Rendered("group { pivot [0, 1, 0] }").Error, "Only a joint");
        StringAssert.Contains(Rendered("joint { axis Y  aim at 3 }").Error, "a place, or a thing by its name");
        StringAssert.Contains(Rendered("joint { axis [0, 0, 0] }").Error, "some way, not nothing");
        StringAssert.Contains(Rendered("joint { axis Z  reach [1, 1, 0] with 'tip' }").Error, "Nothing is named 'tip'");
        StringAssert.Contains(
            Rendered("group { mark 'tip' at [1, 0, 0] }  joint { axis Z  reach [1, 1, 0] with 'tip' }").Error, "not inside it");
    }
}
