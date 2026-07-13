using RayTracer.General;

namespace Tests;

[TestClass]
public class TestProgressBar
{
    [TestMethod]
    public void TestFullLifecycleDoesNotThrow()
    {
        ProgressBar bar = new ();

        bar.SetTotal(10);

        for (int index = 0; index < 10; index++)
            bar.Bump();

        bar.Done();
    }

    [TestMethod]
    public void TestZeroTotalDoneDoesNotThrow()
    {
        // Done() unconditionally sets _current = _total and _used = 50, so it must not
        // divide by the (zero) total the way Bump() would.
        ProgressBar bar = new ();

        bar.SetTotal(0);
        bar.Done();
    }

    [TestMethod]
    public void TestReusingAfterDoneDoesNotThrow()
    {
        ProgressBar bar = new ();

        bar.SetTotal(5);
        bar.Bump();
        bar.Done();

        bar.SetTotal(3);
        bar.Bump();
        bar.Bump();
        bar.Done();
    }
}
