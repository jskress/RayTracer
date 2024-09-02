using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class holds all the information we need to know about an interval
/// </summary>
public class GroupInterval
{
    /// <summary>
    /// This property holds the name, if any, of the variable to set for each iteration,
    /// </summary>
    internal string VariableName { get; }

    private readonly Term _startTerm;
    private readonly Term _endTerm;
    private readonly Term _stepTerm;
    private readonly bool _startIsOpen;
    private readonly bool _endIsOpen;

    public GroupInterval(
        string variableName, Term startTerm, Term endTerm, Term stepTerm,
        bool startIsOpen, bool endIsOpen)
    {
        if ((startTerm == null && endTerm != null) ||
            (startTerm != null && endTerm == null))
            throw new Exception("Internal error: wrong number of terms given.");

        _startTerm = startTerm;
        _endTerm = endTerm;
        _stepTerm = stepTerm;
        _startIsOpen = startIsOpen;
        _endIsOpen = endIsOpen;

        VariableName = variableName;
    }

    /// <summary>
    /// This method is used to return an interval based on the terms we were (or weren't)
    /// given upon construction that will drive our execution.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The appropriate interval for controlling our execution.</returns>
    internal Interval GetInterval(Variables variables)
    {
        double start, end;

        if (_startTerm is null)
            start = end = 1;
        else
        {
            start = _startTerm.GetValue<double>(variables);
            end = _endTerm.GetValue<double>(variables);
        }

        double step = _stepTerm?.GetValue<double>(variables) ?? 1;

        return new Interval
            {
                Start = start,
                End = end,
                IsStartOpen = _startIsOpen,
                IsEndOpen = _endIsOpen
            }
            .Reset(step);
    }
}
