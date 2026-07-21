using Lex.Clauses;
using Lex.Dsl;
using Lex.Parser;
using Lex.Tokens;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle any import statement we may be looking at.
    /// <para>
    /// An import reads a library file exactly as an include would, and then throws away every
    /// definition in it that was not asked for.  That is what sets it apart: a library may hold a
    /// hundred textures, and a scene that wants two of them should not have to carry the names of
    /// the other ninety-eight.
    /// </para>
    /// </summary>
    private void HandleImports()
    {
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "importClause");

        while (clause != null)
        {
            Token fileToken = clause.Tokens[1];
            string path = ResolveImportPath(fileToken);
            HashSet<string> wanted = clause.Tokens
                .Skip(3)
                .Where(token => token is IdToken or KeywordToken)
                .Select(token => token.Text)
                .ToHashSet();

            ImportFrom(path, wanted, fileToken);

            clause = LanguageDsl.ParseClause(CurrentParser, "importClause");
        }
    }

    /// <summary>
    /// This method works out which file an import names, looking beside the scene first and among
    /// the shipped libraries second.  See <see cref="LibraryLocator"/>.
    /// </summary>
    /// <param name="fileToken">The token naming the file, used to report where a failure was.</param>
    /// <returns>The full path of the file to import from.</returns>
    private string ResolveImportPath(Token fileToken)
    {
        string path = LibraryLocator.Find(fileToken.Text, CurrentDirectory);

        if (path is not null)
            return path;

        // Both places are named, since which one was meant is exactly what the reader needs to know
        // to fix it.
        throw new TokenException(
            $"No library named '{fileToken.Text}' was found, either beside the scene or in " +
            $"{LibraryLocator.LibrariesDirectory}.")
        {
            Token = fileToken
        };
    }

    /// <summary>
    /// This method reads the given library and leaves only the named definitions behind.
    /// <para>
    /// Only the things referred to by name at parse time -- materials, pigments, surfaces and the
    /// like -- are filtered.  Plain values are not, and that is deliberate rather than an
    /// oversight: a material holds a copy of any material it was built from, taken when it was
    /// parsed, so discarding what it was built from cannot hurt it; but it holds only a
    /// *reference* to any value it names, looked up when the image is rendered, so discarding
    /// those would leave it pointing at nothing.  A library's colours and numbers therefore come
    /// along.  They are also far the smaller half of the problem, being a handful where the
    /// textures are a hundred.
    /// </para>
    /// </summary>
    /// <param name="path">The full path of the library to read.</param>
    /// <param name="wanted">The names the scene asked for.</param>
    /// <param name="fileToken">The token naming the file, used to report where a failure was.</param>
    private void ImportFrom(string path, HashSet<string> wanted, Token fileToken)
    {
        // Anything already in scope stays in scope: the library's definitions are what we mean to
        // filter, not the scene's own.
        HashSet<string> before = _context.ExtensibleItems.Keys.ToHashSet();
        HashSet<string> valuesBefore = _context.InstructionContext.VariableNames.ToHashSet();
        int depth = _entries.Count;

        PushEntry(path);

        // Read the library to its end, rather than letting it interleave with the scene the way an
        // include does.  Nothing can be filtered until everything has been seen.
        while (_entries.Count > depth)
        {
            if (CurrentParser.IsAtEnd())
            {
                PopEntry();

                continue;
            }

            HandleIncludes();
            _dispatcher.Dispatch(LanguageDsl.ParseNextClause(CurrentParser));
        }

        List<string> arrived = _context.ExtensibleItems.Keys
            .Where(name => !before.Contains(name))
            .ToList();
        // Values are counted as having arrived too, even though they are not filtered.  Naming one
        // is then simply a way of saying out loud that the scene means to use it, which is what
        // anyone reading a library full of named colours will expect to be able to do.
        HashSet<string> valuesArrived = _context.InstructionContext.VariableNames
            .Where(name => !valuesBefore.Contains(name))
            .ToHashSet();
        List<string> missing = wanted
            .Where(name => !arrived.Contains(name) && !before.Contains(name) &&
                           !valuesArrived.Contains(name))
            .ToList();

        // A name the library does not define is worth complaining about rather than passing over:
        // it is far more likely to be a typo than an intention, and the scene would otherwise fail
        // much later with a far less helpful message.
        if (missing.Count > 0)
        {
            throw new TokenException(
                $"'{Path.GetFileName(path)}' does not define {string.Join(", ", missing.Order())}.")
            {
                Token = fileToken
            };
        }

        foreach (string name in arrived.Where(name => !wanted.Contains(name)))
            _context.ExtensibleItems.Remove(name);
    }
}
