using Embix.Core.Config;
using SqlKata.Compilers;
using System;
using System.Reflection;

namespace Embix.MySql
{
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
        /// <exception cref="ArgumentNullException">connString</exception>
        public MySqlIndexBuilderFactory(string profile, string connString,
            params Assembly[] additionalAssemblies)
            : base(profile,
                  new MySqlDbConnectionFactory(connString),
                  new MySqlCompiler(),
                  additionalAssemblies)
        {
        }
    }
}
