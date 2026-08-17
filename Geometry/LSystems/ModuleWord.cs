using System.Text;
using RayTracer.Extensions;
using RayTracer.Terms;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class reads an L-system word -- an axiom, or the successor of a production -- into the
/// modules it is written as.
/// <para>
/// The whole of the difficulty is that a word's letters and its arithmetic are drawn from the same
/// characters.  Prusinkiewicz and Lindenmayer say so plainly: <c>+</c>, <c>&amp;</c>, <c>^</c> and
/// <c>/</c> are used both as letters of the alphabet and as operators, and which is meant "depends
/// on the context".  In <c>F(x*h)+F(x*q)</c> the <c>*</c> is arithmetic and the <c>+</c> is a
/// letter.
/// </para>
/// <para>
/// Scanning for operators would therefore be ambiguous.  Reading module by module is not: a letter,
/// then -- only when the very next character is an open parenthesis -- a <em>balanced</em>
/// parenthesised group.  Everything inside that group is arithmetic and everything outside it is
/// alphabet, by construction rather than by guesswork, so the expression parser is never handed a
/// character that was meant as a letter.
/// </para>
/// </summary>
public static class ModuleWord
{
    private static readonly Rune OpenParenthesis = new ('(');
    private static readonly Rune CloseParenthesis = new (')');
    private static readonly Rune Comma = new (',');

    /// <summary>
    /// This method reads a word into the modules it is written as.
    /// </summary>
    /// <param name="text">The word to read.</param>
    /// <param name="compile">How to compile a piece of argument text into a term.  It is only
    /// asked for when a module actually carries arguments, so a word written without any -- which
    /// is every word in every L-system written before parameters existed -- needs none.</param>
    /// <returns>The modules the word is written as.</returns>
    public static ModuleTemplate[] Parse(string text, Func<string, Term> compile = null)
    {
        List<ModuleTemplate> modules = [];
        Rune[] runes = text.AsRunes();
        int index = 0;

        while (index < runes.Length)
        {
            Rune letter = runes[index++];

            if (index >= runes.Length || runes[index] != OpenParenthesis)
            {
                modules.Add(new ModuleTemplate { Letter = letter });

                continue;
            }

            if (compile is null)
            {
                throw new Exception(
                    $"The module '{letter}' carries parameters, which need a scene to work them " +
                    "out; none was available here.");
            }

            int close = MatchingParenthesis(runes, index);

            modules.Add(new ModuleTemplate
            {
                Letter = letter,
                Arguments = Arguments(Text(runes, index + 1, close), compile)
            });

            index = close + 1;
        }

        return modules.ToArray();
    }

    /// <summary>
    /// This method finds the parenthesis closing the one at the given place.  It counts depth
    /// rather than taking the first close it meets, since an argument may perfectly well be
    /// something like <c>max(x, 1)</c>.
    /// </summary>
    private static int MatchingParenthesis(Rune[] runes, int open)
    {
        int depth = 0;

        for (int index = open; index < runes.Length; index++)
        {
            if (runes[index] == OpenParenthesis)
                depth++;
            else if (runes[index] == CloseParenthesis && --depth == 0)
                return index;
        }

        throw new Exception("An L-system module opens a parenthesis that is never closed.");
    }

    /// <summary>
    /// This method splits a module's parentheses into its arguments and compiles each one.  The
    /// split is on commas at the top level only, so that a comma inside a nested call belongs to
    /// that call rather than ending an argument.
    /// </summary>
    private static Term[] Arguments(string inside, Func<string, Term> compile)
    {
        if (inside.Trim().Length == 0)
            return [];

        List<Term> terms = [];
        Rune[] runes = inside.AsRunes();
        int depth = 0;
        int start = 0;

        for (int index = 0; index <= runes.Length; index++)
        {
            bool end = index == runes.Length;

            if (!end && runes[index] == OpenParenthesis)
                depth++;
            else if (!end && runes[index] == CloseParenthesis)
                depth--;

            if (!end && (runes[index] != Comma || depth != 0))
                continue;

            string piece = Text(runes, start, index);

            if (piece.Trim().Length == 0)
                throw new Exception("An L-system module has an empty argument.");

            terms.Add(compile(piece));

            start = index + 1;
        }

        return terms.ToArray();
    }

    /// <summary>
    /// This method reads the runes between two places back into a string.
    /// </summary>
    private static string Text(Rune[] runes, int start, int end)
    {
        return string.Concat(runes[start..end].Select(rune => rune.ToString()));
    }

    /// <summary>
    /// This method writes a word out the way it would be written down: each module's letter, and
    /// its numbers in parentheses where it carries any.
    /// </summary>
    /// <param name="modules">The word to write out.</param>
    /// <returns>The word, as text.</returns>
    public static string AsText(Module[] modules)
    {
        return string.Concat(modules.Select(module => module.ToString()));
    }

    /// <summary>
    /// This method takes the whitespace out of a word, but only where it is <em>outside</em> a
    /// module's parentheses.
    /// <para>
    /// A word is walked letter by letter, so a stray space would otherwise become a module of its
    /// own; that is why the whitespace has always been stripped.  But arithmetic is not letters,
    /// and a language with word operators would be wrecked by it -- <c>x &gt; 1 and y &lt; 2</c>
    /// run together reads as <c>x&gt;1andy&lt;2</c>, which is one nonsense identifier.  So inside
    /// the parentheses every character is left exactly as written.
    /// </para>
    /// </summary>
    /// <param name="text">The word to strip.</param>
    /// <returns>The word, with the whitespace between modules taken out.</returns>
    public static string StripWhitespaceBetweenModules(string text)
    {
        StringBuilder builder = new ();
        int depth = 0;

        foreach (Rune rune in text.AsRunes())
        {
            if (rune == OpenParenthesis)
                depth++;
            else if (rune == CloseParenthesis)
                depth--;

            if (depth > 0 || rune == CloseParenthesis || !Rune.IsWhiteSpace(rune))
                builder.Append(rune);
        }

        return builder.ToString();
    }

    /// <summary>
    /// This method reads the formal parameter names a production's predecessor is written with --
    /// the <c>x</c> and <c>t</c> of <c>F(x, t)</c>.  These are names being bound rather than
    /// arithmetic being evaluated, so they are taken as written.
    /// </summary>
    /// <param name="text">The predecessor to read.</param>
    /// <returns>The letter and the formal names it binds.</returns>
    public static (Rune Letter, string[] Formals) ParsePredecessor(string text)
    {
        Rune[] runes = text.Trim().AsRunes();

        if (runes.Length == 0)
            throw new Exception("An L-system production has an empty predecessor.");

        Rune letter = runes[0];

        if (runes.Length == 1)
            return (letter, []);

        if (runes[1] != OpenParenthesis)
        {
            throw new Exception(
                $"The L-system production for '{letter}' has more than a letter before its " +
                "parameters.");
        }

        int close = MatchingParenthesis(runes, 1);
        string inside = Text(runes, 2, close);

        if (close != runes.Length - 1)
        {
            throw new Exception(
                $"The L-system production for '{letter}' has something after its parameters.");
        }

        if (inside.Trim().Length == 0)
            return (letter, []);

        return (letter, inside.Split(',').Select(name => name.Trim()).ToArray());
    }
}
