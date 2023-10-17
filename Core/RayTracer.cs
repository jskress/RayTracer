namespace RayTracer.Core;

/// <summary>
/// This is our main raytracer class.
/// </summary>
public class RayTracer
{
    /// <summary>
    /// This method is used to render the configured image.
    /// </summary>
    public void Render()
    {
        new Frame(Arguments.Instance.OutputFile, 50, 50).Render();
    }
}
