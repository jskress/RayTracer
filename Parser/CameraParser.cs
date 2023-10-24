using RayTracer.Core;

namespace RayTracer.Parser;

/// <summary>
/// This class is used to parse camera information.
/// </summary>
internal class CameraParser : BoundedContentParser
{
    private readonly Camera _camera;

    internal CameraParser(FileContent fileContent, Camera camera) : base(fileContent, '{', '}')
    {
        _camera = camera;
    }

    /// <summary>
    /// This method parses the actual content.
    /// </summary>
    protected override void ParseContent()
    {
        while (true)
        {
            if (IsAtEnd())
                break;

            string word = FileContent.GetNextWord(true);

            switch (word)
            {
                case "location":
                    _camera.Location = FileContent.GetNextPoint();
                    break;
                case "lookAt":
                    _camera.LookAt = FileContent.GetNextPoint();
                    break;
                case "up":
                    _camera.Up = FileContent.GetNextVector(true);
                    break;
                case "fieldOfView":
                    _camera.FieldOfView = FileContent.GetNextDouble();
                    break;
                default:
                    FileParser.ErrorOut($"{word} is not an attribute of a camera.");
                    break;
            }
        }
    }
}
