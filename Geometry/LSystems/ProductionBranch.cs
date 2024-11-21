using System.Text;

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
    private record Entry(Rune? Rune = null, ProductionBranch Branch = null)
    {
        internal bool IsRune => Rune is not null;
        internal bool IsBranch => Branch is not null;

        public override string ToString()
        {
            return IsRune ? Rune.ToString() : $"[{Branch}]";
        }
    }

    /// <summary>
    /// This method is used to create a production branch from the given set of runes.
    /// </summary>
    /// <param name="source">The runes to create the branch from.</param>
    /// <param name="symbolsToIgnore">The array of runes that are to be ignored.</param>
    /// <returns>The created branch.</returns>
    public static ProductionBranch Parse(Rune[] source, Rune[] symbolsToIgnore = null)
    {
        List<Entry> entries = [];
        int index = 0;
        
        symbolsToIgnore ??= [];

        while (index < source.Length)
        {
            if (!symbolsToIgnore.Contains(source[index]))
            {
                if (source[index] == LSystemProducer.LeftBracket)
                {
                    int end = FindClosingBracket(source, index);

                    entries.Add(new Entry(Branch: Parse(source[(index + 1)..end], symbolsToIgnore)));

                    index = end;
                }
                else if (source[index] != LSystemProducer.RightBracket)
                    entries.Add(new Entry(Rune: source[index]));
            }

            index++;
        }

        return new ProductionBranch(entries);
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
    internal bool Matches(ProductionBranch other, ProductBranchMatchStyle style)
    {
        return style switch
        {
            ProductBranchMatchStyle.AtEnd => MatchToTheLeft(other),
            ProductBranchMatchStyle.AtStart => MatchToTheRight(other),
            _ => false
        };
    }

    /// <summary>
    /// This method compares the entries for this branch to another starting at an index,
    /// matching from right to left.
    /// </summary>
    /// <param name="other">The branch we are comparing ourselves to.</param>
    /// <returns><c>true</c>, if the entries match, or <c>false</c>, if not.</returns>
    private bool MatchToTheLeft(ProductionBranch other)
    {
        int theirs = other._entries.Count - 1;

        for (int ours = _entries.Count - 1; ours >= 0; ours--)
        {
            if (_entries[ours].IsRune)
                theirs = FindRune(other._entries, theirs, -1);

            if (theirs < 0 || !MatchesAt(other, ProductBranchMatchStyle.AtEnd, ours, theirs))
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
    private bool MatchToTheRight(ProductionBranch other)
    {
        int theirs = 0;

        for (int ours = 0; ours < _entries.Count; ours++)
        {
            if (_entries[ours].IsRune)
                theirs = FindRune(other._entries, theirs, 1);

            if (theirs < 0 || !MatchesAt(other, ProductBranchMatchStyle.AtStart, ours, theirs))
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
        ProductionBranch other, ProductBranchMatchStyle style, int ourIndex, int theirIndex)
    {
        if ((_entries[ourIndex].IsRune && other._entries[theirIndex].IsBranch) ||
            (_entries[ourIndex].IsBranch && other._entries[theirIndex].IsRune))
            return false;

        if (_entries[ourIndex].IsRune)
            return _entries[ourIndex].Rune == other._entries[theirIndex].Rune;
        
        return _entries[ourIndex].Branch.Matches(other._entries[theirIndex].Branch, style);
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
