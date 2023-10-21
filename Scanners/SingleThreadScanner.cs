namespace RayTracer.Scanners;

/// <summary>
/// This class provides a scanner that uses a single thread to visit each pixel.
/// In other words, just a standard <c>for</c> loop.
/// </summary>
public class SingleThreadScanner : IScanner
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
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                function.Invoke(x, y);
        }
    }
}
