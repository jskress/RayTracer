namespace RayTracer.Instructions;

/// <summary>
/// This interface defines the contract for something that can validate its current state.
/// </summary>
public interface IValidatable
{
    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    string Validate();
}
