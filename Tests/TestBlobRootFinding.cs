using MathNet.Numerics;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestBlobRootFinding
{
    /// <summary>
    /// This field holds a field polynomial, captured from a real render, that the exact solver
    /// cannot solve.  Its six roots are three conjugate pairs, and two of those pairs agree in
    /// their imaginary part to thirteen digits -- the near-degenerate cluster that stalls a
    /// companion-matrix iteration.  There is nothing wrong with the polynomial itself: it is
    /// well scaled, and its leading coefficient is a healthy three quarters of its largest.
    /// </summary>
    private static readonly double[] WillNotSolve =
    [
        -0.2539540032097464, 0.010329776839574478, -0.31774297880057034, -0.04761707323180531,
        0.7326445096441603, 0.054919378357851656, -0.5644739300537651
    ];

    [TestMethod]
    public void TestTheExactSolverReallyDoesFailOnThis()
    {
        // The premise of the fallback, asserted rather than assumed.  If a later version of the
        // solver copes with this, that is good news and this test is how we would hear about it.
        Assert.ThrowsExactly<NonConvergenceException>(() => new Polynomial(WillNotSolve).Roots());
    }

    [TestMethod]
    public void TestARayIsStillAnsweredWhenTheExactSolverGivesUp()
    {
        // The bug this guards: the exception came out through the scanner and took the whole
        // render with it, on two pixels out of 235,200.
        double[] roots = Blob.RealRootsOf(WillNotSolve, -2, 2).ToArray();

        // This one has no real roots at all, so the honest answer is that the ray misses -- which
        // is exactly the answer that used to cost us the picture.
        Assert.AreEqual(0, roots.Length);
    }

    [TestMethod]
    public void TestTheFallbackFindsRootsTheExactSolverWouldHave()
    {
        // (t + 1)(t - 0.5)(t - 1.5), which multiplies out to t^3 - t^2 - 1.25t + 0.75, spelled out
        // as coefficients so that the roots are known independently of anything under test.
        double[] cubic = [0.75, -1.25, -1.0, 1.0];
        double[] roots = Blob.RealRootsOf(cubic, -2, 2).Order().ToArray();

        Assert.AreEqual(3, roots.Length);
        Assert.AreEqual(-1.0, roots[0], 1e-9);
        Assert.AreEqual(0.5, roots[1], 1e-9);
        Assert.AreEqual(1.5, roots[2], 1e-9);
    }
}
