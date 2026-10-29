using Embix.Core;
using Embix.Core.Config;
using System.Reflection;

namespace Embix.PgSql;

/// <summary>
/// PostgreSql index builder factory. Its builders write via
/// <see cref="PgSqlIndexWriter"/>.
/// </summary>
/// <seealso cref="IndexBuilderFactory" />
public sealed class PgSqlIndexBuilderFactory : IndexBuilderFactory
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgSqlIndexBuilderFactory"/>
    /// class.
    /// </summary>
    /// <param name="profile">The profile code.</param>
    /// <param name="connString">The connection string.</param>
    /// <param name="additionalAssemblies">The optional additional assemblies
    /// to look for filters and tokenizers.</param>
    /// <exception cref="System.ArgumentNullException">profile or connString
    /// </exception>
    public PgSqlIndexBuilderFactory(string profile, string connString,
        params Assembly[] additionalAssemblies)
        : base(profile,
              new PgSqlDbConnectionFactory(connString),
              additionalAssemblies)
    {
    }

    /// <summary>
    /// Creates the index writer.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Writer.</returns>
    protected override IIndexWriter CreateIndexWriter(EmbixProfile profile) =>
        new PgSqlIndexWriter(profile, ConnectionFactory);
}
