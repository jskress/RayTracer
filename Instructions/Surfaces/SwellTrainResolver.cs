using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve one train of waves on a body of water.
/// </summary>
public class SwellTrainResolver : ObjectResolver<SwellTrain>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for how tall the train's waves are.
    /// </summary>
    public Resolver<double> AmplitudeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the train's steepness, which is the other way of saying
    /// how tall it is: its height against its wavelength.
    /// </summary>
    public Resolver<double> SteepnessResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far apart the train's crests are.
    /// </summary>
    public Resolver<double> WavelengthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for which way the train travels.
    /// </summary>
    public Resolver<Vector> DirectionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for where in its cycle the train stands at the origin.
    /// </summary>
    public Resolver<double> PhaseResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a train.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, SwellTrain value)
    {
        WavelengthResolver.AssignTo(value, target => target.Wavelength, context, variables);
        DirectionResolver.AssignTo(value, target => target.Direction, context, variables);
        PhaseResolver.AssignTo(value, target => target.Phase, context, variables);
        AmplitudeResolver.AssignTo(value, target => target.Amplitude, context, variables);

        // Steepness is set after the wavelength, and after the amplitude, because it is a way of
        // *saying* the amplitude: how tall the waves are, given how far apart they are.  It is the
        // number worth writing a scene in, since it means the same thing at every scale -- a swell
        // of steepness 0.2 reads as the same water whether its crests are a foot apart or fifty.
        if (SteepnessResolver is not null)
        {
            value.Amplitude = SteepnessResolver.Resolve(context, variables) * value.Wavelength /
                              (2 * Math.PI);
        }
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error message, or
    /// <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (AmplitudeResolver is null && SteepnessResolver is null)
            return "A wave train needs either an amplitude or a steepness to say how tall it is.";

        return AmplitudeResolver is not null && SteepnessResolver is not null
            ? "A wave train may be given an amplitude or a steepness, but not both; each says how " +
              "tall it is and they would disagree."
            : null;
    }
}
