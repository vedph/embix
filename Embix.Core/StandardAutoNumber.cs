using System.Threading;

namespace Embix.Core;

/// <summary>
/// Standard implementation for <see cref="IAutoNumber"/>.
/// </summary>
/// <seealso cref="IAutoNumber" />
public class StandardAutoNumber : IAutoNumber
{
    private int _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="StandardAutoNumber"/>
    /// class.
    /// </summary>
    /// <param name="seed">The seed number to start with.</param>
    public StandardAutoNumber(int seed = 0)
    {
        _counter = seed;
    }

    /// <summary>
    /// Gets the next available identifier.
    /// </summary>
    /// <returns>ID.</returns>
    public int GetNextId()
    {
        // thread-safe increment
        return Interlocked.Increment(ref _counter);
    }
}
