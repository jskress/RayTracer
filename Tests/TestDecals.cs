using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover decals: markings painted onto a surface by projecting an outline onto it.
/// <para>
/// **The first thing a decal needs is a space it can be placed in, and that is not the space a
/// pattern is read in.**  A pattern is read in the space of whichever part of a thing the ray met, and
/// a group hands its material down to every part that has none, so one material on a saucer is read in
/// the lathe's space on the lathe and the egg's on the egg.  A marking written on the saucer has to land
/// in one place on all of it.  So a material remembers the surface it was written on -- its anchor --
/// and a decal is read in that surface's space.  The tests below hold that rule to account in the
/// places it is easiest to get wrong: a part scaled and turned inside its group, a shape shared among
/// several places, and an anchor that stands above a shared shape rather than inside it.
/// </para>
/// </summary>
[TestClass]
public class TestDecals
{
    [TestMethod]
    public void TestAMaterialIsAnchoredToTheSurfaceItIsWrittenOn()
    {
        // A group wearing a material, holding one part with no material of its own and one with.
        GroupResolver resolver = new () { MaterialResolver = new MaterialResolver() };

        resolver.SurfaceResolvers.Add(new SphereResolver());
        resolver.SurfaceResolvers.Add(new SphereResolver { MaterialResolver = new MaterialResolver() });

        Group group = resolver.Resolve(new RenderContext(), new Variables());

        group.PrepareForRendering();

        Surface plain = group.Surfaces[0];
        Surface dressed = group.Surfaces[1];

        Assert.AreSame(group, group.Material.Anchor, "a material was not anchored to the group it was written on");
        Assert.AreSame(group.Material, plain.Material,
            "the part with no material of its own was not handed the group's");
        Assert.AreSame(group, plain.Material.Anchor,
            "handing a material down moved its anchor to the part it was handed to");
        Assert.AreSame(dressed, dressed.Material.Anchor,
            "a part's own material was not anchored to that part");
    }

    [TestMethod]
    public void TestEachUseOfANamedShapeHasAnAnchorOfItsOwn()
    {
        // A shape named once and used twice is built twice, and each copy must carry its own material
        // anchored to itself -- a material shared between the two would place a marking on the second
        // copy in the first one's space.
        GroupResolver resolver = new () { MaterialResolver = new MaterialResolver() };

        resolver.SurfaceResolvers.Add(new SphereResolver());

        Group first = ((GroupResolver) resolver.Clone()).Resolve(new RenderContext(), new Variables());
        Group second = ((GroupResolver) resolver.Clone()).Resolve(new RenderContext(), new Variables());

        Assert.AreNotSame(first.Material, second.Material, "two uses of one shape share one material");
        Assert.AreSame(first, first.Material.Anchor);
        Assert.AreSame(second, second.Material.Anchor);
    }

    [TestMethod]
    public void TestAPointIsCarriedIntoTheAnchorsSpaceAndNotThePartsSpace()
    {
        // A part squashed and turned inside its group, as the egg is inside the saucer.  A point on it is
        // asked for in the group's space, and the group is placed and turned in the world too, so every
        // link in the chain has to be walked the right way round.
        Group group = new () { Transform = Transforms.Translate(1.5, -0.4, 2) * Transforms.RotateAroundY(35) };
        Sphere part = new ()
        {
            Transform = Transforms.Translate(0.3, 0, 0) * Transforms.Scale(0.5, 1, 1) * Transforms.RotateAroundZ(-90)
        };

        group.Add(part);

        Material material = new () { Anchor = group };

        group.Material = material;
        group.PrepareForRendering();

        Point there = new (0.7, 0.2, -0.4);
        Point world = group.SurfaceToWorld(there);

        Assert.IsTrue(material.WorldToAnchor(part, world).Matches(there),
            "a point found on a part was not carried into the space of the group the material was written on");
        Assert.IsFalse(part.WorldToSurface(world).Matches(there),
            "the part's own space should differ from the group's, or this test proves nothing");

        // A footprint goes the same way: the group is unscaled, so a patch keeps its size there.
        Footprint patch = new (new Vector(0.1, 0, 0), new Vector(0, 0, 0.1));

        Assert.AreEqual(0.1, material.WorldToAnchor(part, patch).Across.Magnitude, 1e-9,
            "a footprint was not carried into the anchor's space with the point");
    }

    [TestMethod]
    public void TestAMaterialWithNoAnchorIsReadOnTheSurfaceWearingIt()
    {
        // The material a surface with none is given at the last moment was written on nothing, so it is
        // read where a pattern always has been.
        Sphere part = new () { Transform = Transforms.Scale(2, 1, 1) * Transforms.RotateAroundX(20) };
        Material material = new ();

        part.Material = material;
        part.PrepareForRendering();

        Point world = new (0.9, -0.3, 0.5);

        Assert.IsTrue(material.WorldToAnchor(part, world).Matches(part.WorldToSurface(world)));
    }

    [TestMethod]
    public void TestAnAnchorInsideASharedShapeIsPlacedByTheInstanceTheRayCameThrough()
    {
        // One shape, standing in two places.  Its own top has no parent, so the walk from a part inside
        // it runs out there and has to carry on through whichever instance the ray found it through --
        // and each instance places it differently, so a walk through the wrong one is caught.
        Group shape = new () { Transform = Transforms.RotateAroundY(25) * Transforms.Scale(0.8) };
        Sphere part = new () { Transform = Transforms.Scale(2, 1, 1) };

        shape.Add(part);

        Material material = new () { Anchor = shape };

        shape.Material = material;

        Instance left = new () { Prototype = shape, Transform = Transforms.Translate(-2, 0, 0) * Transforms.Scale(1.5) };
        Instance right = new () { Prototype = shape, Transform = Transforms.Translate(2, 1, 0) * Transforms.RotateAroundZ(40) };

        left.PrepareForRendering();
        right.PrepareForRendering();

        Point there = new (0.4, -0.3, 0.6);

        foreach (Instance instance in new[] { left, right })
        {
            Point world = instance.SurfaceToWorld(shape.Transform * there);

            Assert.IsTrue(material.WorldToAnchor(part, world, instance).Matches(there),
                "a point on a shared shape was not carried into the shape's space through its instance");
        }
    }

    [TestMethod]
    public void TestAnAnchorAboveAnInstanceIsNotPlacedByItASecondTime()
    {
        // A group holding an instance hands its material down into the shared shape.  The group stands
        // in the scene's own chain, so the instance must play no part in reaching it: carried through
        // the instance anyway, the point would be moved by the instance's placing twice over.
        Sphere shape = new () { Transform = Transforms.Scale(2, 1, 1) };
        Instance placed = new () { Prototype = shape, Transform = Transforms.Translate(1, 0.5, 0) * Transforms.RotateAroundX(30) };
        Group outer = new () { Transform = Transforms.Translate(-1, 2, 3) * Transforms.RotateAroundY(-50) };

        outer.Add(placed);

        Material material = new () { Anchor = outer };

        outer.Material = material;
        outer.PrepareForRendering();

        Assert.AreSame(material, shape.Material,
            "the group's material should reach the shared shape, or this test proves nothing");

        Point there = new (0.2, -0.6, 0.9);
        Point world = outer.SurfaceToWorld(there);

        Assert.IsTrue(material.WorldToAnchor(shape, world, placed).Matches(there),
            "a point was carried through an instance to reach an anchor that stands above it");
    }

    // ---------------------------------------------------------------------------------------------
    // The outline.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A square outline from (-1, -1) to (1, 1).
    /// </summary>
    private static GeneralPath Square() => new GeneralPath()
        .MoveTo(-1, -1).LineTo(1, -1).LineTo(1, 1).LineTo(-1, 1).ClosePath();

    [TestMethod]
    public void TestAPointIsInsideOrOutsideWhenThereIsNoFootprint()
    {
        FlattenedPath square = new (Square());

        Assert.AreEqual(1, square.CoverageAt(0, 0));
        Assert.AreEqual(1, square.CoverageAt(0.999, -0.999));
        Assert.AreEqual(0, square.CoverageAt(1.001, 0));
        Assert.AreEqual(0, square.CoverageAt(5, 5));
    }

    [TestMethod]
    public void TestAnEdgeIsCoveredInProportionToTheFootprint()
    {
        // A square footprint crossing a straight edge square on is the one case with a simple exact
        // answer: the share inside climbs in a straight line, from all of it when the footprint just
        // touches the edge from inside to none when it just touches it from outside.  The footprint's
        // edges are its whole width, not half of it, and getting that wrong is exactly the mistake
        // that halved every pattern filter here once (see Footprint.For), so it is pinned down.
        FlattenedPath square = new (Square());

        foreach ((double past, double expected) in new[]
            { (-0.1, 1.0), (-0.05, 0.75), (0.0, 0.5), (0.05, 0.25), (0.1, 0.0) })
        {
            double coverage = square.CoverageAt(1 + past, 0.2, 0.2, 0, 0, 0.2);

            Assert.AreEqual(expected, coverage, 1e-9, $"{past} past the edge");
        }
    }

    [TestMethod]
    public void TestAFootprintIsMeasuredAcrossTheEdgeItMeets()
    {
        // A footprint long and thin, lying along the edge rather than across it, should give the
        // edge a sharp step; turned to lie across it, a long ramp.  Measuring the footprint by its
        // longest side, which would be the easy thing, gives the long ramp both ways.  The first
        // point is inside on purpose: outside, the outline's box would answer before the edge did.
        FlattenedPath square = new (Square());

        Assert.AreEqual(1, square.CoverageAt(0.94, 0, 0.01, 0, 0, 1), 1e-9,
            "a footprint lying along the edge reached across it");
        Assert.AreEqual(0.4, square.CoverageAt(1.1, 0, 1, 0, 0, 0.01), 1e-9,
            "a footprint lying across the edge was not measured across it");
    }

    [TestMethod]
    public void TestACurveIsFollowedCloselyEnough()
    {
        // A circle from four cubics, with a point just inside the curve at the middle of one of
        // them -- where a chord cuts deepest.  A curve taken as the straight line between its ends
        // would put that point a good way outside.
        const double k = 0.5522847498;
        GeneralPath circle = new GeneralPath()
            .MoveTo(1, 0)
            .CubicTo(1, k, k, 1, 0, 1)
            .CubicTo(-k, 1, -1, k, -1, 0)
            .CubicTo(-1, -k, -k, -1, 0, -1)
            .CubicTo(k, -1, 1, -k, 1, 0)
            .ClosePath();
        FlattenedPath flat = new (circle);
        TwoDPoint middle = circle.Segments[0].GetPoint(0.5);
        double radius = Math.Sqrt(middle.X * middle.X + middle.Y * middle.Y);

        foreach ((double offset, double expected) in new[] { (-0.001, 1.0), (0.001, 0.0) })
        {
            double scale = (radius + offset) / radius;

            Assert.AreEqual(expected, flat.CoverageAt(middle.X * scale, middle.Y * scale),
                $"a point {offset} from the curve");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The decal.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A decal painting the square outline in the given color, placed as given.
    /// </summary>
    private static Decal SquareDecal(Color color, Matrix placing = null, double near = -0.5, double far = 0.5)
    {
        return new Decal
        {
            Outline = new FlattenedPath(Square()),
            Color = color,
            MinimumReach = near,
            MaximumReach = far,
            Transform = placing ?? Matrix.Identity
        };
    }

    [TestMethod]
    public void TestADecalPaintsOnlyWithinItsReach()
    {
        // Not reaching the far side of a thing is the whole reason a decal has a reach at all.
        Decal decal = SquareDecal(Colors.Blue);

        Assert.IsTrue(Colors.Blue.Matches(decal.LayerOver(Colors.White, new Point(0, 0.4, 0))));
        Assert.IsTrue(Colors.White.Matches(decal.LayerOver(Colors.White, new Point(0, 0.6, 0))));
        Assert.IsTrue(Colors.White.Matches(decal.LayerOver(Colors.White, new Point(0, -0.6, 0))));
    }

    [TestMethod]
    public void TestScalingADecalDoesNotChangeItsReach()
    {
        // A scale is written to size the outline.  The reach is said in the units of the thing the
        // decal is on, so it must come out the same however large the outline is made -- here ten
        // times, which would otherwise make the reach of half a unit five.
        Decal decal = SquareDecal(Colors.Blue, Transforms.Scale(10));

        Assert.IsTrue(Colors.Blue.Matches(decal.LayerOver(Colors.White, new Point(8, 0.4, 0))),
            "the scale did not size the outline");
        Assert.IsTrue(Colors.White.Matches(decal.LayerOver(Colors.White, new Point(8, 0.6, 0))),
            "the scale stretched the reach");
    }

    [TestMethod]
    public void TestADecalReachesFromWhereItIsPlacedAlongTheWayItFaces()
    {
        // Moved up three, the reach goes with it; turned to face along Z, the outline lies in X-Y
        // and the reach runs along Z.
        Decal raised = SquareDecal(Colors.Blue, Transforms.Translate(0, 3, 0));

        Assert.IsTrue(Colors.Blue.Matches(raised.LayerOver(Colors.White, new Point(0, 3.2, 0))));
        Assert.IsTrue(Colors.White.Matches(raised.LayerOver(Colors.White, new Point(0, 0.2, 0))));

        Decal turned = SquareDecal(Colors.Blue, Transforms.RotateAroundX(90));

        Assert.IsTrue(Colors.Blue.Matches(turned.LayerOver(Colors.White, new Point(0.5, -0.5, 0.3))));
        Assert.IsTrue(Colors.White.Matches(turned.LayerOver(Colors.White, new Point(0.5, -0.5, 3))));
        Assert.IsTrue(Colors.White.Matches(turned.LayerOver(Colors.White, new Point(0.5, -3, 0.3))));
    }

    [TestMethod]
    public void TestATranslucentDecalLetsTheSurfaceShowThrough()
    {
        Decal decal = SquareDecal(new Color(0, 0, 1, 0.5));
        Color color = decal.LayerOver(Colors.White, Point.Zero);

        Assert.IsTrue(new Color(0.5, 0.5, 1).Matches(color), color.ToString());
    }

    [TestMethod]
    public void TestALaterDecalGoesOnTop()
    {
        Sphere sphere = new ();
        Material material = new ()
        {
            Pigment = new SolidPigment(Colors.White),
            Decals = [SquareDecal(Colors.Blue, near: -2, far: 2), SquareDecal(Colors.Red, near: -2, far: 2)],
            Anchor = sphere
        };

        sphere.Material = material;
        sphere.PrepareForRendering();

        Assert.IsTrue(Colors.Red.Matches(material.GetColorFor(sphere, new Point(0, 1, 0))));
    }

    [TestMethod]
    public void TestADecalOnAGroupIsPlacedInTheGroupsSpaceOnEveryPart()
    {
        // A part stretched four times along X, inside a group wearing a two-unit square.  Three units
        // out along the part is well outside the square in the group's space; in the part's own, it
        // is three quarters of a unit out, and inside.  Halfway to the edge of the square is inside
        // either way, which shows the decal is there at all.
        Group group = new ();
        Sphere part = new () { Transform = Transforms.Scale(4, 1, 1) };

        group.Add(part);

        Material material = new ()
        {
            Pigment = new SolidPigment(Colors.White),
            Decals = [SquareDecal(Colors.Blue, near: -2, far: 2)],
            Anchor = group
        };

        group.Material = material;
        group.PrepareForRendering();

        Assert.AreSame(material, part.Material);
        Assert.IsTrue(Colors.Blue.Matches(
            material.GetColorFor(part, new Point(0.5, Math.Sqrt(1 - 0.25 / 16), 0))));
        Assert.IsTrue(Colors.White.Matches(
            material.GetColorFor(part, new Point(3, Math.Sqrt(1 - 9.0 / 16), 0))),
            "the decal was placed in the space of the part rather than the group");
    }

    // ---------------------------------------------------------------------------------------------
    // The wrapped projections.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A decal painting the given outline -- the square, if none is given -- wrapped as given.
    /// </summary>
    private static Decal Wrapped(
        DecalProjection projection, double near, double far, Matrix placing = null, double ring = 1,
        GeneralPath outline = null)
    {
        return new Decal
        {
            Outline = new FlattenedPath(outline ?? Square()),
            Color = Colors.Blue,
            Projection = projection,
            MinimumReach = near,
            MaximumReach = far,
            RingRadius = ring,
            Transform = placing ?? Matrix.Identity
        };
    }

    private static bool Paints(Decal decal, Point point)
    {
        return Colors.Blue.Matches(decal.LayerOver(Colors.White, point));
    }

    private static Point OnCylinder(double radius, double theta, double y)
    {
        return new Point(radius * Math.Cos(theta), y, radius * Math.Sin(theta));
    }

    private static Point OnSphere(double radius, double longitude, double latitude)
    {
        return new Point(
            radius * Math.Cos(latitude) * Math.Cos(longitude), radius * Math.Sin(latitude),
            radius * Math.Cos(latitude) * Math.Sin(longitude));
    }

    private static Point OnTorus(double ring, double tube, double theta, double phi)
    {
        double rho = ring + tube * Math.Cos(phi);

        return new Point(rho * Math.Cos(theta), tube * Math.Sin(phi), rho * Math.Sin(theta));
    }

    [TestMethod]
    public void TestACylindricalDecalKeepsItsTrueSizeOnAnyRadius()
    {
        // The square is two units across, so its edge is one unit of arc either side of the middle,
        // whatever the radius -- which is the whole point of measuring along the surface rather than
        // by angle.
        Decal decal = Wrapped(DecalProjection.Cylindrical, 0, 10);

        foreach (double radius in new[] { 0.5, 2, 5 })
        {
            Assert.IsTrue(Paints(decal, OnCylinder(radius, 0.9 / radius, 0.5)), $"inside, at {radius}");
            Assert.IsFalse(Paints(decal, OnCylinder(radius, 1.1 / radius, 0.5)), $"beyond the side, at {radius}");
            Assert.IsFalse(Paints(decal, OnCylinder(radius, 0, 1.1)), $"above the top, at {radius}");
        }
    }

    [TestMethod]
    public void TestASphericalDecalIsMeasuredAlongTheEquatorAndAMeridian()
    {
        Decal decal = Wrapped(DecalProjection.Spherical, 2.9, 3.1);

        Assert.IsTrue(Paints(decal, OnSphere(3, 0.9 / 3, 0.9 / 3)));
        Assert.IsFalse(Paints(decal, OnSphere(3, 1.1 / 3, 0)), "beyond the side");
        Assert.IsFalse(Paints(decal, OnSphere(3, 0, 1.1 / 3)), "above the top");
        Assert.IsFalse(Paints(decal, OnSphere(2, 0, 0)), "inside the reach's inner shell");
    }

    [TestMethod]
    public void TestAToroidalDecalWrapsAroundTheRingAndTheTube()
    {
        // A ring of three, a tube of a half.  Across runs around the ring at the point's own distance
        // from the axis, three and a half on the outer equator, and up runs around the tube.
        GeneralPath small = new GeneralPath()
            .MoveTo(-0.2, -0.2).LineTo(0.2, -0.2).LineTo(0.2, 0.2).LineTo(-0.2, 0.2).ClosePath();
        Decal decal = Wrapped(DecalProjection.Toroidal, 0.4, 0.6, ring: 3, outline: small);

        Assert.IsTrue(Paints(decal, OnTorus(3, 0.5, 0.18 / 3.5, 0)));
        Assert.IsFalse(Paints(decal, OnTorus(3, 0.5, 0.22 / 3.5, 0)), "beyond the side");
        Assert.IsTrue(Paints(decal, OnTorus(3, 0.5, 0, 0.18 / 0.5)));
        Assert.IsFalse(Paints(decal, OnTorus(3, 0.5, 0, 0.22 / 0.5)), "above the top");
        Assert.IsFalse(Paints(decal, OnTorus(3, 0.8, 0, 0)), "outside the reach");
    }

    [TestMethod]
    public void TestEveryWrappedDecalReadsLeftToRightFromOutside()
    {
        // A square in the outline's upper right, so it is painted only where across and up both run
        // the right way: across toward +Z, as seen from outside on the +X side, and up toward +Y.
        GeneralPath corner = new GeneralPath()
            .MoveTo(0, 0).LineTo(0.3, 0).LineTo(0.3, 0.3).LineTo(0, 0.3).ClosePath();

        (string name, Decal decal, Func<double, double, Point> at)[] cases =
        [
            ("cylindrical", Wrapped(DecalProjection.Cylindrical, 0, 2, outline: corner),
                (u, v) => OnCylinder(1, u, v)),
            ("spherical", Wrapped(DecalProjection.Spherical, 0, 2, outline: corner),
                (u, v) => OnSphere(1, u, v)),
            ("toroidal", Wrapped(DecalProjection.Toroidal, 0, 2, ring: 3, outline: corner),
                (u, v) => OnTorus(3, 1, u / 4, v))
        ];

        foreach ((string name, Decal decal, Func<double, double, Point> at) in cases)
        {
            Assert.IsTrue(Paints(decal, at(0.15, 0.15)), $"{name}: not painted where it should be");
            Assert.IsFalse(Paints(decal, at(-0.15, 0.15)), $"{name}: across runs the wrong way");
            Assert.IsFalse(Paints(decal, at(0.15, -0.15)), $"{name}: up runs the wrong way");
        }
    }

    [TestMethod]
    public void TestScalingAWrappedDecalSizesTheOutlineAndNothingElse()
    {
        // Doubled, the square's edge moves out to two units of arc; but the reach, and a toroidal
        // decal's ring, are where the surface is, and a scale written to size the outline must not
        // move them.
        Decal cylindrical = Wrapped(DecalProjection.Cylindrical, 1.9, 2.1, Transforms.Scale(2));

        Assert.IsTrue(Paints(cylindrical, OnCylinder(2, 1.8 / 2, 0)), "the outline was not doubled");
        Assert.IsFalse(Paints(cylindrical, OnCylinder(2, 2.2 / 2, 0)));

        Decal spherical = Wrapped(DecalProjection.Spherical, 1.9, 2.1, Transforms.Scale(2));

        Assert.IsTrue(Paints(spherical, OnSphere(2, 1.8 / 2, 0)), "the spherical reach was scaled");

        Decal toroidal = Wrapped(DecalProjection.Toroidal, 0.4, 0.6, Transforms.Scale(2), ring: 3);

        Assert.IsTrue(Paints(toroidal, OnTorus(3, 0.5, 1.8 / 3.5, 0)),
            "the ring or the reach was scaled with the outline");
    }

    [TestMethod]
    public void TestAWrappedDecalsEdgesAreSoftenedAcrossTheirFootprints()
    {
        // At each projection's side edge and top edge in turn: a point a quarter of a footprint
        // inside, with a footprint lying along the surface and a tenth of a unit wide across the
        // edge, is three quarters covered.  That holds only if the footprint is carried into the
        // outline's plane at the right rate in each direction, which is what is being checked.  No
        // radius here is 1, so a rate that leaves out a radius, or takes it once too often, shows.
        Vector up = new (0, 0.1, 0);

        (string name, Decal decal, Point at, Vector across, Vector along)[] cases =
        [
            ("cylindrical side", Wrapped(DecalProjection.Cylindrical, 0, 5),
                OnCylinder(2, 0.975 / 2, 0), Tangent(0.975 / 2), up),
            ("cylindrical top", Wrapped(DecalProjection.Cylindrical, 0, 5),
                OnCylinder(2, 0, 0.975), up, new Vector(0, 0, 0.1)),
            ("spherical side", Wrapped(DecalProjection.Spherical, 0, 5),
                OnSphere(2, 0.975 / 2, 0), Tangent(0.975 / 2), up),
            ("spherical top", Wrapped(DecalProjection.Spherical, 0, 5),
                OnSphere(2, 0, 0.975 / 2), Meridian(0.975 / 2), new Vector(0, 0, 0.1)),
            ("toroidal side", Wrapped(DecalProjection.Toroidal, 0, 5, ring: 5),
                OnTorus(5, 2, 0.975 / 7, 0), Tangent(0.975 / 7), up),
            ("toroidal top", Wrapped(DecalProjection.Toroidal, 0, 5, ring: 5),
                OnTorus(5, 2, 0, 0.975 / 2), Meridian(0.975 / 2), new Vector(0, 0, 0.1))
        ];

        foreach ((string name, Decal decal, Point at, Vector across, Vector along) in cases)
        {
            Color color = decal.LayerOver(Colors.White, at, new Footprint(across, along));

            // Blue laid over white at a coverage of c leaves red at 1 - c.
            Assert.AreEqual(0.75, 1 - color.Red, 1e-3, $"{name}: {color}");
        }

        return;

        // A tenth of a unit around the Y axis, and a tenth of a unit up a meridian, at an angle.
        static Vector Tangent(double angle) => new Vector(-Math.Sin(angle), 0, Math.Cos(angle)) * 0.1;
        static Vector Meridian(double angle) => new Vector(-Math.Sin(angle), Math.Cos(angle), 0) * 0.1;
    }

    // ---------------------------------------------------------------------------------------------
    // Everything that asks a surface's color.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A clear pane of glass at a height of 2, with an opaque square of the given color painted on it
    /// from above, about the Y axis.  It glows its own color, so what it shows is its color and
    /// nothing of the light's.  The pane stands in a group the material is written on, so that it
    /// may be tilted about Z beneath a decal that still comes straight down.
    /// </summary>
    private static Scene SceneWithAPane(Color paint, Action<Material> adjust = null, double tilt = 0)
    {
        Group group = new ();
        Plane pane = new () { Transform = Transforms.Translate(0, 2, 0) * Transforms.RotateAroundZ(tilt) };
        Material material = new ()
        {
            Pigment = new SolidPigment(new Color(1, 1, 1, 0)),
            Ambient = 1,
            Diffuse = 0,
            Specular = 0,
            Decals = [SquareDecal(paint, near: 1.5, far: 2.5)],
            Anchor = group
        };

        adjust?.Invoke(material);

        group.Add(pane);
        group.Material = material;
        group.PrepareForRendering();

        Scene scene = new () { Background = new SolidPigment(Colors.Green) };

        scene.Lights.Add(new PointLight { Location = new Point(0, 5, 0) });
        scene.Surfaces.Add(group);

        return scene;
    }

    private static Color LookingDownAt(Scene scene, double x)
    {
        return scene.GetColorFor(new Ray(new Point(x, 5, 0), new Vector(0, -1, 0)));
    }

    [TestMethod]
    public void TestPaintOnGlassIsSeenAndNotSeenThrough()
    {
        // Lit, the paint shows its color; and the glass under it is as good as opaque there, so
        // nothing of what lies beyond is added in.  Beside it, the glass is as clear as ever.
        Scene scene = SceneWithAPane(Colors.Blue);
        Color painted = LookingDownAt(scene, 0);
        Color clear = LookingDownAt(scene, 1.5);

        Assert.IsTrue(Colors.Blue.Matches(painted), $"the paint looked {painted}");
        Assert.IsTrue(Colors.Green.Matches(clear), $"the clear glass looked {clear}");
    }

    [TestMethod]
    public void TestPaintOnGlassCastsAShadow()
    {
        Scene scene = SceneWithAPane(Colors.Blue);
        Light light = scene.Lights[0];

        // Straight down from the light, the shadow ray crosses the pane through the paint; from the
        // light to a point well off to the side, it crosses through clear glass.
        Assert.IsTrue(Colors.Black.Matches(scene.GetLightReaching(light, Point.Zero)),
            "light passed through the paint");
        Assert.IsTrue(Colors.White.Matches(scene.GetLightReaching(light, new Point(5, 0, 0))),
            "the clear glass beside the paint cast a shadow");
    }

    [TestMethod]
    public void TestAMetalTakesTheColorOfItsPaintInWhatItMirrors()
    {
        // A metal mirrors the world in its own color, and where it is painted, that is the paint's.
        // Seen square on, as here, the tint is all but the whole of it, so a green world mirrored in
        // blue paint comes back all but black, and in bare white metal, exactly as green as it is.
        Scene scene = SceneWithAPane(Colors.Blue, material =>
        {
            material.Pigment = new SolidPigment(Colors.White);
            material.Ambient = 0;
            material.Reflective = 1;
            material.Metallic = 1;
        });
        Color painted = LookingDownAt(scene, 0);
        Color bare = LookingDownAt(scene, 1.5);

        Assert.IsTrue(painted.Green < 0.05 * Colors.Green.Green, $"the paint mirrored {painted}");
        Assert.IsTrue(Colors.Green.Matches(bare), $"the bare metal mirrored {bare}");
    }

    [TestMethod]
    public void TestPaintOnAFaceTurnedAwayIsNotThereForAnything()
    {
        // The same pane, turned 80 degrees beneath paint that still comes straight down: past the
        // 75 at which a decal is gone.  So it must be gone for everything that asks -- seen, lit,
        // seen through, shadowing and mirrored alike -- since each of them asks by its own road.
        Scene glass = SceneWithAPane(Colors.Blue, tilt: 80);
        Light light = glass.Lights[0];
        Color seen = LookingDownAt(glass, 0);

        Assert.IsTrue(Colors.Green.Matches(seen), $"the glass looked {seen}");
        Assert.IsTrue(Colors.White.Matches(glass.GetLightReaching(light, Point.Zero)),
            "the paint on the turned face still cast a shadow");

        Scene metal = SceneWithAPane(Colors.Blue, material =>
        {
            material.Pigment = new SolidPigment(Colors.White);
            material.Ambient = 0;
            material.Reflective = 1;
            material.Metallic = 1;
        }, 80);
        Color mirrored = LookingDownAt(metal, 0);

        Assert.IsTrue(Colors.Green.Matches(mirrored), $"the metal mirrored {mirrored}");
    }

    // ---------------------------------------------------------------------------------------------
    // Facing.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// How much of a decal is painted at a point, given the surface's normal there.
    /// </summary>
    private static double Strength(Decal decal, Point point, Vector normal)
    {
        // Blue laid over white at a strength of s leaves red at 1 - s.
        return 1 - decal.LayerOver(Colors.White, point, null, normal).Red;
    }

    /// <summary>
    /// A unit normal tilted the given number of degrees from +Y toward +X.
    /// </summary>
    private static Vector TiltedFromUp(double degrees)
    {
        double radians = degrees * Math.PI / 180;

        return new Vector(Math.Sin(radians), Math.Cos(radians), 0);
    }

    [TestMethod]
    public void TestADecalFadesWhereTheSurfaceTurnsAwayFromIt()
    {
        Decal decal = SquareDecal(Colors.Blue);

        Assert.AreEqual(1, Strength(decal, Point.Zero, TiltedFromUp(0)), 1e-12);
        Assert.AreEqual(1, Strength(decal, Point.Zero, TiltedFromUp(59)), 1e-12, "faded before 60 degrees");
        Assert.AreEqual(0, Strength(decal, Point.Zero, TiltedFromUp(76)), 1e-12, "not gone by 75 degrees");

        double early = Strength(decal, Point.Zero, TiltedFromUp(63));
        double late = Strength(decal, Point.Zero, TiltedFromUp(72));

        Assert.IsTrue(1 > early && early > late && late > 0, $"{early} then {late} is not a fade");

        // It eases in and out, so it draws no line of its own where it starts and stops: a degree
        // inside either end, it has hardly begun or has all but finished.  A straight ramp would
        // have moved by a fifteenth already.
        Assert.IsTrue(Strength(decal, Point.Zero, TiltedFromUp(61)) > 0.98, "the fade starts with a jolt");
        Assert.IsTrue(Strength(decal, Point.Zero, TiltedFromUp(74)) < 0.02, "the fade stops with a jolt");

        // Which side the surface faces from is no concern of the decal's: a normal turned toward
        // the eye, as a shading normal often is, must not change a thing.
        Assert.AreEqual(1, Strength(decal, Point.Zero, -TiltedFromUp(0)), 1e-12);
        Assert.AreEqual(early, Strength(decal, Point.Zero, -TiltedFromUp(63)), 1e-12);

        // And with no normal to go on, a decal does not fade at all.
        Assert.AreEqual(1, Strength(decal, Point.Zero, null), 1e-12);
    }

    [TestMethod]
    public void TestAFadeMayBeMovedOrTurnedOff()
    {
        Decal decal = SquareDecal(Colors.Blue);

        decal.FadeFrom = Math.PI / 6;
        decal.FadeTo = Math.PI / 4;

        Assert.AreEqual(1, Strength(decal, Point.Zero, TiltedFromUp(29)), 1e-12, "faded before 30 degrees");
        Assert.AreEqual(0, Strength(decal, Point.Zero, TiltedFromUp(46)), 1e-12, "not gone by 45 degrees");

        double between = Strength(decal, Point.Zero, TiltedFromUp(37.5));

        Assert.IsTrue(between is > 0 and < 1, $"{between} is not between");

        // Two equal angles make a clean cut, rather than a division by nothing.
        decal.FadeFrom = Math.PI / 4;

        Assert.AreEqual(1, Strength(decal, Point.Zero, TiltedFromUp(44.9)), 1e-12);
        Assert.AreEqual(0, Strength(decal, Point.Zero, TiltedFromUp(45.1)), 1e-12);

        // And a decal that does not fade paints a face all but edge on.
        decal.Fades = false;

        Assert.AreEqual(1, Strength(decal, Point.Zero, TiltedFromUp(89)), 1e-12);
    }

    [TestMethod]
    public void TestAFadeGivenBackwardsIsPutInOrder()
    {
        // As a reach is: a fade between two angles has only the one meaning, whichever is said first.
        DecalResolver resolver = new ()
        {
            PathResolver = new LiteralResolver<GeneralPath> { Value = Square() },
            ColorResolver = new LiteralResolver<Color> { Value = Colors.Blue },
            MinimumResolver = new LiteralResolver<double> { Value = -1 },
            MaximumResolver = new LiteralResolver<double> { Value = 1 },
            FadeFromResolver = new LiteralResolver<double> { Value = Math.PI / 4 },
            FadeToResolver = new LiteralResolver<double> { Value = Math.PI / 6 }
        };
        Decal decal = resolver.Resolve(new RenderContext(), new Variables());

        Assert.AreEqual(Math.PI / 6, decal.FadeFrom, 1e-12);
        Assert.AreEqual(Math.PI / 4, decal.FadeTo, 1e-12);
    }

    [TestMethod]
    public void TestADecalFacesTheWayItIsTurned()
    {
        // Turned to come in along Z, a planar decal is square on to a face whose normal runs along
        // Z, and edge on to one whose normal runs up.
        Decal decal = SquareDecal(Colors.Blue, Transforms.RotateAroundX(90));

        Assert.AreEqual(1, Strength(decal, Point.Zero, new Vector(0, 0, 1)), 1e-12);
        Assert.AreEqual(0, Strength(decal, Point.Zero, new Vector(0, 1, 0)), 1e-12);
    }

    [TestMethod]
    public void TestAWrappedDecalFacesOutFromWhatItIsWrappedAbout()
    {
        // A wrapped decal comes in square on to a surface that faces straight out from its axis,
        // center or ring -- the side of a can, a ball, a ring's tube -- and edge on to a can's end.
        Decal cylindrical = Wrapped(DecalProjection.Cylindrical, 0, 5);
        Point side = OnCylinder(2, 0.3, 0.5);

        Assert.AreEqual(1, Strength(cylindrical, side, new Vector(Math.Cos(0.3), 0, Math.Sin(0.3))), 1e-12);
        Assert.AreEqual(0, Strength(cylindrical, side, new Vector(0, 1, 0)), 1e-12, "it painted a can's end");

        Decal spherical = Wrapped(DecalProjection.Spherical, 0, 5);
        Point ball = OnSphere(2, 0.3, 0.2);
        Vector outward = new Vector(ball.X, ball.Y, ball.Z).Unit;

        Assert.AreEqual(1, Strength(spherical, ball, outward), 1e-12);
        Assert.AreEqual(0, Strength(spherical, ball, outward.Cross(new Vector(0, 1, 0)).Unit), 1e-12);

        // Well up the tube, where out from the ring and out from the axis part company by 69 degrees.
        Decal toroidal = Wrapped(DecalProjection.Toroidal, 0, 5, ring: 3);
        Point tube = OnTorus(3, 0.5, 0.1, 1.2);
        Vector away = (new Vector(tube.X, tube.Y, tube.Z) -
                       new Vector(3 * Math.Cos(0.1), 0, 3 * Math.Sin(0.1))).Unit;

        Assert.AreEqual(1, Strength(toroidal, tube, away), 1e-12);
        Assert.AreEqual(0, Strength(toroidal, tube, new Vector(-Math.Sin(0.1), 0, Math.Cos(0.1))), 1e-12);
    }

    [TestMethod]
    public void TestASurfacesNormalIsCarriedIntoTheAnchorAsANormal()
    {
        // A group squashed to half its height in the world, wearing paint from above.  A face 63
        // degrees from level in the group's own space is only 44.5 from level in the world, since the
        // squash flattens it; the decal is measured in the group's space, as all of it is, so it must
        // be partly faded.  Carried in as though it were a direction rather than a normal, the
        // world's normal would come out steeper still, not shallower, and the decal would be at full
        // strength.  Turning the group as well makes sure the normal is turned on the way in.
        Group group = new ()
        {
            Transform = Transforms.RotateAroundY(30) * Transforms.Scale(1, 0.5, 1)
        };
        Sphere part = new ();

        group.Add(part);

        Material material = new ()
        {
            Pigment = new SolidPigment(Colors.White),
            Decals = [SquareDecal(Colors.Blue, near: -5, far: 5)],
            Anchor = group
        };

        group.Material = material;
        group.PrepareForRendering();

        Vector inGroup = TiltedFromUp(63);
        Vector inWorld = (Transforms.RotateAroundY(30) *
                          new Vector(inGroup.X, inGroup.Y / 0.5, inGroup.Z)).Unit;
        Point where = group.SurfaceToWorld(Point.Zero);
        double strength = 1 - material.GetColorFor(part, where, inWorld).Red;
        Decal alone = SquareDecal(Colors.Blue, near: -5, far: 5);

        Assert.AreEqual(Strength(alone, Point.Zero, inGroup), strength, 1e-9,
            "the normal did not arrive in the group's space as the same normal");
        Assert.IsTrue(strength < 1, "the face was measured in the world's space, not the group's");
    }

    // ---------------------------------------------------------------------------------------------
    // The language.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Renders a white floor, looked at from straight above, wearing the given material additions,
    /// and hands back the picture.  The floor glows its own color, so a pixel is exactly the color
    /// painted there.  The picture is 60 pixels square and its middle is the origin; each pixel is a
    /// little under a tenth of a unit, with X running right and Z running up.
    /// </summary>
    private static Canvas FloorPainted(string decals)
    {
        return FloorShowing(
            "plane { material { pigment color White  ambient 1  diffuse 0  specular 0\n" +
            decals + "\n} }");
    }

    /// <summary>
    /// The same, for a scene that writes out the floor in full, and whatever else it needs.
    /// </summary>
    private static Canvas FloorShowing(string body, int size = 60)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"decal-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string scene = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "scene.png");

            File.WriteAllText(scene,
                "context { no gamma  angles are degrees }\n" +
                (body.Contains("camera {")
                    ? ""
                    : "camera { location [0, 10, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 60 }\n") +
                "point light { location [0, 20, 0] }\n" + body + "\n");

            new LanguageParser(scene).Parse().Render(new RenderOptions
            {
                OutputFileName = output, Width = size, Height = size
            });

            return new ImageFile(output).Load()[0];
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// The color of the floor at a place on it, in a picture from <see cref="FloorPainted"/>.
    /// </summary>
    private static Color FloorAt(Canvas picture, double x, double z)
    {
        double across = 10 * Math.Tan(Math.PI / 6);

        return picture.GetPixel(
            (int) Math.Floor(30 + x / across * 30), (int) Math.Floor(30 - z / across * 30));
    }

    [TestMethod]
    public void TestADecalIsWrittenOnAMaterial()
    {
        // The transforms are split by a property on purpose: the scale must survive the color
        // written after it, and the translation must still be added on top.  Scaled to four units
        // square and moved two to the right, the square runs from 0 to 4 across and -2 to 2 up.
        Canvas picture = FloorPainted("""
            decal {
                path { move to -1, -1  line to 1, -1  line to 1, 1  line to -1, 1  close }
                scale 2
                color Red
                min Y -1  max Y 1
                translate [2, 0, 0]
            }
            """);

        Assert.IsTrue(Colors.Red.Matches(FloorAt(picture, 3.5, 1.5)), "the scale was lost");
        Assert.IsTrue(Colors.Red.Matches(FloorAt(picture, 0.5, -1.5)));
        Assert.IsTrue(Colors.White.Matches(FloorAt(picture, -0.5, 0)), "the translation was lost");
        Assert.IsTrue(Colors.White.Matches(FloorAt(picture, 2, 2.5)));
    }

    [TestMethod]
    public void TestDecalsStackInTheOrderTheyAreWritten()
    {
        Canvas picture = FloorPainted("""
            decal {
                path { move to -1, -1  line to 1, -1  line to 1, 1  line to -1, 1  close }
                color Red  min Y -1  max Y 1  scale 2
            }
            decal {
                path { move to -1, -1  line to 1, -1  line to 1, 1  line to -1, 1  close }
                color Blue  min Y -1  max Y 1  translate [1, 0, 0]
            }
            """);

        Assert.IsTrue(Colors.Red.Matches(FloorAt(picture, -1.5, 0)));
        Assert.IsTrue(Colors.Blue.Matches(FloorAt(picture, 0.5, 0)), "the second decal went under the first");
    }

    [TestMethod]
    public void TestADecalLaidOverAMaterialMadeElsewhereGoesOnTopOfItsOwn()
    {
        // The block after a call of a material primitive changes what the call made, and for decals
        // that means adding to them: the primitive's red square must still be there under the blue.
        Canvas picture = FloorShowing("""
            square = path { move to -1, -1  line to 1, -1  line to 1, 1  line to -1, 1  close }
            primitive Painted() -> material {
                return material {
                    pigment color White  ambient 1  diffuse 0  specular 0
                    decal { path square  color Red  min Y -1  max Y 1  scale 2 }
                }
            }
            plane {
                material Painted() {
                    decal { path square  color Blue  min Y -1  max Y 1  translate [1, 0, 0] }
                }
            }
            """);

        Assert.IsTrue(Colors.Red.Matches(FloorAt(picture, -1.5, 0)), "the primitive's own decal was lost");
        Assert.IsTrue(Colors.Blue.Matches(FloorAt(picture, 0.5, 0)), "the added decal is missing");
    }

    /// <summary>
    /// Renders a shape seen square on from +X, wearing a red decal in the given projection, and hands
    /// back the picture.  The shape is given as far as its open brace and whatever it needs to say
    /// before its material.  The decal is a strip, narrow across and long up, so it shows how far up
    /// the side a projection reaches.  The shape glows its own color, so a pixel is exactly the color
    /// painted there.  The picture is 41 pixels square, so its middle is pixel (20, 20).
    /// </summary>
    private static Canvas SeenFromTheSide(string shape, string projection)
    {
        return FloorShowing($$"""
            camera { location [8, 0, 0]  look at [0, 0, 0]  field of view 20 }
            {{shape}}
                material {
                    pigment color White  ambient 1  diffuse 0  specular 0
                    decal {
                        path { move to -0.2, -3  line to 0.2, -3  line to 0.2, 3  line to -0.2, 3  close }
                        color Red
                        {{projection}}
                    }
                }
            }
            """, 41);
    }

    [TestMethod]
    public void TestEveryProjectionIsWrittenOnAMaterial()
    {
        // Each paints its strip down the middle of the side facing +X.  Well up the side of the
        // cylinder and the sphere, only the right projection still reaches: read as a sphere, the
        // cylinder there is too far from the center, and read as a cylinder, the sphere there is too
        // near the axis.  The planar decal, turned to face +X, lays its strip across instead.
        (string shape, string projection, (int x, int y, Color color)[] expected)[] cases =
        [
            ("cylinder { min Y -2  max Y 2", "cylindrical  min radius 0.9  max radius 1.1",
                [(20, 20, Colors.Red), (20, 6, Colors.Red), (8, 20, Colors.White)]),
            ("sphere {", "spherical  min radius 0.9  max radius 1.1",
                [(20, 20, Colors.Red), (20, 6, Colors.Red), (8, 20, Colors.White)]),
            ("torus { radii 2, 0.5", "toroidal radius 2  min radius 0.4  max radius 0.6",
                [(20, 20, Colors.Red), (8, 20, Colors.White)]),
            ("cube {", "planar  min Y -1.1  max Y -0.9  rotate Z 90",
                [(20, 20, Colors.Red), (6, 20, Colors.Red), (20, 6, Colors.White)])
        ];

        foreach ((string shape, string projection, (int x, int y, Color color)[] expected) in cases)
        {
            Canvas picture = SeenFromTheSide(shape, projection);

            foreach ((int x, int y, Color color) in expected)
            {
                Assert.IsTrue(color.Matches(picture.GetPixel(x, y)),
                    $"{projection}: ({x}, {y}) is {picture.GetPixel(x, y)}, not {color}");
            }
        }
    }

    [TestMethod]
    public void TestABoxsSidesStayCleanUnderPaintFromAbove()
    {
        // The square is wider than the box and the reach takes in the whole of it, so the side the
        // camera looks at lies inside both; only the way it faces keeps the paint off it.
        Canvas picture = FloorShowing("""
            camera { location [8, 0, 0]  look at [0, 0, 0]  field of view 20 }
            cube {
                material {
                    pigment color White  ambient 1  diffuse 0  specular 0
                    decal {
                        path { move to -2, -2  line to 2, -2  line to 2, 2  line to -2, 2  close }
                        color Red
                        planar  min Y -2  max Y 2
                    }
                }
            }
            """, 41);

        Assert.IsTrue(Colors.White.Matches(picture.GetPixel(20, 20)),
            $"the side is {picture.GetPixel(20, 20)}");
    }

    /// <summary>
    /// Renders a ball seen square on from +X, painted from above all over by a planar decal that
    /// fades as given, and hands back the picture.  The middle of the picture, pixel (20, 20), is
    /// where the ball faces 90 degrees from the paint coming down; well up it, at pixel (20, 9), it
    /// faces 46.5 degrees from it.
    /// </summary>
    private static Canvas BallPaintedFromAbove(string fade)
    {
        return FloorShowing($$"""
            camera { location [8, 0, 0]  look at [0, 0, 0]  field of view 20 }
            sphere {
                material {
                    pigment color White  ambient 1  diffuse 0  specular 0
                    decal {
                        path { move to -2, -2  line to 2, -2  line to 2, 2  line to -2, 2  close }
                        color Red
                        planar  min Y -2  max Y 2
                        {{fade}}
                    }
                }
            }
            """, 41);
    }

    [TestMethod]
    public void TestAFadeIsWrittenOnADecal()
    {
        // As it comes: there at 46.5 degrees, gone at 90.
        Canvas plain = BallPaintedFromAbove("");

        Assert.IsTrue(Colors.Red.Matches(plain.GetPixel(20, 9)), $"well up is {plain.GetPixel(20, 9)}");
        Assert.IsTrue(Colors.White.Matches(plain.GetPixel(20, 20)), $"the middle is {plain.GetPixel(20, 20)}");

        // Not fading, it paints the middle too.
        Canvas unfaded = BallPaintedFromAbove("no fade");

        Assert.IsTrue(Colors.Red.Matches(unfaded.GetPixel(20, 20)), $"the middle is {unfaded.GetPixel(20, 20)}");

        // Fading between 10 and 30 degrees, it is gone well up -- and the angles are in the scene's
        // own units, degrees here, and may be given either way round.
        foreach (string fade in new[] { "fade from 10 to 30", "fade from 30 to 10" })
        {
            Canvas moved = BallPaintedFromAbove(fade);

            Assert.IsTrue(Colors.White.Matches(moved.GetPixel(20, 9)), $"{fade}: well up is {moved.GetPixel(20, 9)}");
        }
    }

    [TestMethod]
    public void TestAFadeMustMakeSense()
    {
        string both = ErrorFrom("""
            sphere {
                material {
                    decal {
                        path { move to -1, -1  line to 1, -1  line to 1, 1  close }
                        color Red  min Y -1  max Y 1  no fade  fade from 30 to 45
                    }
                }
            }
            """);

        Assert.IsNotNull(both, "a decal was let both fade and not fade");
        StringAssert.Contains(both, "cannot both fade and not fade");

        string beyond = ErrorFrom("""
            context { angles are degrees }
            sphere {
                material {
                    decal {
                        path { move to -1, -1  line to 1, -1  line to 1, 1  close }
                        color Red  min Y -1  max Y 1  fade from 30 to 120
                    }
                }
            }
            """);

        Assert.IsNotNull(beyond, "a fade past edge on was accepted");
        StringAssert.Contains(beyond, "between 0 and 90 degrees");
    }

    [TestMethod]
    public void TestAReachMustBeSaidTheWayItsProjectionReaches()
    {
        string wrapped = ErrorFrom("""
            sphere {
                material {
                    decal {
                        path { move to -1, -1  line to 1, -1  line to 1, 1  close }
                        color Red  cylindrical  min Y -1  max Y 1
                    }
                }
            }
            """);

        Assert.IsNotNull(wrapped, "a cylindrical decal took a reach along Y");
        StringAssert.Contains(wrapped, "min radius\" and \"max radius");

        string flat = ErrorFrom("""
            sphere {
                material {
                    decal {
                        path { move to -1, -1  line to 1, -1  line to 1, 1  close }
                        color Red  min radius 0.9  max radius 1.1
                    }
                }
            }
            """);

        Assert.IsNotNull(flat, "a planar decal took a reach in radius");
        StringAssert.Contains(flat, "min Y\" and \"max Y");

        string ringless = ErrorFrom("""
            sphere {
                material {
                    decal {
                        path { move to -1, -1  line to 1, -1  line to 1, 1  close }
                        color Red  toroidal  min radius 0.9  max radius 1.1
                    }
                }
            }
            """);

        Assert.IsNotNull(ringless, "a toroidal decal was taken without its ring");
        StringAssert.Contains(ringless, "the radius of the ring");
    }

    [TestMethod]
    public void TestADecalMustSayHowFarItReaches()
    {
        string error = ErrorFrom("""
            plane {
                material {
                    decal {
                        path { move to -1, -1  line to 1, -1  line to 1, 1  close }
                        color Red  min Y -1
                    }
                }
            }
            """);

        Assert.IsNotNull(error, "a decal with only half a reach was accepted");
        StringAssert.Contains(error, "min Y\" and \"max Y");
    }

    [TestMethod]
    public void TestAddingADecalToANamedMaterialLeavesTheNameAlone()
    {
        // Extending a material copies it and parses the addition into the copy.  A list of decals
        // shared between the two would paint the addition on everything else wearing the name too.
        MaterialResolver named = new ();

        named.DecalResolvers.Add(new DecalResolver());

        MaterialResolver extended = (MaterialResolver) named.Clone();

        extended.DecalResolvers.Add(new DecalResolver());

        Assert.HasCount(1, named.DecalResolvers, "the addition was painted on the original too");
        Assert.HasCount(2, extended.DecalResolvers);
    }

    /// <summary>
    /// Parses and renders a tiny scene, and hands back what went wrong, or <c>null</c>.
    /// </summary>
    private static string ErrorFrom(string body)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"decal-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string scene = Path.Combine(directory, "scene.igl");

            File.WriteAllText(scene,
                "camera { location [0, 3, -5]  look at [0, 0, 0] }\n" +
                "point light { location [-4, 6, -6] }\n" + body + "\n");

            StringWriter captured = new ();
            TextWriter was = Console.Out;

            Console.SetOut(captured);

            try
            {
                new LanguageParser(scene).Parse()?.Render(new RenderOptions
                {
                    OutputFileName = Path.ChangeExtension(scene, ".png"), Width = 8, Height = 6
                });
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
            finally
            {
                Console.SetOut(was);
            }

            string said = captured.ToString();

            return said.Contains("Error") ? said.Trim() : null;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
