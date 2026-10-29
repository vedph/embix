using Embix.Core;
using Embix.Core.Config;
using System.Reflection;

namespace Embix.MySql;

/// <summary>
/// MySql index builder factory.
/// </summary>
/// <seealso cref="IndexBuilderFactory" />
public sealed class MySqlIndexBuilderFactory : IndexBuilderFactory
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlIndexBuilderFactory"/>
    /// class.
    /// </summary>
    /// <param name="profile">The profile code.</param>
    /// <param name="connString">The connection string.</param>
    /// <param name="additionalAssemblies">The optional additional assemblies
    /// to look for filters and tokenizers.</param>
    /// <exception cref="System.ArgumentNullException">connString</exception>
    public MySqlIndexBuilderFactory(string profile, string connString,
        params Assembly[] additionalAssemblies)
        : base(profile,
              new MySqlDbConnectionFactory(connString),
              additionalAssemblies)
    {
    }

    /// <summary>
    /// Creates the index writer, quoting identifiers with backticks.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Writer.</returns>
    protected override IIndexWriter CreateIndexWriter(EmbixProfile profile) =>
        new SqlIndexWriter(profile, ConnectionFactory, "`", "`");
}
