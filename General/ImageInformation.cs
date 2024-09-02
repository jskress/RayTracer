using System.Diagnostics.CodeAnalysis;

namespace RayTracer.General;

/// <summary>
/// This is a simple class to encapsulate basic information about a generated image such
/// as title, author and things like that.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class ImageInformation
{
    public string Title { get; set; }
    public string Author { get; set; }
    public string Description { get; set; }
    public string Copyright { get; set; }
    public DateTime CreationTime { get; } = DateTime.UtcNow;
    public string Software { get; set; }
    public string Disclaimer { get; set; }
    public string Warning { get; set; }
    public string Source { get; set; }
    public string Comment { get; set; }
}
