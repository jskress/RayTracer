using System.Text;
using RayTracer.Extensions;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This enum tells a branch how to match against another branch.
/// </summary>
internal enum ProductBranchMatchStyle
{
    AtEnd,
    AtStart
}

/// <summary>
/// This class represents a tree of production variables for matching.
/// </summary>
public class ProductionBranch
{
    /// <summary>
    /// This record represents an entry in a branch.  Only one of <c>Variable</c>
    /// or <c>Branch</c> will be present.
    /// </summary>
    /// <param name="Rune">The rune in the sibling list.</param>
    /// <param name="Branch">The branch in the sibling list.</param>
    private record Entry(
        Rune? Rune = null,
        ProductionBranch Branch = null,
        string[] Formals = null,
        double[] Values = null)
    {
        internal bool IsRune => Rune is not null;
        internal bool IsBranch => Branch is not null;

        public override string ToString()
        {
            if (IsBranch)
                return $"[{Branch}]";

            if (Formals is { Length: > 0 })
                return $"{Rune}({string.Join(", ", Formals)})";

            return Values is { Length: > 0 }
                ? $"{Rune}({string.Join(", ", Values)})"
                : Rune.ToString();
        }
    }

    /// <summary>
    /// This method creates a branch out of a piece of the word being rewritten -- the reality a
    /// context pattern is matched against.  Each module keeps the numbers it carries, so that a
    /// pattern naming them has something to bind.
    /// </summary>
    /// <param name="source">The modules to create the branch from.</param>
    /// <returns>The created branch.</returns>
    public static ProductionBranch Parse(Module[] source)
    {
        List<Entry> entries = [];
        int index = 0;

        while (index < source.Length)
        {
            if (source[index].Letter == LSystemProducer.LeftBracket)
            {
                int end = FindClosingBracket(source, index);

                entries.Add(new Entry(Branch: Parse(source[(index + 1)..end])));

                index = end;
            }
            else if (source[index].Letter != LSystemProducer.RightBracket)
            {
                entries.Add(new Entry(
                    Rune: source[index].Letter, Values: source[index].Parameters));
            }

            index++;
        }

        return new ProductionBranch(entries);
    }

    /// <summary>
    /// This method creates a branch out of the text a context is written as -- the pattern rather
    /// than the reality.  A letter here may be followed by parentheses holding <em>names</em>, as
    /// in <c>A(x)</c>: those are formal parameters waiting to be bound to whatever the module that
    /// matches them turns out to be carrying.
    /// </summary>
    /// <param name="text">The context as written.</param>
    /// <returns>The created branch.</returns>
    public static ProductionBranch ParsePattern(string text)
    {
        return ParsePattern(text.AsRunes(), 0, text.AsRunes().Length);
    }

    private static ProductionBranch ParsePattern(Rune[] source, int from, int to)
    {
        List<Entry> entries = [];
        int index = from;

        while (index < to)
        {
            if (source[index] == LSystemProducer.LeftBracket)
            {
                int end = FindClosingBracket(source, index);

                entries.Add(new Entry(Branch: ParsePattern(source, index + 1, end)));

                index = end + 1;

                continue;
            }

            if (source[index] == LSystemProducer.RightBracket)
            {
                index++;

                continue;
            }

            Rune letter = source[index++];

            // The names travel with the letter before them, exactly as in a word, and for the same
            // reason: a parenthesis belongs to the letter it follows and to nothing else.
            if (index < to && source[index] == OpenParenthesis)
            {
                int close = FindClosingParenthesis(source, index, to);
                string inside = string.Concat(source[(index + 1)..close]
                    .Select(rune => rune.ToString()));

                entries.Add(new Entry(
                    Rune: letter,
                    Formals: inside.Trim().Length == 0
                        ? []
                        : inside.Split(',').Select(name => name.Trim()).ToArray()));

                index = close + 1;

                continue;
            }

            entries.Add(new Entry(Rune: letter));
        }

        return new ProductionBranch(entries);
    }

    private static readonly Rune OpenParenthesis = new ('(');
    private static readonly Rune CloseParenthesis = new (')');

    private static int FindClosingParenthesis(Rune[] source, int index, int to)
    {
        int depth = 0;

        for (int scan = index; scan < to; scan++)
        {
            if (source[scan] == OpenParenthesis)
                depth++;
            else if (source[scan] == CloseParenthesis && --depth == 0)
                return scan;
        }

        throw new Exception("An L-system context opens a parenthesis that is never closed.");
    }

    private readonly List<Entry> _entries;

    private ProductionBranch(List<Entry> entries)
    {
        _entries = entries;
    }

    /// <summary>
    /// This method is used to match this branch, interpreted as a pattern, to a given
    /// branch of reality.
    /// </summary>
    /// <param name="other">The branch to try to match to.</param>
    /// <param name="style">The style of matching for the set of runes.</param>
    /// <returns><c>true</c>, if this branch matches the one provided, or <c>false</c>, if
    /// not.</returns>
    /// <param name="bindings">Collects what a parametric context binds: where this pattern says
    /// <c>A(x)</c> and the word says <c>A(4)</c>, <c>x</c> is bound to 4 here.  The bindings are
    /// only worth keeping if the whole match succeeds, so a caller that gets <c>false</c> back
    /// should discard them.</param>
    internal bool Matches(
        ProductionBranch other, ProductBranchMatchStyle style,
        Dictionary<string, double> bindings = null)
    {
        return style switch
        {
            ProductBranchMatchStyle.AtEnd => MatchToTheLeft(other, bindings),
            ProductBranchMatchStyle.AtStart => MatchToTheRight(other, bindings),
            _ => false
        };
    }

    /// <summary>
    /// This method compares the entries for this branch to another starting at an index,
    /// matching from right to left.
    /// </summary>
    /// <param name="other">The branch we are comparing ourselves to.</param>
    /// <returns><c>true</c>, if the entries match, or <c>false</c>, if not.</returns>
    private bool MatchToTheLeft(ProductionBranch other, Dictionary<string, double> bindings)
    {
        int theirs = other._entries.Count - 1;

        for (int ours = _entries.Count - 1; ours >= 0; ours--)
        {
            if (_entries[ours].IsRune)
                theirs = FindRune(other._entries, theirs, -1);

            if (theirs < 0 ||
                !MatchesAt(other, ProductBranchMatchStyle.AtEnd, ours, theirs, bindings))
                return false;

            theirs--;
        }

        return true;
    }

    /// <summary>
    /// This method compares the entries for this branch to another starting at an index,
    /// matching from left to right.
    /// </summary>
    /// <param name="other">The branch we are comparing ourselves to.</param>
    /// <returns><c>true</c>, if the entries match, or <c>false</c>, if not.</returns>
    private bool MatchToTheRight(ProductionBranch other, Dictionary<string, double> bindings)
    {
        int theirs = 0;

        for (int ours = 0; ours < _entries.Count; ours++)
        {
            if (_entries[ours].IsRune)
                theirs = FindRune(other._entries, theirs, 1);

            if (theirs < 0 ||
                !MatchesAt(other, ProductBranchMatchStyle.AtStart, ours, theirs, bindings))
                return false;

            theirs++;
        }

        return true;
    }

    /// <summary>
    /// This method compares the entries for this branch to another at a specific index.
    /// </summary>
    /// <param name="other">The branch we are comparing ourselves to.</param>
    /// <param name="style">The matching style to use on child branches.</param>
    /// <param name="ourIndex">The index of our entry we are to compare.</param>
    /// <param name="theirIndex">The index of the other branch's entry to which we are to
    /// compare.</param>
    /// <returns><c>true</c>, if the entries match, or <c>false</c>, if not.</returns>
    private bool MatchesAt(
        ProductionBranch other, ProductBranchMatchStyle style, int ourIndex, int theirIndex,
        Dictionary<string, double> bindings)
    {
        Entry ours = _entries[ourIndex];
        Entry theirs = other._entries[theirIndex];

        if ((ours.IsRune && theirs.IsBranch) || (ours.IsBranch && theirs.IsRune))
            return false;

        if (ours.IsBranch)
            return ours.Branch.Matches(theirs.Branch, style, bindings);

        if (ours.Rune != theirs.Rune)
            return false;

        // A context written with names only matches a module carrying exactly that many numbers,
        // which is the same rule the strict predecessor follows.  A context written as a bare
        // letter names nothing and so asks nothing of the numbers.
        if (ours.Formals is not { Length: > 0 })
            return true;

        double[] values = theirs.Values ?? [];

        if (values.Length != ours.Formals.Length)
            return false;

        if (bindings is not null)
        {
            for (int index = 0; index < values.Length; index++)
                bindings[ours.Formals[index]] = values[index];
        }

        return true;
    }

    /// <summary>
    /// This method is used to find the closing bracket that matches the opening one at
    /// the starting index.
    /// </summary>
    /// <param name="source">The source to scan.</param>
    /// <param name="index">The location in the source where we should start; it should be
    /// an opening bracket.</param>
    /// <returns>The index of the closing bracket or the length of the source, if we never
    /// found a balancing closing bracket.</returns>
    private static int FindClosingBracket(Rune[] source, int index)
    {
        int depth = 0;

        do
        {
            if (source[index] == LSystemProducer.LeftBracket)
                depth++;
            else if (source[index] == LSystemProducer.RightBracket)
            {
                depth--;

                if (depth == 0)
                    break;
            }

            index++;
        }
        while (index < source.Length);

        return index;
    }

    /// <summary>
    /// This method finds the bracket closing the one at the given place in a word.
    /// </summary>
    private static int FindClosingBracket(Module[] source, int index)
    {
        int depth = 0;

        do
        {
            if (source[index].Letter == LSystemProducer.LeftBracket)
                depth++;
            else if (source[index].Letter == LSystemProducer.RightBracket)
            {
                depth--;
                
                if (depth == 0)
                    break;
            }

            index++;
        }
        while (index < source.Length);

        return index;
    }

    /// <summary>
    /// This method is used to start at the given index and return the index of the entry
    /// that represents a rune in the indicated direction.
    /// </summary>
    /// <param name="entries">The entries to search.</param>
    /// <param name="index">The index where searching should start.</param>
    /// <param name="direction">The direction to scan.</param>
    /// <returns>The index of the next rune entry, or <c>-1</c>, if we couldn't find a rune.</returns>
    private static int FindRune(List<Entry> entries, int index, int direction)
    {
        while (index >= 0 && index < entries.Count && !entries[index].IsRune)
            index += direction;

        return index >= entries.Count ? -1 : index;
    }

    /// <summary>
    /// This method provides a string representation of this branch.
    /// </summary>
    /// <returns>This branch, as a string.</returns>
    public override string ToString()
    {
        return string.Join("", _entries.Select(entry => entry.ToString()));
    }
}
