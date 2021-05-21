using System.Collections.Generic;

namespace Embix.Core
{
    /// <summary>
    /// Metadata supplier. Implement this interface to supply calculated metadata
    /// from the current record and its context data from an <see cref="IIndexWriter"/>.
    /// </summary>
    public interface IMetadataSupplier
    {
        /// <summary>
        /// Supplies additional metadata for the specified token, storing them
        /// into <paramref name="metadata"/>.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="field">The field code.</param>
        /// <param name="token">The token value.</param>
        /// <param name="metadata">The metadata.</param>
        void Supply(string documentId, string field,
            string token, IDictionary<string, object> metadata);
    }
}
