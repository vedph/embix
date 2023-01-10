using Embix.Core.Filters;
using Fusi.Text.Unicode;
using Fusi.Tools.Config;
using System;
using System.Text;

namespace Embix.Plugin.Greek;

/// <summary>
/// This filter replaces the Unicode characters in the Greek extended
/// Unicode region with their counterparts in the Greek region, wherever
/// possible. This allows further processing to avoid checking for both
/// codes.
/// Tag: <c>text-filter.grc-to-modern</c>.
/// </summary>
/// <seealso cref="ITextFilter" />
[Tag("text-filter.grc-to-modern")]
public sealed class GrcToModernTextFilter : ITextFilter
{
    /// <summary>
    /// Applies this filter to the specified text.
    /// </summary>
    /// <param name="text">The text wrapped in a <see cref="StringBuilder" />
    /// .</param>
    /// <exception cref="ArgumentNullException">text</exception>
    public void Apply(StringBuilder text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));

        for (int i = 0; i < text.Length; i++)
            text[i] = UniData.GreekToModern(text[i]);
    }
}
