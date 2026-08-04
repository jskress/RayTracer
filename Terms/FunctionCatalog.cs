using System.Reflection;

namespace RayTracer.Terms;

/// <summary>
/// This class holds every function the DSL knows, gathered from the methods that implement them by
/// way of the <see cref="FunctionAttribute"/> they carry.  It is the one place that answers what a
/// function is called, what forms of it exist and which of those a particular call means; the
/// parser, the evaluator, the tests and the documentation all read from here rather than keeping
/// lists of their own.
/// <para>
/// A call is resolved in stages, because the DSL binds late and a scene's variables have no type
/// until they are evaluated.  At parse time only the name and the number of values given can be
/// checked, which is enough to catch a misspelling or a missing argument where the author can still
/// see it (<see cref="IsKnown"/> and <see cref="Accepts"/>).  Which form of an overloaded function
/// is meant waits until there are values to look at (<see cref="Match"/>).
/// </para>
/// </summary>
public class FunctionCatalog
{
    /// <summary>
    /// This property holds the catalog of functions the DSL itself uses.
    /// </summary>
    public static FunctionCatalog Instance { get; } = new (typeof(MathFunctions));

    /// <summary>
    /// This property reports the name of every function in the catalog.
    /// </summary>
    public IEnumerable<string> Names => _signatures.Keys;

    private readonly Dictionary<string, List<FunctionSignature>> _signatures = new ();

    /// <summary>
    /// This constructor builds a catalog out of the functions declared by the given classes.  The
    /// DSL's own catalog is <see cref="Instance"/>; this is public so that a test may build a
    /// catalog of its own to try the resolution rules against.
    /// </summary>
    /// <param name="declaringTypes">The classes whose functions to gather.</param>
    public FunctionCatalog(params Type[] declaringTypes)
    {
        foreach (Type type in declaringTypes)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                foreach (FunctionAttribute attribute in method.GetCustomAttributes<FunctionAttribute>())
                {
                    if (!_signatures.TryGetValue(attribute.Name, out List<FunctionSignature> forms))
                    {
                        forms = [];

                        _signatures[attribute.Name] = forms;
                    }

                    forms.Add(new FunctionSignature(attribute.Name, method));
                }
            }
        }
    }

    /// <summary>
    /// This method reports whether the catalog holds a function of the given name.
    /// </summary>
    /// <param name="name">The name to look for.</param>
    /// <returns><c>true</c>, if a function of that name exists.</returns>
    public bool IsKnown(string name)
    {
        return _signatures.ContainsKey(name);
    }

    /// <summary>
    /// This method reports whether any form of the named function takes the given number of
    /// values.  This is the parse-time check: it cannot tell whether the values are of the right
    /// types, since they have none yet, but it can tell that no form of the function takes that
    /// many of them.
    /// </summary>
    /// <param name="name">The name of the function called.</param>
    /// <param name="argumentCount">The number of values the call supplies.</param>
    /// <returns><c>true</c>, if some form of the function takes that many values.</returns>
    public bool Accepts(string name, int argumentCount)
    {
        return _signatures.TryGetValue(name, out List<FunctionSignature> forms) &&
               forms.Any(form => form.ParameterCount == argumentCount);
    }

    /// <summary>
    /// This method is used to check a call as far as it can be checked while it is being parsed,
    /// which is its name and how many values it supplies.  This is the check worth making early:
    /// a misspelled name or a missing argument is reported against the text the author wrote,
    /// rather than waiting for the scene to be rendered.
    /// </summary>
    /// <param name="name">The name of the function called.</param>
    /// <param name="argumentCount">The number of values the call supplies.</param>
    /// <returns>What is wrong with the call, or <c>null</c> if nothing is.</returns>
    public string CheckCall(string name, int argumentCount)
    {
        if (!_signatures.TryGetValue(name, out List<FunctionSignature> forms))
            return $"There is no function named '{name}'.";

        return forms.Any(form => form.ParameterCount == argumentCount)
            ? null
            : $"The function '{name}' does not take {ArgumentText(argumentCount)}; it takes " +
              $"{ArgumentText(forms.Select(form => form.ParameterCount))}.";
    }

    /// <summary>
    /// This method is used to settle which form of a function a call means from the <i>types</i> of
    /// what it is given rather than from the values themselves.  This is the third place a call is
    /// resolved, and the only one where the answer can be settled for good: a field function deals in
    /// numbers throughout and is compiled before it is ever asked anything, so there is nothing left
    /// to wait for.
    /// </summary>
    /// <param name="name">The name of the function called.</param>
    /// <param name="argumentTypes">The types of the values the call supplies.</param>
    /// <returns>The form of the function to call, or <c>null</c> along with what is wrong.</returns>
    public (FunctionSignature Signature, string Error) ResolveForTypes(
        string name, params Type[] argumentTypes)
    {
        string problem = CheckCall(name, argumentTypes.Length);

        if (problem != null)
            return (null, problem);

        List<FunctionSignature> fits = _signatures[name]
            .Where(form => form.ParameterCount == argumentTypes.Length &&
                           form.ParameterTypes.Zip(argumentTypes)
                               .All(pair => pair.First == pair.Second))
            .ToList();

        return fits.Count switch
        {
            1 => (fits[0], null),
            0 => (null, $"No form of the function '{name}' takes " +
                        $"({string.Join(", ", argumentTypes.Select(FunctionSignature.DslNameFor))}). " +
                        $"It takes {string.Join(" or ", SignaturesFor(name).Select(form => form.ToString()))}."),
            _ => (null, $"The call to '{name}' is ambiguous; it fits " +
                        $"{string.Join(" and ", fits.Select(form => form.ToString()))}.")
        };
    }

    /// <summary>
    /// This method returns every form of the named function, or an empty list if there is no such
    /// function.
    /// </summary>
    /// <param name="name">The name of the function to describe.</param>
    /// <returns>The forms of that function.</returns>
    public IReadOnlyList<FunctionSignature> SignaturesFor(string name)
    {
        return _signatures.TryGetValue(name, out List<FunctionSignature> forms) ? forms : [];
    }

    /// <summary>
    /// This method is used to settle which form of a function a call means, given the values it
    /// supplies.  A form that fits the values as they already are wins outright, so an overload is
    /// never taken by conversion while another one already fits; only if none fits exactly do the
    /// DSL's conversions get a say.  Anything else -- no such function, no form taking that many
    /// values, no form the values fit, or more than one form fitting equally well -- comes back as
    /// a match that failed, carrying the reason.
    /// </summary>
    /// <param name="name">The name of the function called.</param>
    /// <param name="arguments">The values the call supplies.  Note that a bare <c>null</c> here is
    /// no arguments at all, since that is what it binds to; a call supplying one value that
    /// resolved to nothing must pass <c>[null]</c>.</param>
    /// <returns>The outcome of the match.</returns>
    public FunctionMatch Match(string name, params object[] arguments)
    {
        arguments ??= [];

        string problem = CheckCall(name, arguments.Length);

        if (problem != null)
            return new FunctionMatch(problem);

        List<FunctionSignature> candidates = _signatures[name]
            .Where(form => form.ParameterCount == arguments.Length)
            .ToList();

        FunctionMatch match = MatchAgainst(candidates, arguments, true) ??
                              MatchAgainst(candidates, arguments, false);

        return match ?? new FunctionMatch(
            $"No form of the function '{name}' takes ({string.Join(", ", arguments.Select(TypeNameOf))}). " +
            $"It takes {string.Join(" or ", candidates.Select(form => form.ToString()))}.");
    }

    /// <summary>
    /// This method is used to try the given values against each of the candidate forms, either as
    /// they are or through the DSL's conversions.  Exactly one fit is a match; several fits is an
    /// ambiguity, which is reported rather than guessed at; no fit at all leaves the decision to
    /// the caller, which may have another pass to try.
    /// </summary>
    /// <param name="candidates">The forms of the function that take the right number of values.</param>
    /// <param name="arguments">The values the call supplies.</param>
    /// <param name="exactly">Whether the values must already be of the types wanted.</param>
    /// <returns>The match, or <c>null</c> if no candidate fit.</returns>
    private static FunctionMatch MatchAgainst(
        List<FunctionSignature> candidates, object[] arguments, bool exactly)
    {
        List<(FunctionSignature Form, object[] Values)> fits = [];

        foreach (FunctionSignature candidate in candidates)
        {
            if (candidate.TryBind(arguments, exactly, out object[] values))
                fits.Add((candidate, values));
        }

        return fits.Count switch
        {
            0 => null,
            1 => new FunctionMatch(fits[0].Form, fits[0].Values),
            _ => new FunctionMatch(
                $"The call to '{candidates[0].Name}' is ambiguous; it fits " +
                $"{string.Join(" and ", fits.Select(fit => fit.Form.ToString()))}.")
        };
    }

    /// <summary>
    /// This method returns the DSL's name for the type of a value a call supplied, for error
    /// messages.
    /// </summary>
    /// <param name="argument">The value to name the type of.</param>
    /// <returns>The DSL's name for the value's type.</returns>
    private static string TypeNameOf(object argument)
    {
        return argument is null ? "null" : FunctionSignature.DslNameFor(argument.GetType());
    }

    /// <summary>
    /// This method phrases a count of arguments for an error message.
    /// </summary>
    /// <param name="count">The number of arguments.</param>
    /// <returns>The count, as words.</returns>
    private static string ArgumentText(int count)
    {
        return count == 1 ? "1 argument" : $"{count} arguments";
    }

    /// <summary>
    /// This method phrases the set of argument counts a function's forms take, for an error
    /// message.
    /// </summary>
    /// <param name="counts">The number of arguments each form takes.</param>
    /// <returns>The counts, as words.</returns>
    private static string ArgumentText(IEnumerable<int> counts)
    {
        List<int> ordered = counts.Distinct().Order().ToList();

        return ordered.Count == 1
            ? ArgumentText(ordered[0])
            : $"{string.Join(", ", ordered.Take(ordered.Count - 1))} or {ArgumentText(ordered[^1])}";
    }
}
