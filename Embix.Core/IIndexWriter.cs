using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Embix.Core
{
    /// <summary>
    /// Interface implemented by objects writing to the database. These objects
    /// may provide a caching strategy if required, or directly write into the
    /// output.
    /// </summary>
    /// <remarks>The implementation of this interface must be thread-safe.
    /// </remarks>
    public interface IIndexWriter : IDisposable
    {
        /// <summary>
        /// Gets or sets the optional metadata supplier to be used.
        /// </summary>
        IMetadataSupplier MetadataSupplier { get; set; }

        /// <summary>
        /// Gets or sets the optional logger.
        /// </summary>
        ILogger Logger { get; set; }

        /// <summary>
        /// Writes the specified token from the specified field.
        /// </summary>
        /// <param name="documentId">The ID of the document data being written
        /// belong to.</param>
        /// <param name="partitionNr">The partition number of the caller.</param>
        /// <param name="field">The field the token comes from.</param>
        /// <param name="token">The token value.</param>
        /// <param name="metadata">The optional metadata for this token.
        /// Some tokens might be accompanied by some metadata; e.g. language,
        /// read from their source. In this case they are stored into this
        /// dictionary.
        /// </param>
        void Write(string documentId, int partitionNr, string field, string token,
            IDictionary<string, object> metadata = null);
    }
}
