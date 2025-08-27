using System.Diagnostics;
using RayTracer.General;
using RayTracer.ImageIO;
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
    /// This method is used to run all our instructions.
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="frame">The frame to render.</param>
    public void Execute(
        RenderOptions options, RenderContext context, Variables variables, long frame)
    {
        if (_renderInstructions == 0)
            AddInstruction(new RenderInstruction(null, null));

        foreach (Instruction instruction in _instructions)
        {
            if (instruction is RenderInstruction renderInstruction)
            {
                CreateImage(options, context, variables, renderInstruction, frame);

                frame++;
            }
            else
                instruction.Execute(context, variables);
        }
    }

    /// <summary>
    /// This method actually renders an image using the given render instruction.
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="renderInstruction">The render instruction to use.</param>
    /// <param name="frame">The frame to render.</param>
    private void CreateImage(
        RenderOptions options, RenderContext context, Variables variables,
        RenderInstruction renderInstruction, long frame)
    {
        context.ApplyOptions(options, frame);

        renderInstruction.Objects = _objects;

        Terminal.Out("Generating...");

        Stopwatch stopwatch = Stopwatch.StartNew();

        renderInstruction.Execute(context, variables);

        stopwatch.Stop();

        string fileName = GetOutputImageFile(options, frame);
        ImageFile outputFile = new ImageFile(fileName);

        Terminal.Out("Output file:", OutputLevel.Chatty);
        Terminal.Out($"--> {options.OutputFileName}", OutputLevel.Chatty);
        Terminal.Out("Writing...");

        outputFile.Save(renderInstruction.Canvas, context.ImageInformation);

        Terminal.Out($"Done!  It took {stopwatch.Elapsed}");
    }

    /// <summary>
    /// This method is used to determine the image file we will write our rendered image
    /// to.
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    /// <param name="frame">The frame to render.</param>
    /// <returns>The image file to write the generated image to.</returns>
    private string GetOutputImageFile(RenderOptions options, long frame)
    {
        string fileName = options.OutputFileName;

        if (_renderInstructions > 1)
        {
            string directory = Path.GetDirectoryName(fileName);
            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            fileName = Path.Combine(directory!, $"{name}-{frame:D7}{extension}");
        }

        return fileName;
    }
}
