using Embix.Core.Filters;
using System.Collections.Generic;
using System.Text;

namespace Embix.Core;

/// <summary>
/// A tokenizer interface.
/// </summary>
/// <typeparam name="T">The type of token to be returned.</typeparam>
public interface ITokenizer<T>
{
    /// <summary>
    /// Gets or sets the optional filter.
    /// </summary>
    CompositeTextFilter? Filter { get; set; }

    /// <summary>
    /// Tokenizes the specified text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>A sequence of tokens.</returns>
    IEnumerable<T> Tokenize(StringBuilder text);
}
