using Embix.Core.Filters;
using Fusi.Tools.Config;
using System;
using System.Text;

namespace Embix.Plugin.Greek;

/// <summary>
/// Greek sigma to lunate sigma text filter.
/// This converts any lowercase or uppercase sigma to its lunate counterpart.
/// The lunate sigma is convenient for indexing as it is not distinguished
/// into final and non-final.
/// Tag: <c>text-filter.grc-sigma-to-lunate</c>.
/// </summary>
/// <seealso cref="ITextFilter" />
[Tag("text-filter.grc-sigma-to-lunate")]
public sealed class GrcSigmaToLunateTextFilter : ITextFilter
{
    public void Apply(StringBuilder text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));

        for (int i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '\u03c2':  // lowercase final sigma
                case '\u03c3':  // lowercase sigma
                    text[i] = '\u03f2';
                    break;
                case '\u03a3':  // capital sigma
                    text[i] = '\u03f9';
                    break;
            }
        }
    }
}
