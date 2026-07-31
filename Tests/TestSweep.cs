using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestSweep
{
    /// <summary>
    /// For a straight, axis-aligned spline, the rotation-minimizing frame never has any
    /// reason to rotate (see the straight-line case already validated for
    /// <see cref="RotationMinimizingFrame"/>), so lofting a simple square profile along it
    /// must produce vertices at exactly the positions plain "extrude along a straight line"
    /// math would give -- computed independently here, not by re-deriving the
    /// implementation's own frame logic.
    /// </summary>
    [TestMethod]
    public void TestStraightSplineLoftsProfileAtExpectedPositions()
    {
        Sweep sweep = new ()
        {
            Profile = BuildSquareProfile(),
            Spline = new Spline
            {
                Start = new Point(0, 0, 0),
                Segments = { new SplineSegmentSpec { End = new Point(0, 10, 0) } }
            },
            Steps = 4
        };

        sweep.PrepareForRendering();

        List<Point> vertices = new SurfaceIterator(sweep).Surfaces
            .OfType<Triangle>()
            .SelectMany(triangle => new[] { triangle.Point1, triangle.Point2, triangle.Point3 })
            .ToList();

        // With the tangent pointing straight up, our initial-normal logic picks world X as
        // the frame's normal and (by the right-hand rule, tangent x normal) world -Z as its
        // binormal, so a profile point (px, py) lofted at ring height y lands at
        // (px, y, -py).
        Point[] expectedAtY0 =
        [
            new Point(1, 0, -1), new Point(-1, 0, -1), new Point(-1, 0, 1), new Point(1, 0, 1)
        ];
        Point[] expectedAtY10 =
        [
            new Point(1, 10, -1), new Point(-1, 10, -1), new Point(-1, 10, 1), new Point(1, 10, 1)
        ];

        foreach (Point expected in expectedAtY0.Concat(expectedAtY10))
            Assert.IsTrue(vertices.Any(vertex => vertex.Matches(expected)), $"Missing vertex near {expected}");
    }

    /// <summary>
    /// Lofting a profile along a genuinely curved (and non-planar) spline must keep every
    /// profile point the same distance from its own ring's spline point at every ring --
    /// i.e. the rotation-minimizing frame stays rigid (orthonormal) all the way along the
    /// path, rather than shearing or scaling the profile as it turns.
    /// </summary>
    [TestMethod]
    public void TestCurvedSplineKeepsProfileRigidAtEveryRing()
    {
        Sweep sweep = new ()
        {
            Profile = BuildSquareProfile(),
            Spline = new Spline
            {
                Start = new Point(0, 0, 0),
                Segments =
                {
                    new SplineSegmentSpec
                    {
                        Control1 = new Point(2, 3, 1), Control2 = new Point(4, -1, 3),
                        End = new Point(6, 2, 0)
                    }
                }
            },
            Steps = 16
        };
        double expectedDistance = Math.Sqrt(2); // corner (1,1) of the square is sqrt(2) from center.

        sweep.PrepareForRendering();

        List<ISplineCurve> curves = sweep.Spline.GetCurves();
        ISplineCurve curve = curves[0];

        for (int step = 0; step <= sweep.Steps; step++)
        {
            double u = (double) step / sweep.Steps;
            Point ringCenter = curve.GetPoint(u);

            // Reconstruct this ring's actual lofted corner the same way Sweep itself would,
            // then confirm it's still exactly sqrt(2) from the ring's own spline point --
            // this only holds if the frame used to loft it was truly orthonormal.
            List<Point> vertices = new SurfaceIterator(sweep).Surfaces
                .OfType<Triangle>()
                .SelectMany(triangle => new[] { triangle.Point1, triangle.Point2, triangle.Point3 })
                .Where(vertex => (vertex - ringCenter).Magnitude.Near(expectedDistance, 0.01))
                .ToList();

            Assert.IsTrue(vertices.Count > 0, $"No rigid corner found near ring at u={u}");
        }
    }

    /// <summary>
    /// A per-point scale grows (or shrinks) the profile uniformly as it travels the spline,
    /// interpolated linearly between the points that carry it.  For the same straight,
    /// axis-aligned spline as <see cref="TestStraightSplineLoftsProfileAtExpectedPositions"/>
    /// -- normal = world X, binormal = world -Z, so a profile point (px, py) at ring height y
    /// and scale s lands at (px*s, y, -py*s) -- a scale of 1 at the foot growing to 2 at the
    /// top must put the (1, 1) corner at (1, 0, -1) at the start, (2, 10, -2) at the end, and,
    /// halfway up at y = 5 (scale 1.5), at (1.5, 5, -1.5).
    /// </summary>
    [TestMethod]
    public void TestScaleGrowsProfileAlongSpline()
    {
        Sweep sweep = new ()
        {
            Profile = BuildSquareProfile(),
            Spline = new Spline
            {
                Start = new Point(0, 0, 0), // StartScale defaults to 1.
                Segments = { new SplineSegmentSpec { End = new Point(0, 10, 0), Scale = 2 } }
            },
            Steps = 4
        };

        sweep.PrepareForRendering();

        List<Point> vertices = new SurfaceIterator(sweep).Surfaces
            .OfType<Triangle>()
            .SelectMany(triangle => new[] { triangle.Point1, triangle.Point2, triangle.Point3 })
            .ToList();

        Point[] expected =
        [
            new Point(1, 0, -1),      // the (1, 1) corner at the foot, scale 1.
            new Point(1.5, 5, -1.5),  // halfway up, scale interpolated to 1.5.
            new Point(2, 10, -2)      // at the top, scale 2.
        ];

        foreach (Point point in expected)
            Assert.IsTrue(vertices.Any(vertex => vertex.Matches(point)), $"Missing vertex near {point}");
    }

    /// <summary>
    /// The scale is a genuine uniform scale about each ring's own spline point, holding true
    /// on a curved, non-planar spline just as it does on a straight one.  This is the growing
    /// counterpart of <see cref="TestCurvedSplineKeepsProfileRigidAtEveryRing"/>: with a scale
    /// of 1 at the start growing to 2 at the end, every ring's lofted corner must sit exactly
    /// <c>sqrt(2) * (1 + u)</c> from its ring's spline point -- the profile's natural corner
    /// distance times this ring's interpolated scale -- rather than the fixed sqrt(2) it would
    /// keep with no scaling.
    /// </summary>
    [TestMethod]
    public void TestScaleIsUniformAboutRingCenterOnCurvedSpline()
    {
        Sweep sweep = new ()
        {
            Profile = BuildSquareProfile(),
            Spline = new Spline
            {
                Start = new Point(0, 0, 0), // StartScale defaults to 1.
                Segments =
                {
                    new SplineSegmentSpec
                    {
                        Control1 = new Point(2, 3, 1), Control2 = new Point(4, -1, 3),
                        End = new Point(6, 2, 0), Scale = 2
                    }
                }
            },
            Steps = 16
        };
        double cornerDistance = Math.Sqrt(2); // corner (1, 1) of the square is sqrt(2) from center.

        sweep.PrepareForRendering();

        List<ISplineCurve> curves = sweep.Spline.GetCurves();
        ISplineCurve curve = curves[0];

        for (int step = 0; step <= sweep.Steps; step++)
        {
            double u = (double) step / sweep.Steps;
            Point ringCenter = curve.GetPoint(u);
            double expectedDistance = cornerDistance * (1 + u); // scale grows 1 -> 2 across the segment.

            List<Point> vertices = new SurfaceIterator(sweep).Surfaces
                .OfType<Triangle>()
                .SelectMany(triangle => new[] { triangle.Point1, triangle.Point2, triangle.Point3 })
                .Where(vertex => (vertex - ringCenter).Magnitude.Near(expectedDistance, 0.01))
                .ToList();

            Assert.IsTrue(vertices.Count > 0, $"No corner at the scaled distance found near ring at u={u}");
        }
    }

    /// <summary>
    /// A sweep with N spline steps and an M-point (already-sampled) profile must produce
    /// exactly <c>2 * N * (M - 1)</c> lateral triangles -- two triangles per quad band, one
    /// band per pair of consecutive rings, one pair of consecutive profile points per band
    /// -- plus, since our square profile is closed, two capping triangles at each end (a
    /// square triangulates into exactly 2 triangles).
    /// </summary>
    [TestMethod]
    public void TestTriangleCountMatchesRingAndProfileResolution()
    {
        Sweep sweep = new ()
        {
            Profile = BuildSquareProfile(),
            Spline = new Spline
            {
                Start = new Point(0, 0, 0),
                Segments = { new SplineSegmentSpec { End = new Point(0, 10, 0) } }
            },
            Steps = 6,
            ProfileSteps = 1
        };

        sweep.PrepareForRendering();

        int triangleCount = new SurfaceIterator(sweep).Surfaces.OfType<Triangle>().Count();
        int profilePointCount = sweep.Profile.Sample(sweep.ProfileSteps).Count;
        int expectedLateralTriangles = 2 * sweep.Steps * (profilePointCount - 1);
        int expectedCapTriangles = 2 * 2; // a square triangulates into 2 triangles, times 2 caps.

        Assert.AreEqual(expectedLateralTriangles + expectedCapTriangles, triangleCount);
    }

    /// <summary>
    /// An open profile (no "close") has no "inside" to fill, so it must produce only
    /// lateral triangles -- no caps.
    /// </summary>
    [TestMethod]
    public void TestOpenProfileIsNotCapped()
    {
        GeneralPath openProfile = new GeneralPath()
            .MoveTo(1, 1)
            .LineTo(-1, 1)
            .LineTo(-1, -1)
            .LineTo(1, -1); // no ClosePath() call.
        Sweep sweep = new ()
        {
            Profile = openProfile,
            Spline = new Spline
            {
                Start = new Point(0, 0, 0),
                Segments = { new SplineSegmentSpec { End = new Point(0, 10, 0) } }
            },
            Steps = 6,
            ProfileSteps = 1
        };

        sweep.PrepareForRendering();

        int triangleCount = new SurfaceIterator(sweep).Surfaces.OfType<Triangle>().Count();
        int profilePointCount = sweep.Profile.Sample(sweep.ProfileSteps).Count;
        int expectedLateralTriangles = 2 * sweep.Steps * (profilePointCount - 1);

        Assert.AreEqual(expectedLateralTriangles, triangleCount);
    }

    /// <summary>
    /// Setting <see cref="Sweep.Open"/> must suppress capping even though the profile
    /// itself is closed.
    /// </summary>
    [TestMethod]
    public void TestOpenPropertySuppressesCappingOfClosedProfile()
    {
        Sweep sweep = new ()
        {
            Profile = BuildSquareProfile(),
            Spline = new Spline
            {
                Start = new Point(0, 0, 0),
                Segments = { new SplineSegmentSpec { End = new Point(0, 10, 0) } }
            },
            Steps = 6,
            ProfileSteps = 1,
            Open = true
        };

        sweep.PrepareForRendering();

        int triangleCount = new SurfaceIterator(sweep).Surfaces.OfType<Triangle>().Count();
        int profilePointCount = sweep.Profile.Sample(sweep.ProfileSteps).Count;
        int expectedLateralTriangles = 2 * sweep.Steps * (profilePointCount - 1);

        Assert.AreEqual(expectedLateralTriangles, triangleCount);
    }

    /// <summary>
    /// A spline whose two segments meet at a sharp corner (not flowing smoothly into each
    /// other) must fail to prepare, by default, with a clear error -- the same kind of kink,
    /// and the same reasoning, as <see cref="TestTube.TestDiscontinuousTubeThrowsByDefault"/>.
    /// </summary>
    [TestMethod]
    public void TestDiscontinuousSplineThrowsByDefault()
    {
        Sweep sweep = new ()
        {
            Profile = BuildSquareProfile(),
            Spline = new Spline
            {
                Start = new Point(-10, 0, 0),
                Segments =
                {
                    new SplineSegmentSpec { End = new Point(0, 0, 0) },
                    new SplineSegmentSpec { End = new Point(0, 10, 0) }
                }
            }
        };

        Assert.ThrowsExactly<Exception>(sweep.PrepareForRendering);
    }

    /// <summary>
    /// Marking a kinked spline <see cref="Spline.Discontinuous"/> must suppress the
    /// tangent-continuity check, since the kink is intentional there.
    /// </summary>
    [TestMethod]
    public void TestDiscontinuousFlagSuppressesTheCheck()
    {
        Sweep sweep = new ()
        {
            Profile = BuildSquareProfile(),
            Spline = new Spline
            {
                Start = new Point(-10, 0, 0),
                Segments =
                {
                    new SplineSegmentSpec { End = new Point(0, 0, 0) },
                    new SplineSegmentSpec { End = new Point(0, 10, 0) }
                },
                Discontinuous = true
            }
        };

        sweep.PrepareForRendering();
    }

    /// <summary>
    /// By default a sweep recenters its profile about the 2D origin, so the spline threads the
    /// middle of the profile.  A square drawn off in the first quadrant (corners (0,0)..(2,2),
    /// centered on (1,1)) carried up a straight spline on the Y axis must therefore come out
    /// straddling that axis -- the lofted ring's X extent centered on 0, not on the +1 the
    /// profile was drawn around.
    /// </summary>
    [TestMethod]
    public void TestProfileIsCenteredAboutTheSplineByDefault()
    {
        Sweep sweep = new () // Center defaults to true.
        {
            Profile = BuildOffCenterSquareProfile(),
            Spline = new Spline
            {
                Start = new Point(0, 0, 0),
                Segments = { new SplineSegmentSpec { End = new Point(0, 10, 0) } }
            },
            Steps = 4
        };

        sweep.PrepareForRendering();

        List<double> xs = SweepVertices(sweep).Select(vertex => vertex.X).ToList();
        double xCenter = (xs.Min() + xs.Max()) / 2;

        Assert.IsTrue(xCenter.Near(0, 1e-9), $"a centered profile should straddle the spline (xCenter={xCenter})");
    }

    /// <summary>
    /// "no center" keeps the profile's own placement, so the same off-center square is lofted
    /// where it was drawn: its X runs 0..2, straddling +1 rather than the spline on the axis.
    /// This is the difference the flag is there to make.
    /// </summary>
    [TestMethod]
    public void TestNoCenterKeepsTheProfilesOwnPlacement()
    {
        Sweep sweep = new ()
        {
            Profile = BuildOffCenterSquareProfile(),
            Spline = new Spline
            {
                Start = new Point(0, 0, 0),
                Segments = { new SplineSegmentSpec { End = new Point(0, 10, 0) } }
            },
            Steps = 4,
            Center = false
        };

        sweep.PrepareForRendering();

        List<double> xs = SweepVertices(sweep).Select(vertex => vertex.X).ToList();
        double xCenter = (xs.Min() + xs.Max()) / 2;

        Assert.IsTrue(xCenter.Near(1, 1e-9), $"an uncentered profile should keep its own placement (xCenter={xCenter})");
    }

    /// <summary>
    /// Collects every triangle vertex of a prepared sweep.
    /// </summary>
    private static List<Point> SweepVertices(Sweep sweep)
    {
        return new SurfaceIterator(sweep).Surfaces
            .OfType<Triangle>()
            .SelectMany(triangle => new[] { triangle.Point1, triangle.Point2, triangle.Point3 })
            .ToList();
    }

    /// <summary>
    /// Builds a simple closed, axis-aligned square profile, corners at (+-1, +-1).
    /// </summary>
    private static GeneralPath BuildSquareProfile()
    {
        return new GeneralPath()
            .MoveTo(1, 1)
            .LineTo(-1, 1)
            .LineTo(-1, -1)
            .LineTo(1, -1)
            .ClosePath();
    }

    /// <summary>
    /// Builds a closed square drawn off in the first quadrant, corners (0,0)..(2,2), so its
    /// center sits at (1, 1) rather than the origin -- the case centering exists to handle.
    /// </summary>
    private static GeneralPath BuildOffCenterSquareProfile()
    {
        return new GeneralPath()
            .MoveTo(0, 0)
            .LineTo(2, 0)
            .LineTo(2, 2)
            .LineTo(0, 2)
            .ClosePath();
    }
}
