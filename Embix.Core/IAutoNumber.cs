namespace Embix.Core
{
    /// <summary>
    /// Interface implemented by components capable of generating thread-safe
    /// auto numbering.
    /// </summary>
    public interface IAutoNumber
    {
        /// <summary>
        /// Gets the next available identifier.
        /// </summary>
        /// <returns>ID.</returns>
        int GetNextId();
    }
}
