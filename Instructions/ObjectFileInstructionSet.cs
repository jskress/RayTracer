using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions;

public class ObjectFileInstructionSet : SurfaceInstructionSet<Group>
{
    private readonly string _directory;
    private readonly Term _term;

    public ObjectFileInstructionSet(string directory, Term term)
    {
        _directory = directory;
        _term = term;
    }

    /// <summary>
    /// This method may be used by subclasses to create our group.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    protected override void CreateObject(Variables variables)
    {
        string path = _term.GetValue<string>(variables);

        path = Path.GetFullPath(Path.Combine(_directory, path));

        ObjectFileParser objectFileParser = new (fileName: path);

        CreatedObject = objectFileParser.Parse();
    }
    
    /// <summary>
    /// This method creates a copy of this instruction set,
    /// </summary>
    /// <returns>The copy of this instruction set.</returns>
    public override object Copy()
    {
        ObjectFileInstructionSet instructionSet = new (_directory, _term);

        CopyInto(instructionSet);

        return instructionSet;
    }
}
