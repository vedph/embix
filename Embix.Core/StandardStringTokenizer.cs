using Embix.Core.Filters;
using Fusi.Tools.Config;
using System.Collections.Generic;
using System.Text;

namespace Embix.Core;

/// <summary>
/// A simple string-tokenizer, which just spits out tokens as strings,
/// without any other information about them. Tokens are separated at
/// whitespace or apostrophe.
/// <para>Tag: <c>string-tokenizer.standard</c>.</para>
/// </summary>
[Tag("string-tokenizer.standard")]
public sealed class StandardStringTokenizer : IStringTokenizer
{
    // reuse these builders to avoid multiple instantiations overhead
    private readonly StringBuilder _sb;

    /// <summary>
    /// Gets or sets the optional filters to be applied at the token level.
    /// </summary>
    public CompositeTextFilter? Filter { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StandardStringTokenizer"/> class.
    /// </summary>
    public StandardStringTokenizer()
    {
        _sb = new StringBuilder();
    }

    private void SetCurrentToken(StringBuilder text, int start, int length)
    {
        _sb.Clear();
        for (int i = 0; i < length; i++)
            _sb.Append(text[start + i]);
    }

    /// <summary>
    /// Filters and then tokenizes the specified text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Tokens.</returns>
    public IEnumerable<string> Tokenize(StringBuilder text)
    {
        if (text == null || text.Length == 0) yield break;

        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ' || text[i] == '\'')
            {
                SetCurrentToken(text, start, i - start);
                Filter?.Apply(_sb);
                if (_sb.Length > 0) yield return _sb.ToString();
                start = ++i;
            }
        }

        // last token if any
        if (start < text.Length)
        {
            SetCurrentToken(text, start, text.Length - start);
            Filter?.Apply(_sb);
            if (_sb.Length > 0) yield return _sb.ToString();
        }
    }
}
