using System.Text;
using Lex;
using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Renderer;

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
    /// <para>
    /// The stack empties the moment the last file runs out of tokens, which happens while a clause at
    /// the very end of a file is still being handled, so the file we have just finished stands in for
    /// the file we are in.  Without that, anything asked of the file after its last token -- what
    /// directory to resolve an image name against, most of all -- came back as nothing.
    /// </para>
    /// </summary>
    private string CurrentFileName => _entries.IsEmpty() ? _lastFileName : _entries.Peek().FileName;

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
    private readonly FailureTracker _failureTracker;
    private readonly ClauseDispatcher _dispatcher;

    private string _lastFileName;

    public LanguageParser(string fileName)
    {
        _dispatcher = CreateDispatcher();
        _entries = new Stack<Entry>();
        _context = new ParsingContext();
        _failureTracker = new FailureTracker();
        _lastFileName = fileName;

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
                HandleImports();
                HandleIncludeEnd();
                _dispatcher.Dispatch(LanguageDsl.ParseNextClause(CurrentParser));
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
        string fileName = _entries.IsEmpty() ? _lastFileName : PopEntry();
        int line = exception.Line;
        int column = exception.Column;

        while (_entries.Count > 0)
            PopEntry();

        Console.WriteLine($"Error: {exception.Message}");

        if (line > 0)
        {
            string source = File.ReadAllLines(fileName)[line - 1]
                .Replace("\t", "    ");
            string location = exception.Token != null
                ? $"{Path.GetFileName(fileName)}: [{line}:{column}] -> {exception.Token}"
                : $"{Path.GetFileName(fileName)}: [{line}:{column}]";

            Console.WriteLine(location);
            Console.WriteLine(source);

            if (column > 0)
                Console.WriteLine($"{new string('-', column - 1)}^");
        }
    }

    /// <summary>
    /// This is a helper method for raising a good error for unexpected input: one built
    /// from whatever our failure tracker recorded as the furthest, most specific match
    /// attempts, falling back to a generic message pointing at the next token if the
    /// tracker has nothing recorded.
    /// </summary>
    /// <param name="fallbackMessage">The message to use if the failure tracker has nothing
    /// recorded.</param>
    /// <returns>The exception to throw; this method never returns normally.</returns>
    private TokenException CreateUnexpectedInputException(string fallbackMessage)
    {
        string message = _failureTracker.BuildMessage();

        if (message == null)
        {
            return new TokenException(fallbackMessage)
            {
                Token = CurrentParser.GetNextToken()
            };
        }

        return new TokenException(message)
        {
            Line = _failureTracker.Line,
            Column = _failureTracker.Column
        };
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
    /// clause will result in only one clause or no clause at all.
    /// </summary>
    /// <param name="clauseName">The name of the clause to parse.</param>
    /// <returns></returns>
    private Clause ParseClause(string clauseName)
    {
        // Nothing is left to read the clause out of, which is an answer rather than a fault: asking
        // for a clause is always allowed to come back with none.  It matters for the clauses that are
        // asked for on the chance of their being there, since the thing they are optional to may
        // perfectly well be the last thing in the file -- and the file is let go of the moment its
        // last clause has been read, before whatever read it has finished asking its questions.
        if (CurrentParser is null)
            return null;

        HandleIncludes();
        HandleIncludeEnd();

        if (CurrentParser is null)
            return null;

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
    /// <returns>The token for the closing brace that ended the block.</returns>
    private Token ParseBlock(string blockClauseName, Action<Clause> handleClause)
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
        Token brace = CurrentParser.GetNextToken();

        HandleIncludeEnd();

        return brace;
    }

    /// <summary>
    /// This is a helper method for verifying that a scene element is either inside an
    /// explicit scene definition or there are no explicit scene definitions.
    /// </summary>
    /// <param name="clause">The current clause in play.  We pull the error token from this.</param>
    /// <param name="noun">The noun to use in the error if we find one.</param>
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
    /// This is a helper method for parsing an object block.
    /// </summary>
    /// <param name="blockName">The name of the block that describes the block's entries.</param>
    /// <param name="handleClause">The action that will be used to handle the block's entry
    /// clauses.</param>
    /// <param name="resolver">The resolver to use if we don't need a new one.</param>
    /// <returns>The created resolver.</returns>
    private TResolver ParseObjectResolver<TResolver>(
        string blockName, Action<Clause> handleClause, TResolver resolver = null,
        bool validate = true)
        where TResolver : class, IObjectResolver, new()
    {
        resolver ??= new TResolver();

        _context.PushTarget(resolver);

        Token token = ParseBlock(blockName, handleClause);

        _context.PopTarget();

        // Not everything read this way is describing a whole thing.  The block after a call of a
        // scene's own primitive is laid *over* something already made, so it holds only what that
        // call wished to change -- and asking it for the properties a whole one must have would
        // refuse every such block that did not repeat them.  What must be there was seen to when the
        // primitive itself was read.
        if (validate && resolver is IValidatable validatable)
        {
            string errorMessage = validatable.Validate();

            if (errorMessage != null)
            {
                throw new TokenException(errorMessage)
                {
                    Token = token
                };
            }
        }

        return resolver;
    }

    /// <summary>
    /// This method is used to push the given file name and a new parser wrapped around it
    /// onto our entry stack.
    /// </summary>
    /// <param name="fileName">The name of the new file to start parsing.</param>
    private void PushEntry(string fileName)
    {
        LexicalParser parser = LanguageDsl.CreateLexicalParser();
        string text = File.ReadAllText(fileName, Encoding.UTF8)
            .Replace("\t", "    ");

        parser.SetSource(text.AsReader());
        parser.FailureTracker = _failureTracker;

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

        return _lastFileName = entry.FileName;
    }

    /// <summary>
    /// This is a helper method for creating a consolidated "command" from tokens.
    /// </summary>
    /// <param name="clause">The clause to pull tokens from.</param>
    /// <returns>The consolidated "command" we found.</returns>
    private static string ToCmd(Clause clause)
    {
        string first = clause.Text();
        List<string> words = [first];

        if (first is "apply" or "no" or "bounded" or "with")
            words.Add(clause.Text(1));

        return string.Join('.', words);
    }
}
