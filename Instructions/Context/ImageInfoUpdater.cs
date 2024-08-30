using RayTracer.General;

namespace RayTracer.Instructions.Context;

/// <summary>
/// This class contains the resolvers that will be used to assign values to the current
/// rendering context's image information object.
/// </summary>
public class ImageInfoUpdater : Instruction
{
    /// <summary>
    /// This property provides the resolver, if any, for updating the title property in
    /// the rendering context's image information.
    /// </summary>
    public Resolver<string> TitleResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for updating the author property in
    /// the rendering context's image information.
    /// </summary>
    public Resolver<string> AuthorResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for updating the description property
    /// in the rendering context's image information.
    /// </summary>
    public Resolver<string> DescriptionResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for updating the copyright property
    /// in the rendering context's image information.
    /// </summary>
    public CopyrightResolver CopyrightResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for updating the software property
    /// in the rendering context's image information.
    /// </summary>
    public Resolver<string> SoftwareResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for updating the disclaimer property
    /// in the rendering context's image information.
    /// </summary>
    public Resolver<string> DisclaimerResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for updating the warning property
    /// in the rendering context's image information.
    /// </summary>
    public Resolver<string> WarningResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for updating the source property
    /// in the rendering context's image information.
    /// </summary>
    public Resolver<string> SourceResolver { get; set; }

    /// <summary>
    /// This property provides the resolver, if any, for updating the comment property
    /// in the rendering context's image information.
    /// </summary>
    public CommentResolver CommentResolver { get; set; }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        context.ImageInformation ??= new ImageInformation();

        TitleResolver.AssignTo(
            context.ImageInformation, target => target.Title, context, variables);
        AuthorResolver.AssignTo(
            context.ImageInformation, target => target.Author, context, variables);
        DescriptionResolver.AssignTo(
            context.ImageInformation, target => target.Description, context, variables);
        CopyrightResolver.AssignTo(
            context.ImageInformation, target => target.Copyright, context, variables);
        SoftwareResolver.AssignTo(
            context.ImageInformation, target => target.Software, context, variables);
        DisclaimerResolver.AssignTo(
            context.ImageInformation, target => target.Disclaimer, context, variables);
        WarningResolver.AssignTo(
            context.ImageInformation, target => target.Warning, context, variables);
        SourceResolver.AssignTo(
            context.ImageInformation, target => target.Source, context, variables);
        CommentResolver.AssignTo(
            context.ImageInformation, target => target.Comment, context, variables);
    }
}
