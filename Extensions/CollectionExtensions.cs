namespace RayTracer.Extensions;

/// <summary>
/// This class provides some useful extension methods for lists.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// This is a helper method for checking a stack for being empty.
    /// </summary>
    /// <param name="stack">The stack to examine.</param>
    /// <returns><c>true</c>, if the stack is empty.</returns>
    internal static bool IsEmpty<T>(this Stack<T> stack)
    {
        return stack.Count == 0;
    }

    /// <summary>
    /// This is a helper method for checking a list for being empty.
    /// </summary>
    /// <param name="list">The list to examine.</param>
    /// <returns><c>true</c>, if the list is empty.</returns>
    internal static bool IsEmpty<T>(this List<T> list)
    {
        return list.Count == 0;
    }

    /// <summary>
    /// This is a helper method for checking a list for being either <c>null</c> or empty.
    /// </summary>
    /// <param name="list">The list to examine.</param>
    /// <returns><c>true</c>, if the list is <c>null</c> or empty.</returns>
    internal static bool IsNullOrEmpty<T>(this IEnumerable<T> list)
    {
        return list == null || !list.Any();
    }

    /// <summary>
    /// This is a helper method for checking an array for being either <c>null</c> or empty.
    /// </summary>
    /// <param name="array">The list to examine.</param>
    /// <returns><c>true</c>, if the array is <c>null</c> or empty.</returns>
    internal static bool IsNullOrEmpty<T>(this T[] array)
    {
        return array == null || array.Length == 0;
    }

    /// <summary>
    /// This method is used to remove the first element in a list, making it available.
    /// If the list is <c>null</c> or empty, then <c>null</c> is returned.
    /// </summary>
    /// <param name="list">The list to remove the element from.</param>
    /// <returns>The element from the list.</returns>
    internal static T RemoveFirst<T>(this List<T> list)
        where T : class
    {
        if (list == null || list.Count == 0)
            return null;

        T result = list[0];

        list.RemoveAt(0);

        return result;
    }
}
