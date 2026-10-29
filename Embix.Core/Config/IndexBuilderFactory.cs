using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using System;
using System.Reflection;

namespace Embix.Core.Config;

/// <summary>
/// A base class for factories which ease the creation of a configured
/// <see cref="IndexBuilder"/>. All the builders got from the same factory
/// share the same index writer, so that token IDs are consistent across
/// documents. The writer is owned by the factory, which disposes it when
/// disposed.
/// </summary>
public abstract class IndexBuilderFactory : IIndexBuilderFactory, IDisposable
{
    private readonly string _profileCode;
    private readonly IConfiguration _configuration;
    private readonly Assembly[] _addAssemblies;
    private IIndexWriter? _writer;
    private Container? _container;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the size of the tokens buffer. Default is 10000: larger
    /// buffers mean fewer database roundtrips, which is especially relevant
    /// for COPY.
    /// </summary>
    public int BufferSize { get; set; }

    /// <summary>
    /// Gets the connection factory.
    /// </summary>
    protected IDbConnectionFactory ConnectionFactory { get; }

    /// <summary>
    /// Gets the profile.
    /// </summary>
    public EmbixProfile Profile { get; }

    /// <summary>
    /// Gets the dependency container, configured to include all
    /// the required assemblies for filter components.
    /// </summary>
    public Container Container => EnsureContainer();

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexBuilderFactory"/> class.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="factory">The connection factory.</param>
    /// <param name="additionalAssemblies">The optional additional assemblies
    /// to look for filters and tokenizers.</param>
    /// <exception cref="ArgumentNullException">profile or factory</exception>
    protected IndexBuilderFactory(
        string profile,
        IDbConnectionFactory factory,
        params Assembly[] additionalAssemblies)
    {
        _profileCode = profile ?? throw new ArgumentNullException(nameof(profile));
        ConnectionFactory = factory
            ?? throw new ArgumentNullException(nameof(factory));

        BufferSize = 10000;

        _configuration = new ConfigurationBuilder()
            .AddInMemoryJson(profile).Build();
        _addAssemblies = additionalAssemblies ?? [];

        Profile = new EmbixProfile(profile, GetFactory());
    }

    /// <summary>
    /// Creates the index writer. Override this to provide a database-specific
    /// writer. The default implementation returns a <see cref="SqlIndexWriter"/>.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Writer.</returns>
    protected virtual IIndexWriter CreateIndexWriter(EmbixProfile profile) =>
        new SqlIndexWriter(profile, ConnectionFactory);

    private IIndexWriter GetIndexWriter()
    {
        if (_writer != null) return _writer;

        _writer = CreateIndexWriter(Profile);
        if (_writer is BufferedIndexWriter buffered)
            buffered.BufferSize = BufferSize;
        return _writer;
    }

    private Container EnsureContainer()
    {
        if (_container != null) return _container;

        _container = new Container();
        EmbixFilterFactory.ConfigureServices(_container, _addAssemblies);
        _container.Verify();
        return _container;
    }

    /// <summary>
    /// Gets a filters factory.
    /// </summary>
    /// <returns>Filters factory.</returns>
    protected EmbixFilterFactory GetFactory() =>
        new(EnsureContainer(), _configuration);

    /// <summary>
    /// Gets an index builder. The builder does not own the index writer,
    /// which is shared among all the builders from this factory.
    /// </summary>
    /// <param name="supplier">The optional metadata supplier.</param>
    /// <param name="logger">The optional logger to use.</param>
    /// <returns>The index builder.</returns>
    public IndexBuilder GetBuilder(IMetadataSupplier? supplier, ILogger? logger)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EmbixProfile profile = new(_profileCode, GetFactory());

        IIndexWriter writer = GetIndexWriter();
        writer.Logger = logger;
        writer.MetadataSupplier = supplier;

        return new IndexBuilder(profile, ConnectionFactory, writer, false)
        {
            Logger = logger
        };
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    /// <param name="disposing">True to release managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _writer?.Dispose();
            _container?.Dispose();
        }
        _disposed = true;
    }

    /// <summary>
    /// Disposes the shared index writer, flushing it.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
