using Embix.Core.Config;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;

namespace Embix.Core
{
    /// <summary>
    /// SQL-based index writer.
    /// </summary>
    public class SqlIndexWriter : BufferedIndexWriter, IIndexWriter
    {
        private readonly IDbConnectionFactory _connFactory;

        /// <summary>
        /// Gets the SQL compiler.
        /// </summary>
        protected Compiler SqlCompiler { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlIndexWriter" /> class.
        /// </summary>
        /// <param name="profile">The profile to be used.</param>
        /// <param name="connFactory">The connection factory.</param>
        /// <param name="compiler">The SQL kata compiler to be used.</param>
        /// <exception cref="ArgumentNullException">profile or getConnection or
        /// compiler</exception>
        public SqlIndexWriter(
            EmbixProfile profile,
            IDbConnectionFactory connFactory,
            Compiler compiler) : base(profile)
        {
            _connFactory = connFactory
                ?? throw new ArgumentNullException(nameof(connFactory));
            SqlCompiler = compiler
                ?? throw new ArgumentNullException(nameof(compiler));
        }

        /// <summary>
        /// Gets a record writer.
        /// </summary>
        /// <returns>
        /// Record writer.
        /// </returns>
        protected override IIndexRecordWriter GetRecordWriter()
        {
            return new SqlIndexRecordWriter(new QueryFactory(
                _connFactory.GetConnection(), SqlCompiler));
        }
    }
}
