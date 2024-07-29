namespace RayTracer.General;

/// <summary>
/// This class represents a variable pool.  We do the whole parenting thing here so that
/// we can support scoping.
/// </summary>
public class Variables
{
    private readonly Variables _parent;
    private readonly Dictionary<string, Dictionary<RuntimeTypeHandle, object>> _variables;

    public Variables(Variables parent = null)
    {
        _parent = parent;
        _variables = new Dictionary<string, Dictionary<RuntimeTypeHandle, object>>();
    }

    /// <summary>
    /// This method is used to look up the named variable.  If we don't have it, and we
    /// have a parent, pass the request on to the parent.
    /// </summary>
    /// <param name="key">The name of the desired variable.</param>
    /// <param name="type">The type of value required.  In some cases, this may be <c>null</c>.</param>
    /// <returns>The value of the variable or <c>null</c>.</returns>
    public object GetValue(string key, Type type = null)
    {
        if (!_variables.TryGetValue(key, out Dictionary<RuntimeTypeHandle, object> values))
            return _parent?.GetValue(key, type);

        if (type == null && values.Count == 1)
            return values.Values.First();

        if (type == null || !values.TryGetValue(type.TypeHandle, out object value))
            return _parent?.GetValue(key, type);

        return value;
    }

    /// <summary>
    /// This method is used to set the value for a variable.  Passing <c>null</c> for
    /// <c>value</c> will remove the variable if there is only one value for that variable.
    /// </summary>
    /// <param name="key">The name of the desired variable.</param>
    /// <param name="value">The value to set.</param>
    public void SetValue(string key, object value)
    {
        if (!_variables.TryGetValue(key, out Dictionary<RuntimeTypeHandle, object> values))
            _variables[key] = values = new Dictionary<RuntimeTypeHandle, object>();

        if (value == null && values.Count == 1)
            _variables.Remove(key);

        values[value!.GetType().TypeHandle] = value;
    }
}
