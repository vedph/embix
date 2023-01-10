namespace Embix.Core;

/// <summary>
/// Index tables initializer interface.
/// </summary>
public interface ITableInitializer
{
    /// <summary>
    /// Initializes the database index tables by creating them if not
    /// present; if the tables are already present and <paramref name="clear"/>
    /// is true, they are truncated.
    /// </summary>
    /// <param name="clear">if set to <c>true</c>, truncate the index
    /// tables when present.</param>
    void Initialize(bool clear);
}
