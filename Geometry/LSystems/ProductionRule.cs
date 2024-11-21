using System.Text;
using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class represents a production rule for an L-system.
/// </summary>
public class ProductionRule : ProductionRuleBase
{
    /// <summary>
    /// This property holds the spectrum of production values.
    /// When this rule does not have any stochastic aspect, the spectrum will hold only a
    /// single value.
    /// </summary>
    public Spectrum<Rune[]> Productions { get; } = new ();

    /// <summary>
    /// This property holds the collection of runes that should be ignored regarding
    /// context evaluation.
    /// </summary>
    internal Rune[] SymbolsToIgnore { get; init; }

    /// <summary>
    /// This method is used to determine whether this rule applies to the given set of
    /// runes.
    /// If we carry no context, then we must.
    /// If we do carry context, then those contexts must match as well for us to match.
    /// </summary>
    /// <param name="source">The source to check.</param>
    /// <param name="index">The current location in the source.</param>
    /// <returns><c>true</c>, if this rule matches the given source at the index provided.</returns>
    public bool Matches(Rune[] source, int index)
    {
        // Note: we will only be here in this method if our variable was already matched.
        if (!SymbolsToIgnore.IsNullOrEmpty() &&
            (LeftContext is not null || RightContext is not null))
            (source, index) = RemoveIgnoredSymbols(source, index);

        if (LeftContext is not null && !LeftContextMatches(source, index))
            return false;

        return RightContext is null || RightContextMatches(source, index);
    }

    /// <summary>
    /// This method is used to create an array of runes that does not contain any symbols
    /// we are to ignore.
    /// </summary>
    /// <param name="source">The array to start with.</param>
    /// <param name="index">The index in the array of the current symbol.</param>
    /// <returns>A new array that does not contain any of our ignored symbols and the
    /// updated index that accounts for removals.</returns>
    private (Rune[], int) RemoveIgnoredSymbols(Rune[] source, int index)
    {
        List<Rune> work = [];
        int leftCount = 0;

        for (int i = 0; i < source.Length; i++)
        {
            if (SymbolsToIgnore.Contains(source[i]))
            {
                if (i < index)
                    leftCount++;
            }
            else
                work.Add(source[i]);
        }

        index -= leftCount;

        return (work.ToArray(), index);
    }

    /// <summary>
    /// This method is used to test whether the left context matches the given source.
    /// </summary>
    /// <param name="source">The source to check.</param>
    /// <param name="index">The current location in the source.</param>
    /// <returns><c>true</c>, if the left context matches the given source at the index
    /// provided.</returns>
    private bool LeftContextMatches(Rune[] source, int index)
    {
        if (index > 0 && source[index - 1] == LSystemProducer.LeftBracket)
            index--;

        if (index > 0)
        {
            int start = FindSiblingStart(source, index - 1);

            if (start < index)
            {
                ProductionBranch left = ProductionBranch.Parse(source[(start + 1)..index]);

                return LeftContext.Matches(left, ProductBranchMatchStyle.AtEnd);
            }
        }

        return false;
    }

    /// <summary>
    /// This method is used to test whether the right context matches the given source.
    /// </summary>
    /// <param name="source">The source to check.</param>
    /// <param name="index">The current location in the source.</param>
    /// <returns><c>true</c>, if the right context matches the given source at the index
    /// provided.</returns>
    private bool RightContextMatches(Rune[] source, int index)
    {
        if (index < source.Length - 1)
        {
            int end = FindSiblingEnd(source, index + 1);

            if (index < end)
            {
                ProductionBranch right = ProductionBranch.Parse(source[(index + 1)..end]);

                return RightContext.Matches(right, ProductBranchMatchStyle.AtStart);
            }
        }

        return false;
    }

    /// <summary>
    /// This method is used to scan to the left of the given point in the source and find
    /// the beginning of the current sibling list.
    /// </summary>
    /// <param name="source">The source to check.</param>
    /// <param name="index">The current location in the source.</param>
    /// <returns>The index of the left bracket that begins the current sibling list or
    /// <c>-1</c> if such a bracket could not be found.</returns>
    private static int FindSiblingStart(Rune[] source, int index)
    {
        int depth = 0;

        do
        {
            if (source[index] == LSystemProducer.RightBracket)
                depth++;
            else if (source[index] == LSystemProducer.LeftBracket)
            {
                depth--;

                if (depth < 0)
                    break;
            }

            index--;
        }
        while (index >= 0);

        return index;
    }

    /// <summary>
    /// This method is used to scan to the right of the given point in the source and find
    /// the end of the current sibling list.
    /// </summary>
    /// <param name="source">The source to check.</param>
    /// <param name="index">The current location in the source.</param>
    /// <returns>The index of the right bracket that ends the current sibling list or
    /// <c>source.Length</c> if such a bracket could not be found.</returns>
    private static int FindSiblingEnd(Rune[] source, int index)
    {
        int depth = 0;

        do
        {
            if (source[index] == LSystemProducer.LeftBracket)
                depth++;
            else if (source[index] == LSystemProducer.RightBracket)
            {
                depth--;

                if (depth < 0)
                    break;
            }

            index++;
        }
        while (index < source.Length);

        return index;
    }

    /// <summary>
    /// This method provides a string representation of this production rule.
    /// </summary>
    /// <returns>This rule, as a string.</returns>
    public override string ToString()
    {
        string text;
        if (Productions.Count == 1)
        {
            (_, Rune[] production) = Productions.GetByIndex(0);
            text = production.AsString();
        }
        else
        {
            List<string> entries = [];

            for (int index = 0; index < Productions.Count; index++)
            {
                (double breakValue, Rune[] production) = Productions.GetByIndex(index);
                entries.Add($"{production.AsString()} >= {breakValue}");
            }
            
            text = string.Join(", ", entries);
        }
        
        return $"{base.ToString()} -> {text}";
    }
}
