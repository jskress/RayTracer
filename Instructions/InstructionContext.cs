using System.Diagnostics;
using RayTracer.General;
using RayTracer.ImageIO;
using RayTracer.Instructions.Context;
using RayTracer.Options;

namespace RayTracer.Instructions;

/// <summary>
/// This class is the root of our instruction set structure.  It is used to manage the
/// execution of all the instructions found in our input source in preparation for rendering
/// a single image.
/// </summary>
public class InstructionContext
{
    private readonly List<Instruction> _instructions = [];
    private readonly List<object> _objects = [];

    private int _renderInstructions;

    /// <summary>
    /// This property reports the names of the variables set so far, in the order the instructions
    /// setting them were added.  An import uses it to tell which names a library brought.
    /// </summary>
    internal IReadOnlyList<string> VariableNames => _instructions
        .OfType<SetVariableInstruction>()
        .Select(instruction => instruction.VariableName)
        .ToList();

    /// <summary>
    /// This property reports the names of the primitives declared so far.  An import uses it the same
    /// way it uses <see cref="VariableNames"/>: to tell which names a library brought.
    /// </summary>
    internal IReadOnlyList<string> PrimitiveNames => _instructions
        .OfType<DeclarePrimitiveInstruction>()
        .Select(instruction => instruction.Primitive.Name)
        .ToList();

    /// <summary>
    /// This property reports the names of the functions declared so far.  An import uses it the same
    /// way it uses <see cref="VariableNames"/>: to tell which names a library brought.
    /// </summary>
    internal IReadOnlyList<string> FunctionNames => _instructions
        .OfType<DeclareFunctionInstruction>()
        .Select(instruction => instruction.Function.Name)
        .ToList();

    /// <summary>
    /// This property reports whether every instruction is a plain definition -- a name bound to a
    /// value or a thing.  A file offered as a library must be, since anything else (a surface, a
    /// camera, a render command) would be dragged into every scene that imported it.
    /// </summary>
    internal bool HoldsOnlyDefinitions => _instructions.All(instruction =>
        IsADefinition(instruction) || instruction is ImportInstruction);

    /// <summary>
    /// This method reports whether one instruction merely gives something a name.  A value, a thing, a
    /// function and a primitive all count: each leaves a name behind and nothing else, which is the
    /// whole of what a library is for.  An import does not count as one, though a library may hold it:
    /// it brings in names for the library's own use, as `buildings` takes its windows from `windows`.
    /// </summary>
    /// <param name="instruction">The instruction to judge.</param>
    /// <returns>Whether it is a definition.</returns>
    private static bool IsADefinition(Instruction instruction)
    {
        return instruction is SetVariableInstruction or DeclareFunctionInstruction
            or DeclarePrimitiveInstruction;
    }

    /// <summary>
    /// This property reports how many definitions the source held.
    /// </summary>
    internal int DefinitionCount => _instructions.Count(IsADefinition);

    /// <summary>
    /// This method is used to add a new instruction to the set.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    public void AddInstruction(Instruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        if (instruction is RenderInstruction)
            _renderInstructions++;

        _instructions.Add(instruction);
    }

    /// <summary>
    /// This method is used to add a top-level object.
    /// </summary>
    /// <param name="value">The top-level object to add.</param>
    internal void AddTopLevelObject(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _objects.Add(value);
    }

    /// <summary>
    /// This method carries out the definitions this context holds against the given names, and does
    /// nothing else.
    /// <para>
    /// A library is run this way rather than through <see cref="Execute"/>, which would give it a
    /// render of its own for want of one.
    /// </para>
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The scope to carry them out in.</param>
    internal void CarryOutDefinitions(RenderContext context, Variables variables)
    {
        foreach (Instruction instruction in _instructions)
            instruction.Execute(context, variables);
    }

    /// <summary>
    /// This method is used to run all our instructions.
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="frame">The frame to render.</param>
    /// <param name="output">Where a frame of an animation goes, or null for a still.</param>
    public void Execute(
        RenderOptions options, RenderContext context, Variables variables, long frame, FrameOutput output = null)
    {
        if (_renderInstructions == 0)
            AddInstruction(new RenderInstruction(null, null));

        if (output is not null && _renderInstructions > 1)
        {
            throw new Exception(
                $"An animation is rendered through one render command, and this scene has {_renderInstructions}.");
        }

        // Each frame of an animation carries the scene out again from the start, and must not find the last
        // frame's things still standing.
        _objects.Clear();

        bool rendered = false;

        for (int index = 0; index < _instructions.Count; index++)
        {
            if (_instructions[index] is RenderInstruction renderInstruction)
            {
                // A second render carries the file out again, up to itself, into a context of its own, so that it
                // settles things of its own: settling the first render's again scaled their ambient twice, hung a
                // physical sky's sun a second time, and rendered a scene the first had already put away.
                if (rendered)
                {
                    context = context.Afresh();
                    _objects.Clear();

                    foreach (Instruction earlier in _instructions.Take(index).Where(earlier => earlier is not RenderInstruction))
                        earlier.Execute(context, variables);
                }

                CreateImage(options, context, variables, renderInstruction, frame, output);

                frame++;
                rendered = true;
            }
            else
                _instructions[index].Execute(context, variables);
        }
    }

    /// <summary>
    /// This method returns the animation the scene's context asks for, or null if it asks for none.  It is
    /// worked out before the scene is carried out at all, since every frame's time hangs on it, so it is read in
    /// the given scope alone: the renderer's own names and the frame's, not the scene's.
    /// </summary>
    /// <param name="variables">The scope to read it in.</param>
    /// <returns>The animation asked for, or null.</returns>
    internal Animation AnimationOf(Variables variables)
    {
        RenderContext context = new ();
        Animation animation = null;

        foreach (ContextUpdater updater in _instructions.OfType<ContextUpdater>())
            animation = updater.AnimationOf(context, variables) ?? animation;

        return animation;
    }

    /// <summary>
    /// This method actually renders an image using the given render instruction.
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="renderInstruction">The render instruction to use.</param>
    /// <param name="frame">The frame to render.</param>
    /// <param name="output">Where a frame of an animation goes, or null for a still.</param>
    private void CreateImage(
        RenderOptions options, RenderContext context, Variables variables,
        RenderInstruction renderInstruction, long frame, FrameOutput output)
    {
        context.ApplyOptions(options);

        renderInstruction.Objects = _objects;

        Terminal.Out("Generating...");

        Stopwatch stopwatch = Stopwatch.StartNew();

        renderInstruction.Execute(context, variables);

        stopwatch.Stop();

        if (output?.Movie is { } movie)
        {
            movie.Add(renderInstruction.Canvas, context);

            Terminal.Out($"Done!  It took {stopwatch.Elapsed}");

            return;
        }

        string fileName = GetOutputImageFile(options, frame, output?.Numbered == true);
        ImageFile outputFile = new ImageFile(fileName);

        Terminal.Out("Output file:", OutputLevel.Chatty);
        Terminal.Out($"--> {fileName}", OutputLevel.Chatty);
        Terminal.Out("Writing...");

        outputFile.Save(renderInstruction.Canvas, context, context.ImageInformation);

        Terminal.Out($"Done!  It took {stopwatch.Elapsed}");
    }

    /// <summary>
    /// This method is used to determine the image file we will write our rendered image
    /// to.
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    /// <param name="frame">The frame to render.</param>
    /// <param name="numbered">Whether the frame is one of several of an animation, and so carries its number.</param>
    /// <returns>The image file to write the generated image to.</returns>
    private string GetOutputImageFile(RenderOptions options, long frame, bool numbered)
    {
        string fileName = options.OutputFileName;

        if (numbered || _renderInstructions > 1)
        {
            string directory = Path.GetDirectoryName(fileName);
            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            fileName = Path.Combine(directory!, $"{name}-{frame:D7}{extension}");
        }

        return fileName;
    }
}

/// <summary>
/// This record says where the frames of an animation go: each to a file of its own carrying its number, or all
/// into one movie.
/// </summary>
/// <param name="Numbered">Whether each frame goes to a file of its own, its number in the name.</param>
/// <param name="Movie">The movie every frame goes into instead, or null.</param>
public sealed record FrameOutput(bool Numbered, MovieFile Movie);
