using CommandLine.Text;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Instructions;

namespace RayTracer.Renderer;

/// <summary>
/// This class provides the work of actually rendering images.
/// </summary>
public class ImageRenderer
{
    private readonly InstructionContext _instructionContext;
    private readonly Statistics _statistics;
    private readonly Variables _globals;

    public ImageRenderer(InstructionContext instructionsContext)
    {
        string software = HeadingInfo.Default.ToString();

        _instructionContext = instructionsContext;
        _statistics = new Statistics();
        _globals = new Variables();

        _globals.SetValue("__software__", software);
        _globals.SetValue("π", Math.PI);
        _globals.SetValue("Identity", Matrix.Identity);
        _globals.SetValue("NegativeInfinity", double.NegativeInfinity);
        _globals.SetValue("PositiveInfinity", double.PositiveInfinity);

        Colors.AddToVariables(_globals);
        IndicesOfRefraction.AddToVariables(_globals);
        Directions.AddToVariables(_globals);
    }

    /// <summary>
    /// This method is used to render all images called for in our input files.
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    public void Render(ProgramOptions options)
    {
        RenderImage(options, options.Frame ?? 0);
    }

    /// <summary>
    /// This method is used to render one particular frame.
    /// </summary>
    /// <param name="options">The command line options supplied by the user.</param>
    /// <param name="frame">The frame to render.</param>
    private void RenderImage(ProgramOptions options, long frame)
    {
        RenderContext context = new ()
        {
            ProgressBar = new ProgressBar(),
            Statistics = _statistics
        };
        Variables variables = new Variables(_globals);

        _instructionContext.Execute(options, context, variables, frame);
    }
}
