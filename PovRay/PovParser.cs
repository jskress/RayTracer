using Lex.Tokens;

namespace RayTracer.PovRay;

/// <summary>
/// This class reads a POV-Ray include file into declarations the emitter can work from.
/// <para>
/// It reads a good deal less than the whole of POV-Ray's language, and does so on purpose.  The
/// library files we care about are almost nothing but declarations, so what is needed is the
/// arithmetic, the block forms, and enough of the directives to get past the version guards.
/// Anything else is noted as a declaration we could not convert and stepped over, which is what
/// keeps one macro in one file from costing us the other ninety declarations in it.
/// </para>
/// </summary>
public partial class PovParser
{
    /// <summary>
    /// These are the directives that open something a matching <c>#end</c> closes.  They are
    /// counted when we are skipping over a block we do not want, so that an inner one does not
    /// look like the end of the outer.
    /// </summary>
    private static readonly HashSet<string> OpeningDirectives =
    [
        "if", "ifdef", "ifndef", "macro", "while", "for", "switch"
    ];

    private readonly string _fileName;
    private readonly bool _isSeparateLibrary;
    private readonly bool _isPrelude;
    private readonly List<Token> _tokens;
    private readonly Dictionary<string, PovSymbol> _symbols;
    private readonly Dictionary<string, PovMacro> _macros;
    private readonly List<PovIssue> _issues;
    private readonly Func<string, PovFile> _includeReader;
    private readonly List<PovDeclaration> _declarations = [];
    private readonly List<string> _includes = [];

    private int _index;

    /// <summary>
    /// This property notes whether we have read everything there is.
    /// </summary>
    private bool AtEnd => _index >= _tokens.Count;

    /// <summary>
    /// This property provides the token we are looking at, or <c>null</c> at the end of the file.
    /// </summary>
    private Token Current => _index < _tokens.Count ? _tokens[_index] : null;

    /// <summary>
    /// This property provides the token after the one we are looking at, or <c>null</c> when there
    /// is none.
    /// </summary>
    private Token Next => _index + 1 < _tokens.Count ? _tokens[_index + 1] : null;

    /// <summary>
    /// This property provides the line we are on, for reporting.
    /// </summary>
    private int CurrentLine => Current?.Line ?? _tokens.LastOrDefault()?.Line ?? 0;

    /// <summary>
    /// This property notes whether a directive starts where we are now.  A directive is written as
    /// the <c>#</c> operator and a name, which POV-Ray allows to be separated by white space, so
    /// the two arrive as two tokens and are treated as one thing here.
    /// </summary>
    private bool AtDirective => Current.IsPunctuation("#") && Next is IdToken;

    /// <summary>
    /// This property provides the name of the directive we are looking at.
    /// </summary>
    private string DirectiveName => Next.Text;

    /// <param name="fileName">The name of the file being read, for reporting.</param>
    /// <param name="isSeparateLibrary">Whether this file becomes a library of its own.</param>
    /// <param name="isPrelude">Whether this file is being read for its names alone.</param>
    /// <param name="tokens">The tokens the file is made of.</param>
    /// <param name="symbols">The names in scope, shared with every file being read, since an
    /// include's declarations are visible to whatever included it.</param>
    /// <param name="macros">The macros in scope, shared the same way the names are.</param>
    /// <param name="issues">Where to note anything we could not convert.</param>
    /// <param name="includeReader">How to read a file this one includes.</param>
    public PovParser(
        string fileName, bool isSeparateLibrary, bool isPrelude, List<Token> tokens,
        Dictionary<string, PovSymbol> symbols, Dictionary<string, PovMacro> macros,
        List<PovIssue> issues, Func<string, PovFile> includeReader)
    {
        _fileName = fileName;
        _isSeparateLibrary = isSeparateLibrary;
        _isPrelude = isPrelude;
        _tokens = tokens;
        _symbols = symbols;
        _macros = macros;
        _issues = issues;
        _includeReader = includeReader;
    }

    /// <summary>
    /// This method reads the whole file.
    /// </summary>
    /// <returns>What the file declares.</returns>
    public PovFile Parse()
    {
        while (!AtEnd)
            ParseTopLevel();

        return new PovFile
        {
            Name = _fileName,
            IsSeparateLibrary = _isSeparateLibrary,
            IsPrelude = _isPrelude,
            Includes = _includes,
            Declarations = _declarations
        };
    }

    /// <summary>
    /// This method reads one thing at the top level of the file, stepping over it and noting why
    /// when it turns out to be something we cannot handle.
    /// </summary>
    private void ParseTopLevel()
    {
        if (!AtDirective)
        {
            // Anything outside a directive at the top level of a library file is a scene's worth
            // of geometry, which is not what we are here for.
            Report(null, CurrentLine, $"{Current.Describe()} was found outside any declaration.");
            SkipToNextDirective();

            return;
        }

        try
        {
            ParseDirective();
        }
        catch (PovParseException exception)
        {
            Report(null, exception.Line, exception.Message);
            SkipToNextDirective();
        }
    }

    /// <summary>
    /// This method reads one directive.
    /// </summary>
    private void ParseDirective()
    {
        string name = DirectiveName;
        int line = CurrentLine;

        switch (name)
        {
            case "declare":
            case "local":
                ParseDeclare();

                break;

            case "include":
                ParseInclude();

                break;

            case "ifdef":
            case "ifndef":
                ParseIfDefined(name == "ifndef");

                break;

            case "if":
                ParseIf();

                break;

            case "else":
            case "elseif":
                // Reaching one of these means the branch before it was taken, so everything from
                // here to the matching end belongs to a branch that was not.
                _index += 2;

                SkipToEnd(stopAtElse: false);

                break;

            case "end":
                _index += 2;

                break;

            case "undef":
                _index += 2;

                if (Current is IdToken)
                    _symbols.Remove(_tokens[_index++].Text);

                break;

            case "version":
                _index += 2;

                SkipToSemicolon();

                break;

            // These all say something to whoever is watching the render, or move a file about,
            // and none of them declare anything.  What follows runs to the end of the line.
            case "debug":
            case "error":
            case "warning":
            case "render":
            case "fclose":
            case "fopen":
            case "read":
            case "write":
                _index += 2;

                SkipRestOfLine();

                break;

            case "macro":
                ParseMacro();

                break;

            case "while":
            case "for":
            case "switch":
                ParseSkippedBlock($"a \"#{name}\" loop");

                break;

            case "default":
                _index += 2;

                SkipBracedBlock();

                break;

            default:
                Report(null, line, $"We do not handle the directive \"#{name}\".");

                _index += 2;

                SkipToNextDirective();

                break;
        }
    }

    /// <summary>
    /// This method reads a declaration, noting its value under its name so that later declarations
    /// may lean on it.
    /// </summary>
    private void ParseDeclare()
    {
        int line = Current.Line;

        _index += 2;

        // POV-Ray lets a declaration be marked as one that should no longer be used, which
        // glass_old.inc does to every texture in it.  The mark says nothing about the value, so it
        // is stepped over rather than acted on.
        while (Current.IsIdentifier("deprecated") || Current.IsIdentifier("once") ||
               Current is StringToken)
            _index++;

        if (Current is not IdToken)
            throw new PovParseException($"Expected a name to declare but found {Current}.", line);

        string name = Current.Text;

        _index++;

        // An array or a function is declared with something other than a plain equals sign after
        // the name, and neither is anything we could emit.
        if (!Current.IsPunctuation("="))
            throw new PovParseException($"\"{name}\" is declared in a way we do not handle.", Current.Line);

        _index++;

        PovValue value;

        try
        {
            value = ParseValue();
        }
        catch (PovParseException exception)
        {
            Report(name, exception.Line, exception.Message);
            SkipToNextDirective();

            return;
        }

        if (Current.IsPunctuation(";"))
            _index++;

        _symbols[name] = new PovSymbol
        {
            Value = value, SourceFile = _fileName, FromSeparateLibrary = _isSeparateLibrary
        };

        _declarations.Add(new PovDeclaration
        {
            Name = name, Value = value, SourceFile = _fileName, Line = line
        });
    }

    /// <summary>
    /// This method reads a value: either a run of blocks, which is how a layered texture is
    /// written, or an expression.
    /// </summary>
    /// <returns>The value that was read.</returns>
    private PovValue ParseValue()
    {
        if (!StartsBlock())
            return ParseExpression();

        PovBlock first = ParseBlock();

        // A texture written twice over means the second lies on top of the first.  Only textures
        // layer this way, so only a run of textures is gathered up.
        if (first.Kind != "texture" || !StartsBlock() || Current.Text != "texture")
            return first;

        List<PovBlock> layers = [first];

        while (StartsBlock() && Current.Text == "texture")
            layers.Add(ParseBlock());

        return new PovBlockSequence { Blocks = layers };
    }

    /// <summary>
    /// This method notes whether a block starts where we are now, which it does when a name is
    /// followed by an opening brace.
    /// </summary>
    /// <returns><c>true</c> if a block starts here.</returns>
    private bool StartsBlock() =>
        Current is IdToken && Next.IsPunctuation("{");

    /// <summary>
    /// This method reads a braced block and everything in it.
    /// </summary>
    /// <returns>The block that was read.</returns>
    private PovBlock ParseBlock()
    {
        string kind = Current.Text;
        int line = Current.Line;

        _index += 2;

        List<IPovItem> items = [];

        while (!Current.IsPunctuation("}"))
        {
            if (AtEnd)
                throw new PovParseException($"The \"{kind}\" block was never closed.", line);

            // A directive may turn up inside a block as readily as outside one, and textures.inc
            // uses that to hold a property back from an older version of POV-Ray:
            //
            //     finish { specular 1 #if (version < 3.1) ior 1.5 #end }
            //
            // It says nothing about the block itself, so it is dealt with where it stands and the
            // block goes on with whichever branch was taken.
            if (AtDirective)
            {
                ParseDirective();

                continue;
            }

            items.Add(ParseBlockItem());
        }

        _index++;

        return new PovBlock { Kind = kind, Items = items, Line = line };
    }

    /// <summary>
    /// This method reads one thing out of a block: a nested block, a map entry, a named property,
    /// or a bare value.
    /// </summary>
    /// <returns>The item that was read.</returns>
    private IPovItem ParseBlockItem()
    {
        Token token = Current;

        if (token.IsPunctuation("["))
            return ParseMapEntry();

        if (token.IsPunctuation(","))
        {
            // Commas between a block's items carry no meaning in POV-Ray; they turn up inside maps
            // and transform lists purely as punctuation.
            _index++;

            return ParseBlockItem();
        }

        if (StartsBlock())
            return ParseBlock();

        if (token is not IdToken)
            return ParseExpression();

        // A name that already stands for something, or one of the language's own value keywords,
        // is a value in its own right rather than a property waiting to be given one.
        if (StartsExpression(token))
            return ParseExpression();

        _index++;

        List<PovValue> values = [];

        while (StartsExpression(Current))
            values.Add(ParseExpression());

        return new PovProperty { Name = token.Text, Values = values, Line = token.Line };
    }

    /// <summary>
    /// This method reads one entry of a map.
    /// <para>
    /// POV-Ray writes these two ways, one giving a single place and a single value and the older
    /// one giving a band and the value at each end of it, and the library files use both.  Rather
    /// than guess which is which, everything in the brackets is read and then split: the numbers
    /// in front are the places and whatever follows them are the values.
    /// </para>
    /// </summary>
    /// <returns>The map entry that was read.</returns>
    private PovMapEntry ParseMapEntry()
    {
        int line = Current.Line;

        _index++;

        List<PovValue> parts = [];

        while (!Current.IsPunctuation("]"))
        {
            if (AtEnd)
                throw new PovParseException("A map entry was never closed.", line);

            if (Current.IsPunctuation(","))
            {
                _index++;

                continue;
            }

            parts.Add(ParseBlockItem() switch
            {
                PovValue value => value,
                PovProperty property => throw new PovParseException(
                    $"A map entry holds \"{property.Name}\", which we cannot make a value of.", line),
                _ => throw new PovParseException("A map entry holds something we cannot make a value of.", line)
            });
        }

        _index++;

        int stopCount = parts.TakeWhile(part => part is PovNumber).Count();

        // Every value in the entry would count as a place if none of them were colors, which
        // happens for a map of plain numbers.  The last one is the value in that case.
        if (stopCount == parts.Count && stopCount > 1)
            stopCount--;

        return new PovMapEntry
        {
            Stops = parts.Take(stopCount).Cast<PovNumber>().Select(number => number.Value).ToList(),
            Values = parts.Skip(stopCount).ToList(),
            Line = line
        };
    }

    /// <summary>
    /// This method notes whether the given token could start an expression.  It is what tells a
    /// property's value apart from the property after it, since POV-Ray writes both as bare names
    /// with nothing between them.
    /// </summary>
    /// <param name="token">The token to consider.</param>
    /// <returns><c>true</c> if an expression could start there.</returns>
    private bool StartsExpression(Token token)
    {
        if (token is NumberToken)
            return true;

        if (token is OperatorToken or BounderToken)
            return token.Text is "<" or "(" or "-" or "+";

        if (token is not IdToken)
            return false;

        // A name followed by a brace opens a block, which is an item of its own rather than a
        // value we could work out here.
        if (Next.IsPunctuation("{"))
            return false;

        return token.Text is "color" or "colour" or "version" ||
               ColorKeywords.ContainsKey(token.Text) ||
               AxisConstants.ContainsKey(token.Text) ||
               (FunctionNames.Contains(token.Text) && Next.IsPunctuation("(")) ||
               _symbols.ContainsKey(token.Text);
    }

    /// <summary>
    /// This method reads an include, either noting it as a library this one leans on or folding
    /// its declarations into this file's own.
    /// </summary>
    private void ParseInclude()
    {
        int line = Current.Line;

        _index += 2;

        if (Current is not StringToken)
            throw new PovParseException($"Expected a file name to include but found {Current}.", line);

        string name = Current.Text;

        _index++;

        PovFile included = _includeReader(name);

        if (included is null)
        {
            Report(null, line, $"The included file, '{name}', could not be read.");

            return;
        }

        // A prelude was read for its names alone, and they are already in scope, so there is
        // nothing to record and nothing to carry.
        if (included.IsPrelude)
            return;

        if (included.IsSeparateLibrary)
        {
            if (!_includes.Contains(included.Name))
                _includes.Add(included.Name);

            return;
        }

        // A file that is not becoming a library of its own has nothing to import from, so what it
        // declares is taken as though it had been written here.  Anything it leaned on comes along
        // with it.
        foreach (string dependency in included.Includes.Where(dependency => !_includes.Contains(dependency)))
            _includes.Add(dependency);

        _declarations.AddRange(included.Declarations);
    }

    /// <summary>
    /// This method notes that this file leans on the library a name came from, so that the library
    /// we generate can say so.  A name from this same file, or from one whose declarations we are
    /// carrying ourselves, brings no dependency with it.
    /// </summary>
    /// <param name="symbol">The name that was used.</param>
    private void NoteDependencyOn(PovSymbol symbol)
    {
        if (!symbol.FromSeparateLibrary || symbol.SourceFile == _fileName ||
            _includes.Contains(symbol.SourceFile))
            return;

        _includes.Add(symbol.SourceFile);
    }

    /// <summary>
    /// This method reads a conditional that turns on whether a name has been declared, which is
    /// how every one of the library files guards itself against being read twice.
    /// </summary>
    /// <param name="wantUndefined">Whether the body is wanted when the name is *not* declared.</param>
    private void ParseIfDefined(bool wantUndefined)
    {
        int line = Current.Line;

        _index += 2;

        Expect("(");

        if (Current is not IdToken)
            throw new PovParseException($"Expected a name to test but found {Current}.", line);

        bool defined = _symbols.ContainsKey(Current.Text);

        _index++;

        Expect(")");

        if (defined != wantUndefined)
            return;

        SkipToEnd(stopAtElse: true);
    }

    /// <summary>
    /// This method reads a conditional that turns on a number.
    /// </summary>
    private void ParseIf()
    {
        int line = Current.Line;

        _index += 2;

        Expect("(");

        PovValue condition = ParseCondition();

        Expect(")");

        if (condition is not PovNumber number)
            throw new PovParseException("A condition worked out to something that is not a number.", line);

        if (number.Value != 0)
            return;

        SkipToEnd(stopAtElse: true);
    }

    /// <summary>
    /// This method reads a macro definition.
    /// <para>
    /// Only the ones that stand for a single expression are kept, since those are POV-Ray's way of
    /// writing a named function and are worth having.  A macro that does anything more -- declares
    /// things, loops, writes out an object -- is noted as one we cannot handle, exactly as before.
    /// Which sort it is cannot be told from the definition alone, so the body is kept and the
    /// question is settled where it is called: if it does not read as an expression there, the
    /// declaration doing the calling is the one that fails.
    /// </para>
    /// </summary>
    private void ParseMacro()
    {
        int line = CurrentLine;

        _index += 2;

        if (Current is not IdToken)
        {
            SkipToEnd(stopAtElse: false);
            Report(null, line, "A macro was declared without a name.");

            return;
        }

        string name = Current.Text;
        List<string> parameters = [];

        _index++;

        if (Current.IsPunctuation("("))
        {
            _index++;

            while (!Current.IsPunctuation(")") && !AtEnd)
            {
                if (Current is IdToken)
                    parameters.Add(Current.Text);

                _index++;
            }

            _index++;
        }

        int start = _index;

        SkipToEnd(stopAtElse: false);

        // The two tokens of the "#end" that closed it are not part of the body.
        List<Token> body = _tokens.GetRange(start, Math.Max(0, _index - start - 2));

        _macros[name] = new PovMacro { Name = name, Parameters = parameters, Body = body };
    }

    /// <summary>
    /// This method works out what a call to one of the macros we kept comes to, by reading its
    /// body as an expression with the arguments standing in for what it takes.
    /// </summary>
    /// <param name="macro">The macro being called.</param>
    /// <param name="arguments">What it was given.</param>
    /// <param name="line">The line it was called on, for reporting.</param>
    /// <returns>The value the macro comes to.</returns>
    private PovValue ExpandMacro(PovMacro macro, List<PovValue> arguments, int line)
    {
        if (arguments.Count != macro.Parameters.Count)
        {
            throw new PovParseException(
                $"\"{macro.Name}\" wants {macro.Parameters.Count} of them and was given " +
                $"{arguments.Count}.", line);
        }

        // What the macro takes is put in scope for as long as it takes to read the body, and
        // whatever those names meant before is put back afterward.  POV-Ray's own macros shadow
        // freely, and ior.inc calls one with a name it also declares.
        List<(string Name, PovSymbol Was)> shadowed = [];

        for (int index = 0; index < macro.Parameters.Count; index++)
        {
            string parameter = macro.Parameters[index];

            shadowed.Add((parameter, _symbols.GetValueOrDefault(parameter)));

            _symbols[parameter] = new PovSymbol
            {
                Value = arguments[index], SourceFile = _fileName, FromSeparateLibrary = false
            };
        }

        try
        {
            PovParser reader = new PovParser(
                _fileName, _isSeparateLibrary, _isPrelude, macro.Body, _symbols, _macros,
                _issues, _includeReader);

            try
            {
                return reader.ParseWholeExpression(macro.Name, line);
            }
            catch (PovParseException exception)
            {
                // Whatever went wrong went wrong inside the macro, which is nowhere the reader of
                // the report can see, so the macro is named and the line is the call rather than
                // somewhere in a definition they were not looking at.
                throw new PovParseException(
                    $"The macro \"{macro.Name}\" does more than work out a value: {exception.Message}",
                    line);
            }
        }
        finally
        {
            foreach ((string parameter, PovSymbol was) in shadowed)
            {
                if (was is null)
                    _symbols.Remove(parameter);
                else
                    _symbols[parameter] = was;
            }
        }
    }

    /// <summary>
    /// This method reads everything it has as a single expression, which is how a macro's body is
    /// worked out.  Anything left over means the body was more than an expression, and so more
    /// than we can stand in for.
    /// </summary>
    /// <param name="name">The macro's name, for reporting.</param>
    /// <param name="line">The line it was called on, for reporting.</param>
    /// <returns>The value the body comes to.</returns>
    private PovValue ParseWholeExpression(string name, int line)
    {
        if (AtEnd)
            throw new PovParseException($"The macro \"{name}\" gives nothing back.", line);

        PovValue value = ParseExpression();

        if (!AtEnd)
            throw new PovParseException($"The macro \"{name}\" does more than work out a value.", line);

        return value;
    }

    /// <summary>
    /// This method steps over a directive that runs to a matching <c>#end</c>, noting that we did.
    /// </summary>
    /// <param name="what">What was stepped over, for the report.</param>
    private void ParseSkippedBlock(string what)
    {
        int line = CurrentLine;

        _index += 2;

        // What is being skipped very often has a name -- a macro always does -- and naming it in
        // the report is the difference between a note the user can act on and one they cannot.
        string name = Current is IdToken ? Current.Text : null;

        SkipToEnd(stopAtElse: false);

        Report(name, line, $"We do not handle {what}.");
    }

    /// <summary>
    /// This method steps forward to the directive that closes the one we are inside, counting any
    /// that open and close along the way.
    /// </summary>
    /// <param name="stopAtElse">Whether an <c>#else</c> at our own level also stops us, which it
    /// does when the branch we are skipping may have another after it.</param>
    private void SkipToEnd(bool stopAtElse)
    {
        int depth = 0;

        while (!AtEnd)
        {
            if (AtDirective)
            {
                string text = DirectiveName;

                if (OpeningDirectives.Contains(text))
                    depth++;
                else if (text == "end")
                {
                    if (depth == 0)
                    {
                        _index += 2;

                        return;
                    }

                    depth--;
                }
                else if (depth == 0 && stopAtElse && text is "else" or "elseif")
                {
                    _index += 2;

                    // An "#elseif" carries a condition we are about to ignore, having already
                    // decided to take this branch.  Stepping over it leaves the body behind.
                    if (text == "elseif" && Current.IsPunctuation("("))
                        SkipParenthesized();

                    return;
                }
            }

            _index++;
        }
    }

    /// <summary>
    /// This method steps over a parenthesized run of tokens, counting the nesting.
    /// </summary>
    private void SkipParenthesized()
    {
        int depth = 0;

        while (!AtEnd)
        {
            if (Current.IsPunctuation("("))
                depth++;
            else if (Current.IsPunctuation(")"))
            {
                depth--;

                if (depth == 0)
                {
                    _index++;

                    return;
                }
            }

            _index++;
        }
    }

    /// <summary>
    /// This method steps over a braced run of tokens, counting the nesting.
    /// </summary>
    private void SkipBracedBlock()
    {
        int depth = 0;

        while (!AtEnd)
        {
            if (Current.IsPunctuation("{"))
                depth++;
            else if (Current.IsPunctuation("}"))
            {
                depth--;

                if (depth == 0)
                {
                    _index++;

                    return;
                }
            }

            _index++;
        }
    }

    /// <summary>
    /// This method steps forward to the next directive, which is where we pick up again after
    /// something we could not read.  Declarations always start with one, so this lands us at the
    /// start of the next thing rather than somewhere in the middle of this one.
    /// </summary>
    private void SkipToNextDirective()
    {
        while (!AtEnd && !AtDirective)
            _index++;
    }

    /// <summary>
    /// This method steps over everything left on the line we are on.
    /// </summary>
    private void SkipRestOfLine()
    {
        int line = Current.Line;

        while (!AtEnd && Current.Line == line)
            _index++;
    }

    /// <summary>
    /// This method steps forward past the next semicolon.
    /// </summary>
    private void SkipToSemicolon()
    {
        while (!AtEnd && !Current.IsPunctuation(";"))
            _index++;

        if (Current.IsPunctuation(";"))
            _index++;
    }

    /// <summary>
    /// This method steps over the given punctuation, complaining if it is not what we are looking
    /// at.
    /// </summary>
    /// <param name="text">The punctuation that must be here.</param>
    private void Expect(string text)
    {
        if (!Current.IsPunctuation(text))
            throw new PovParseException($"Expected \"{text}\" but found {Current}.", Current.Line);

        _index++;
    }

    /// <summary>
    /// This method notes something we could not bring across.
    /// </summary>
    /// <param name="name">The declaration it was in, or <c>null</c> when it was in none.</param>
    /// <param name="line">The line it was on.</param>
    /// <param name="reason">Why it could not be brought across.</param>
    private void Report(string name, int line, string reason) =>
        _issues.Add(new PovIssue
        {
            SourceFile = _fileName, Line = line, Name = name, Reason = reason
        });
}
