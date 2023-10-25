using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Parser;

/// <summary>
/// This class handles parsing the extra attributes for a group at the surface level.
/// </summary>
internal class GroupAttributeParser : AttributeParser
{
    private readonly Group _group;

    internal GroupAttributeParser(FileContent fileContent, Group group) : base(fileContent)
    {
        _group = group;
    }

    /// <summary>
    /// This method adds the named attribute to our surface.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <returns><c>true</c>, if the name was a supported attribute, or <c>false</c>, if
    /// not.</returns>
    internal override bool TryParseAttributes(string name)
    {
        switch (name)
        {
            case "bbox":
                Point point1 = FileContent.GetNextPoint();
                Point point2 = FileContent.GetNextPoint();
                _group.BoundingBox = new BoundingBox(point1, point2);
                break;
            default:
                return false;
        }

        return true;
    }
}
