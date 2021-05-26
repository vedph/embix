using Microsoft.Extensions.Logging;
using SimpleInjector;

namespace Embix.Core.Config
{
    /// <summary>
    /// Index builder factory interface.
    /// </summary>
    public interface IIndexBuilderFactory
    {
        /// <summary>
        /// Gets or sets the size of the tokens buffer.
        /// </summary>
        int BufferSize { get; set; }

        /// <summary>
        /// Gets or sets the dependency container, configured to include all
        /// the required assemblies for filter components.
        /// </summary>
        Container Container { get; }

        /// <summary>
        /// Gets the profile.
        /// </summary>
        EmbixProfile Profile { get; }

        /// <summary>
        /// Gets the builder.
        /// </summary>
        /// <param name="supplier">The optional metadata supplier for the
        /// index writer.</param>
        /// <param name="logger">The optional logger.</param>
        /// <returns>Builder.</returns>
        IndexBuilder GetBuilder(IMetadataSupplier supplier, ILogger logger);
    }
}