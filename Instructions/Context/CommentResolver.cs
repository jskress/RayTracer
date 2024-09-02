using RayTracer.General;

namespace RayTracer.Instructions.Context;

/// <summary>
/// This class is used to resolve a value from a term to append as a comment.
/// </summary>
public class CommentResolver : TermResolver<string>
{
    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override string Resolve(RenderContext context, Variables variables)
    {
        string currentText = context.ImageInformation.Comment;
        string newText = base.Resolve(context, variables);

        return string.IsNullOrEmpty(currentText) || newText == null
            ? newText
            : $"{currentText}\n{newText}";
    }
}
