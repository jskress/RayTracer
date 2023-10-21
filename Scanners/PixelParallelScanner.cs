namespace RayTracer.Scanners;

/// <summary>
/// This class provides a scanner that accesses each pixel in the image in parallel.
/// </summary>
public class PixelParallelScanner : IScanner
{
    /// <summary>
    /// This method handles iterating over all possible combinations of x, from 0
    /// to <c>width</c>, and y, from 0 to <c>height</c>, invoking the given action
    /// for each.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="function">The function to apply to each pixel.</param>
    public void Scan(int width, int height, Action<int, int> function)
    {
        int pixels = width * height;

        Parallel.For(0, pixels, new ParallelOptions(), pixel =>
        {
            int y = pixel / width;
            int x = pixel - width * y;

            function.Invoke(x, y);
        });
    }
}
