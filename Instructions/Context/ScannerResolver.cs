using RayTracer.General;
using RayTracer.Scanners;

namespace RayTracer.Instructions.Context;

public class ScannerResolver : Resolver<IScanner>
{
    /// <summary>
    /// This holds the factory lambda that will create the appropriate scanner.
    /// </summary>
    public Func<IScanner> ScannerFactory { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override IScanner Resolve(RenderContext context, Variables variables)
    {
        return ScannerFactory();
    }
}
