namespace RayTracer.Scanners;

/// <summary>
/// This interface defines a means by which we can scan over all pixels for an
/// image and apply a function to each one.
/// </summary>
public interface IScanner
{
    /// <summary>
    /// This method should handle iterating over all possible combinations of x,
    /// from 0 to <c>width</c>, and y, from 0 to <c>height</c>, invoking the given
    /// action for each.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="function">The function to apply to each pixel.</param>
    void Scan(int width, int height, Action<int, int> function);
}
