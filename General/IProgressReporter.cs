namespace RayTracer.General;

/// <summary>
/// This interface is how a render says how far along it is.
/// <para>
/// There are two ways of answering that question and they want quite different things.  A person
/// watching a terminal wants one line that keeps being rewritten, in colour, with a bar and a time
/// remaining -- see <see cref="ProgressBar"/>.  A program watching the same render wants the
/// opposite: whole lines it can read one at a time, with numbers in them, and no control characters
/// at all.  A bar drawn with carriage returns is invisible through a pipe, so a tool watching one has
/// nothing to go on but the age of the process, which says nothing about whether any pixels are being
/// produced -- it cannot tell slow from stuck.
/// </para>
/// <para>
/// The render reports through this and does not care which is listening.
/// </para>
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// This method says how much work there is to do in total.
    /// </summary>
    /// <param name="total">The number of units of work to expect.</param>
    void SetTotal(long total);

    /// <summary>
    /// This method says that one more unit of work is finished.  It is called from every thread a
    /// scanner is using, so an implementation must be safe to call concurrently.
    /// </summary>
    void Bump();

    /// <summary>
    /// This method says the work is over, however it ended.  It is called from a <c>finally</c>, so
    /// it must cope with being called after a failure part way through.
    /// </summary>
    void Done();
}
