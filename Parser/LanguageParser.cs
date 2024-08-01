using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Renderer;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This record defines the entry which holds the parser and file it is working off
    /// of.
    /// </summary>
    /// <param name="FileName">The name of the source file.</param>
    /// <param name="Parser">The parser being used to parse it.</param>
    private record Entry(string FileName, LexicalParser Parser);

    /// <summary>
    /// This is a helper property for accessing the current file name.
    /// </summary>
    private string CurrentFileName => _entries.IsEmpty() ? null : _entries.Peek().FileName;

    /// <summary>
    /// This is a helper property for accessing the current file's directory.
    /// </summary>
    private string CurrentDirectory => Path.GetDirectoryName(CurrentFileName);

    /// <summary>
    /// This is a helper property for accessing the current parser.
    /// </summary>
    private LexicalParser CurrentParser => _entries.IsEmpty() ? null : _entries.Peek().Parser;

    private readonly ParsingContext _context;
    private readonly Stack<Entry> _entries;

    public LanguageParser(string fileName)
    {
        FillDispatchTables();

        _entries = new Stack<Entry>();
        _context = new ParsingContext();

        PushEntry(fileName);
    }

    /// <summary>
    /// This method is used to parse the input file we were created with.
    /// </summary>
    public ImageRenderer Parse()
    {
        try
        {
            while (!IsAtEnd())
            {
                HandleIncludes();
                HandleIncludeEnd();
                ProcessClause(LanguageDsl.ParseNextClause(CurrentParser), string.Empty);
                HandleIncludeEnd();
            }

            return new ImageRenderer(_context.InstructionContext);
        }
        catch (TokenException exception)
        {
            ShowError(exception);

            return null;
        }
    }

    /// <summary>
    /// This method is used to provide error feedback to the user when we run across a
    /// parsing problem.
    /// </summary>
    /// <param name="exception">The token exception we trapped.</param>
    private void ShowError(TokenException exception)
    {
        string fileName = PopEntry();
        int line = exception.Token.Line;
        int column = exception.Token.Column;

        while (_entries.Count > 0)
            PopEntry();

        Console.WriteLine($"Error: {exception.Message}");

        if (line > 0)
        {
            string[] lines = File.ReadAllLines(fileName);

            Console.WriteLine($"{Path.GetFileName(fileName)}: [{line}:{column}] -> {exception.Token}");
            Console.WriteLine(lines[line - 1]);

            if (column > 0)
                Console.WriteLine($"{new string('-', column - 1)}^");
        }
    }

    /// <summary>
    /// This method is used to determine whether we have reached the end of our parsing.
    /// </summary>
    /// <returns><c>true</c>, if we have exhausted all the input source, or <c>false</c>,
    /// if not.</returns>
    private bool IsAtEnd()
    {
        while (_entries.Count > 0 && CurrentParser.IsAtEnd())
            PopEntry();

        return _entries.IsEmpty();
    }

    /// <summary>
    /// This method is used to handle any include statement we may be staring at.
    /// </summary>
    private void HandleIncludes()
    {
        Clause includeClause = LanguageDsl.ParseClause(CurrentParser, "includeClause");

        while (includeClause != null)
        {
            string path = includeClause.Tokens[1].Text;
            
            path = Path.GetFullPath(Path.Combine(CurrentDirectory, path));

            if (!File.Exists(path))
            {
                throw new TokenException($"The file, '{path}', does not exist.")
                {
                    Token = includeClause.Tokens[1]
                };
            }

            PushEntry(path);

            includeClause = LanguageDsl.ParseClause(CurrentParser, "includeClause");
        }
    }

    /// <summary>
    /// This method is used to handle popping our parser stack when we need to.
    /// </summary>
    private void HandleIncludeEnd()
    {
        if (CurrentParser?.IsAtEnd() ?? false)
            PopEntry();
    }

    /// <summary>
    /// This method is used to parse the named clause.  It is assumed that parsing the
    /// clause will result in zero or one clause only.
    /// </summary>
    /// <param name="clauseName">The name of the clause to parse.</param>
    /// <returns></returns>
    private Clause ParseClause(string clauseName)
    {
        HandleIncludes();
        HandleIncludeEnd();

        Clause clause = LanguageDsl.ParseClause(CurrentParser, clauseName);

        HandleIncludeEnd();

        return clause;
    }

    /// <summary>
    /// This is a helper method used to parse the content of a block.  The given name must
    /// be the name of the clause that defines what the block can contain.
    /// </summary>
    /// <param name="blockClauseName">The clause that defines the block's content</param>
    /// <param name="handleClause">An action that handles the clause we parsed.</param>
    private void ParseBlock(string blockClauseName, Action<Clause> handleClause)
    {
        while (!CurrentParser.IsNext(BounderToken.CloseBrace))
        {
            HandleIncludes();
            HandleIncludeEnd();

            if (!CurrentParser.IsNext(BounderToken.CloseBrace))
            {
                Clause clause = LanguageDsl.ParseClause(CurrentParser, blockClauseName);

                handleClause(clause);

                HandleIncludeEnd();
            }
        }

        HandleIncludeEnd();

        // Make sure we eat the closing brace.
        _ = CurrentParser.GetNextToken();

        HandleIncludeEnd();
    }

    /// <summary>
    /// This is a helper method for verifying that a scene element is either inside an
    /// explicit scene definition or there are no explicit scene definitions.
    /// </summary>
    /// <param name="clause">The current clause in play.  We pull the error token from this.</param>
    /// <param name="noun">The noun to use in the error, if we find one.</param>
    private void VerifyDefaultSceneUsage(Clause clause, string noun)
    {
        if (_context.IsEmpty && _context.SeenSceneDefinition)
        {
            throw new TokenException($"{noun} definition found outside a scene definition when explicit scenes are being used.")
            {
                Token = clause?.Tokens.First()
            };
        }
    }

    /// <summary>
    /// This is a helper method for creating an appropriate instruction for setting the
    /// name of a named thing.
    /// </summary>
    /// <param name="term">The term to use to resolve the name.</param>
    /// <returns>The appropriate instruction for setting a thing's name.</returns>
    private static ObjectInstruction<TObject> CreateNamedInstruction<TObject>(Term term)
        where TObject : NamedThing, new()
    {
        return new SetObjectPropertyInstruction<TObject, string>(
            target => target.Name, term);
    }

    /// <summary>
    /// This method is used to push the given file name and a new parser wrapped around it
    /// onto our entry stack.
    /// </summary>
    /// <param name="fileName">The name of the new file to start parsing.</param>
    private void PushEntry(string fileName)
    {
        LexicalParser parser = LanguageDsl.CreateLexicalParser();

        parser.SetSource(File.OpenText(fileName));

        _entries.Push(new Entry(fileName, parser));
    }

    /// <summary>
    /// This method is used to push the given file name and a new parser wrapped around it
    /// onto our entry stack.
    /// </summary>
    /// <returns>The name of the file we just popped.</returns>
    private string PopEntry()
    {
        Entry entry = _entries.Pop();

        // Must do this for proper file closure.
        entry.Parser.Dispose();

        return entry.FileName;
    }

    /// <summary>
    /// This is a helper method for creating a consolidated "command" from tokens.
    /// </summary>
    /// <param name="clause">The clause to pull tokens from.</param>
    /// <returns>The consolidated "command" we found.</returns>
    private static string ToCmd(Clause clause)
    {
        string first = clause.Tokens[0].Text;
        List<string> words = [first];

        if (first is "apply" or "no")
            words.Add(clause.Tokens[1].Text);

        return string.Join('.', words);
    }
}
