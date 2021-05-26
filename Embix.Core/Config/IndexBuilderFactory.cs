using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SqlKata.Compilers;
using System;
using System.Reflection;

namespace Embix.Core.Config
{
    /// <summary>
    /// A base class for factories which ease the creation of a configured
    /// <see cref="IndexBuilder"/>.
    /// </summary>
    public abstract class IndexBuilderFactory : IIndexBuilderFactory
    {
        private readonly string _profileCode;
        private readonly IConfiguration _configuration;
        private IIndexWriter _writer;
        private readonly IDbConnectionFactory _connFactory;
        private readonly Assembly[] _addAssemblies;

        /// <summary>
        /// Gets or sets the size of the tokens buffer. Default is 100.
        /// </summary>
        public int BufferSize { get; set; }

        /// <summary>
        /// Gets the SQL compiler.
        /// </summary>
        protected Compiler SqlCompiler { get; }

        /// <summary>
        /// Gets the profile.
        /// </summary>
        public EmbixProfile Profile { get; }

        /// <summary>
        /// Gets or sets the dependency container, configured to include all
        /// the required assemblies for filter components.
        /// </summary>
        public Container Container { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexBuilderFactory"/> class.
        /// </summary>
        /// <param name="profile">The profile.</param>
        /// <param name="compiler">The compiler.</param>
        /// <param name="additionalAssemblies">The optional additional assemblies
        /// to look for filters and tokenizers.</param>
        /// <exception cref="ArgumentNullException">compiler or profile</exception>
        protected IndexBuilderFactory(
            string profile,
            IDbConnectionFactory factory,
            Compiler compiler,
            params Assembly[] additionalAssemblies)
        {
            _profileCode = profile ?? throw new ArgumentNullException(nameof(profile));
            _connFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            SqlCompiler = compiler ?? throw new ArgumentNullException(nameof(compiler));

            BufferSize = 100;

            _configuration = new ConfigurationBuilder()
                .AddInMemoryJson(profile).Build();
            _addAssemblies = additionalAssemblies;

            Profile = new EmbixProfile(profile, GetFactory());
        }

        private IIndexWriter GetIndexWriter()
        {
            return _writer ??= new SqlIndexWriter(Profile, _connFactory, SqlCompiler)
            {
                BufferSize = BufferSize
            };
        }

        private void EnsureContainer()
        {
            if (Container != null) return;

            Container = new Container();
            EmbixFilterFactory.ConfigureServices(Container, _addAssemblies);
            Container.Verify();
        }

        /// <summary>
        /// Gets a filters factory.
        /// </summary>
        /// <returns>Filters factory.</returns>
        protected EmbixFilterFactory GetFactory()
        {
            EnsureContainer();
            return new EmbixFilterFactory(Container, _configuration);
        }

        /// <summary>
        /// Gets the index builder.
        /// </summary>
        /// <param name="logger">The optional logger to use.</param>
        /// <returns>The index builder.</returns>
        public IndexBuilder GetBuilder(IMetadataSupplier supplier, ILogger logger)
        {
            EmbixProfile profile = new EmbixProfile(_profileCode, GetFactory());

            IIndexWriter writer = GetIndexWriter();
            writer.Logger = logger;
            writer.MetadataSupplier = supplier;

            return new IndexBuilder(profile, _connFactory, writer)
            {
                Logger = logger
            };
        }
    }
}
