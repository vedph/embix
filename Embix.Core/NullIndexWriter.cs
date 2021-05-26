using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Embix.Core
{
    /// <summary>
    /// A null index writer, which does nothing.
    /// </summary>
    /// <seealso cref="IIndexWriter" />
    public sealed class NullIndexWriter : IIndexWriter
    {
        private bool _disposed;

        /// <summary>
        /// Gets or sets the optional metadata supplier to be used.
        /// </summary>
        public IMetadataSupplier MetadataSupplier { get; set; }

        /// <summary>
        /// Gets or sets the optional logger.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Does nothing.
        /// </summary>
        /// <param name="documentId">The ID of the document data being written
        /// belong to.</param>
        /// <param name="partitionNr">The partition number of the caller.</param>
        /// <param name="field">The field the token comes from.</param>
        /// <param name="token">The token value.</param>
        /// <param name="metadata">The optional metadata for this token.
        /// Some tokens might be accompanied by some metadata; e.g. language,
        /// read from their source. In this case they are stored into this
        /// dictionary.</param>
        /// <exception cref="ArgumentNullException">field or token</exception>
        public void Write(string documentId, int partitionNr, string field,
            string token,
            IDictionary<string, object> metadata = null)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            Logger?.LogDebug($"[#{partitionNr:000}] Write: {documentId}.{field}: {token}");
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
