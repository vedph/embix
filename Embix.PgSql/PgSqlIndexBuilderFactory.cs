using Embix.Core.Config;
using System;
using SqlKata.Compilers;
using System.Reflection;

namespace Embix.PgSql
{
    /// <summary>
    /// PostgreSql index builder factory.
    /// </summary>
    /// <seealso cref="IndexBuilderFactoryBase" />
    public sealed class PgSqlIndexBuilderFactory : IndexBuilderFactoryBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PgSqlIndexBuilderFactory"/>
        /// class.
        /// </summary>
        /// <param name="profile">The profile code.</param>
        /// <param name="connString">The connection string.</param>
        /// <exception cref="ArgumentNullException">connString</exception>
        public PgSqlIndexBuilderFactory(string profile, string connString,
            params Assembly[] additionalAssemblies)
            : base(profile,
                  new PgSqlDbConnectionFactory(connString),
                  new PostgresCompiler(),
                  additionalAssemblies)
        {
        }
    }
}
