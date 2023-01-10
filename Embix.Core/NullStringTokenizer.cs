using Embix.Core.Filters;
using Fusi.Tools.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace Embix.Core;

/// <summary>
/// Null string tokenizer. This just returns the input text as a unique
/// token, eventually filtered.
/// </summary>
/// <seealso cref="IStringTokenizer" />
[Tag("string-tokenizer.null")]
public sealed class NullStringTokenizer : IStringTokenizer
{
    /// <summary>
    /// Gets or sets the filter.
    /// </summary>
    public CompositeTextFilter? Filter { get; set; }

    /// <summary>
    /// Just returns the specified text as a unique token, eventually
    /// filtered.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text as a filtered token.</returns>
    public IEnumerable<string> Tokenize(StringBuilder text)
    {
        if (Filter != null)
        {
            Filter.Apply(text);
            if (text.Length == 0) return Array.Empty<string>();
        }
        return new[] { text.ToString() };
    }
}
