using RayTracer.General;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class provides an instruction that will initialize an instance of the noisy
/// pigment.
/// </summary>
public class InitializeNoisyPigment : Instruction
{
    private readonly Term _depthTerm;
    private readonly bool _phased;
    private readonly Term _tightnessTerm;
    private readonly Term _scaleTerm;

    private int _depth;
    private int _tightness;
    private double _scale;
 
    public InitializeNoisyPigment(Term depthTerm, bool phased, Term tightnessTerm, Term scaleTerm)
    {
        _depthTerm = depthTerm;
        _phased = phased;
        _tightnessTerm = tightnessTerm;
        _scaleTerm = scaleTerm;

        _depth = 1;
        _tightness = 10;
        _scale = 1;
    }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        _depth = _depthTerm.GetValue<int>(variables);

        if (_tightnessTerm != null)
            _tightness = _tightnessTerm.GetValue<int>(variables);

        if (_scaleTerm != null)
            _scale = _scaleTerm.GetValue<double>(variables);
    }

    /// <summary>
    /// This method is used to apply our current values to the given pigment.
    /// </summary>
    /// <param name="pigment">The pigment to apply our values to.</param>
    internal void ApplyTo(NoisyPigment pigment)
    {
        pigment.Depth = _depth;
        pigment.Phased = _phased;
        pigment.Tightness = _tightness;
        pigment.Scale = _scale;
    }
}
