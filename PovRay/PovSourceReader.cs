using Lex.Tokens;

namespace RayTracer.PovRay;

/// <summary>
/// This class reads a set of POV-Ray include files, following the includes between them.
/// <para>
/// Which files were asked for matters as much as what is in them.  One that was asked for becomes
/// a library of its own, and another file that includes it is left leaning on it by name rather
/// than carrying a copy of it; <c>metals.inc</c> includes <c>golds.inc</c>, and both are worth
/// having separately.  One that was *not* asked for has its declarations folded into whichever
/// file included it, since a scene would otherwise have no way to name them.
/// </para>
/// </summary>
public class PovSourceReader
{
    private readonly string _directory;
    private readonly List<string> _prelude;
    private readonly HashSet<string> _preludeLookup;
    private readonly List<string> _wanted;
    private readonly HashSet<string> _wantedLookup;
    private readonly Dictionary<string, PovSymbol> _symbols = new();
    private readonly Dictionary<string, PovMacro> _macros = new();
    private readonly Dictionary<string, PovFile> _files = new();
    private readonly HashSet<string> _reading = [];
    private readonly List<PovIssue> _issues = [];

    /// <summary>
    /// This property provides everything we could not bring across, in the order we met it.
    /// </summary>
    public IReadOnlyList<PovIssue> Issues => _issues;

    /// <param name="directory">The directory the include files live in.</param>
    /// <param name="prelude">Files to read for their names alone, before the rest.
    /// <para>
    /// <c>colors.inc</c> is one, and is the reason this exists.  The stone files use <c>White</c>
    /// and <c>Mica</c> without including it, on the understanding that a scene will have included
    /// it first, so its names have to be in scope or a quarter of stones1.inc will not read.  But
    /// it must not become a library: the ray tracer already knows all but a handful of those
    /// colors by the same names, and a color resolves to its value as it is read, so a <c>White</c>
    /// in a stone texture arrives in the output as the number it stands for and needs nothing to
    /// have been imported at all.
    /// </para></param>
    /// <param name="wanted">The files that are to become libraries of their own, in the order they
    /// should be read.</param>
    public PovSourceReader(
        string directory, IEnumerable<string> prelude, IEnumerable<string> wanted)
    {
        _directory = directory;
        _prelude = prelude.ToList();
        _preludeLookup = _prelude.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _wanted = wanted.ToList();
        _wantedLookup = _wanted.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// This method reads the prelude and then every file that was asked for, in the order given.
    /// Only the latter come back, the prelude having been read for its names alone.
    /// </summary>
    /// <returns>The files, read.</returns>
    public List<PovFile> ReadAll()
    {
        foreach (string name in _prelude)
            Read(name);

        return _wanted.Select(Read).Where(file => file is not null).ToList();
    }

    /// <summary>
    /// This method reads one file, or gives back what was read the last time it was asked for,
    /// since several files may include the same one and it must be read only once.
    /// </summary>
    /// <param name="name">The file's name, without its directory.</param>
    /// <returns>The file, read, or <c>null</c> if it could not be.</returns>
    public PovFile Read(string name)
    {
        if (_files.TryGetValue(name, out PovFile existing))
            return existing;

        // A file that includes itself, directly or by way of another, would otherwise read
        // forever.  The guards in the library files make this unlikely, but they are guards
        // against exactly this, so they are worth not relying on.
        if (!_reading.Add(name))
            return null;

        try
        {
            string path = Path.Combine(_directory, name);

            if (!File.Exists(path))
            {
                _issues.Add(new PovIssue
                {
                    SourceFile = name, Line = 0,
                    Reason = $"The file, '{path}', does not exist."
                });

                return null;
            }

            List<Token> tokens = PovTokens.Tokenize(File.ReadAllText(path));
            PovParser parser = new PovParser(
                name, _wantedLookup.Contains(name), _preludeLookup.Contains(name), tokens,
                _symbols, _macros, _issues, Read);
            PovFile file = parser.Parse();

            _files[name] = file;

            return file;
        }
        finally
        {
            _reading.Remove(name);
        }
    }
}
