namespace RayTracer.Scanners;

/// <summary>
/// This class provides a scanner that accesses each line in the image in parallel.
/// </summary>
public class LineParallelScanner : IScanner
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
        if (height > width)
        {
            Parallel.For(0, height, new ParallelOptions(), y =>
            {
                for (int x = 0; x < width; x++)
                    function.Invoke(x, y);
            });
        }
        else
        {
            Parallel.For(0, width, new ParallelOptions(), x =>
            {
                for (int y = 0; y < height; y++)
                    function.Invoke(x, y);
            });
        }
    }
}
