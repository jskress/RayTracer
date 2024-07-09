namespace RayTracer.General;

/// <summary>
/// This class represents a variable pool.  We do the whole parenting thing here so that
/// we can support scoping.
/// </summary>
public class Variables
{
    private readonly Variables _parent;
    private readonly Dictionary<string, object> _variables;

    /// <summary>
    /// This indexer is used to get or set the value of a variable.
    /// </summary>
    /// <param name="key">The name of the variable to get or set.</param>
    public object this[string key]
    {
        get => GetValue(key);
        set => _variables[key] = value;
    }

    public Variables(Variables parent = null)
    {
        _parent = parent;
        _variables = new Dictionary<string, object>();
    }

    /// <summary>
    /// This method is used to look up the named variable.  If we don't have it, and we
    /// have a parent, pass the request on to the parent.
    /// </summary>
    /// <param name="key">The name of the desired variable.</param>
    /// <returns>The value of the variable or <c>null</c>.</returns>
    private object GetValue(string key)
    {
        return _variables.TryGetValue(key, out object value)
            ? value
            : _parent?.GetValue(key);
    }
}
