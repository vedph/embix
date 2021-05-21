using System.Data;

namespace Embix.Core
{
    /// <summary>
    /// A connection factory interface.
    /// </summary>
    public interface IDbConnectionFactory
    {
        /// <summary>
        /// Gets a new connection to the database.
        /// </summary>
        /// <returns>Connection.</returns>
        IDbConnection GetConnection();
    }
}
