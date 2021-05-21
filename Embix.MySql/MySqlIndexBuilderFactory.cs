using Embix.Core.Config;
using SqlKata.Compilers;
using System;

namespace Embix.MySql
{
    /// <summary>
    /// MySql index builder factory.
    /// </summary>
    /// <seealso cref="IndexBuilderFactoryBase" />
    public sealed class MySqlIndexBuilderFactory : IndexBuilderFactoryBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlIndexBuilderFactory"/>
        /// class.
        /// </summary>
        /// <param name="profile">The profile code.</param>
        /// <param name="connString">The connection string.</param>
        /// <exception cref="ArgumentNullException">connString</exception>
        public MySqlIndexBuilderFactory(string profile, string connString)
            : base(profile,
                  new MySqlDbConnectionFactory(connString),
                  new MySqlCompiler())
        {
        }
    }
}
